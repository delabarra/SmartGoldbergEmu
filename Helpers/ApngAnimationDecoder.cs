using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace SmartGoldbergEmu.Helpers
{
    // Minimal APNG decoder: GDI+ only loads the default frame, so we rebuild each frame PNG and composite.
    public static class ApngAnimationDecoder
    {
        private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        public sealed class FrameBitmap : IDisposable
        {
            public Bitmap Bitmap { get; private set; }
            public int DelayMilliseconds { get; private set; }

            public FrameBitmap(Bitmap bitmap, int delayMilliseconds)
            {
                Bitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
                DelayMilliseconds = delayMilliseconds < 10 ? 10 : delayMilliseconds;
            }

            public void Dispose()
            {
                Bitmap?.Dispose();
                Bitmap = null;
            }
        }

        public static bool TryDecode(string filePath, out FrameBitmap[] frames)
        {
            frames = null;
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return false;

            try
            {
                byte[] fileBytes = File.ReadAllBytes(filePath);
                return TryDecode(fileBytes, out frames);
            }
            catch
            {
                frames = null;
                return false;
            }
        }

        public static bool TryDecode(byte[] fileBytes, out FrameBitmap[] frames)
        {
            frames = null;
            if (fileBytes == null || fileBytes.Length < 8)
                return false;

            for (int i = 0; i < PngSignature.Length; i++)
            {
                if (fileBytes[i] != PngSignature[i])
                    return false;
            }

            try
            {
                var chunks = ReadChunks(fileBytes);
                if (chunks.Count == 0)
                    return false;

                PngChunk ihdr = null;
                var otherChunks = new List<PngChunk>();
                var frameDefs = new List<FrameDef>();
                FrameDef current = null;
                FrameDef defaultImage = new FrameDef();
                bool sawAcTl = false;
                bool idatStarted = false;

                foreach (var chunk in chunks)
                {
                    switch (chunk.Type)
                    {
                        case "IHDR":
                            if (ihdr != null)
                                return false;
                            ihdr = chunk;
                            break;
                        case "acTL":
                            sawAcTl = true;
                            break;
                        case "fcTL":
                            if (!sawAcTl)
                                break;
                            if (!TryParseFcTl(chunk.Data, out var fcTl))
                                return false;
                            if (!idatStarted)
                            {
                                defaultImage.FcTl = fcTl;
                            }
                            else
                            {
                                if (current != null)
                                    frameDefs.Add(current);
                                current = new FrameDef { FcTl = fcTl };
                            }
                            break;
                        case "IDAT":
                            if (!sawAcTl)
                                return false;
                            idatStarted = true;
                            defaultImage.IdatChunks.Add(chunk);
                            break;
                        case "fdAT":
                            if (current == null || chunk.Data.Length < 4)
                                return false;
                            var idatData = new byte[chunk.Data.Length - 4];
                            Buffer.BlockCopy(chunk.Data, 4, idatData, 0, idatData.Length);
                            current.IdatChunks.Add(new PngChunk("IDAT", idatData));
                            break;
                        case "IEND":
                            if (current != null)
                                frameDefs.Add(current);
                            break;
                        default:
                            if (IsCopyThroughChunk(chunk.Type))
                                otherChunks.Add(chunk);
                            break;
                    }
                }

                if (ihdr == null || !sawAcTl)
                    return false;

                if (defaultImage.FcTl != null && defaultImage.IdatChunks.Count > 0)
                    frameDefs.Insert(0, defaultImage);

                if (frameDefs.Count == 0)
                    return false;

                int canvasWidth = ReadInt32Be(ihdr.Data, 0);
                int canvasHeight = ReadInt32Be(ihdr.Data, 4);
                if (canvasWidth <= 0 || canvasHeight <= 0)
                    return false;

                var rendered = new List<FrameBitmap>(frameDefs.Count);
                using (var canvas = new Bitmap(canvasWidth, canvasHeight, PixelFormat.Format32bppArgb))
                using (var graphics = Graphics.FromImage(canvas))
                {
                    graphics.Clear(Color.Transparent);
                    Bitmap previousSnapshot = null;

                    try
                    {
                        foreach (var def in frameDefs)
                        {
                            if (def.FcTl == null || def.IdatChunks.Count == 0)
                                continue;

                            var fc = def.FcTl.Value;
                            if (fc.DisposeOp == 2)
                            {
                                previousSnapshot?.Dispose();
                                previousSnapshot = new Bitmap(canvas);
                            }

                            using (var framePng = BuildFramePng(ihdr, otherChunks, def.IdatChunks, fc.Width, fc.Height))
                            using (var frameImage = Image.FromStream(framePng, useEmbeddedColorManagement: false, validateImageData: false))
                            {
                                var dest = new Rectangle(fc.XOffset, fc.YOffset, fc.Width, fc.Height);
                                if (fc.BlendOp == 0)
                                {
                                    using (var clearBrush = new SolidBrush(Color.Transparent))
                                    using (var g = Graphics.FromImage(canvas))
                                    {
                                        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                                        g.FillRectangle(clearBrush, dest);
                                        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                                        g.DrawImage(frameImage, dest);
                                    }
                                }
                                else
                                {
                                    graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                                    graphics.DrawImage(frameImage, dest);
                                }
                            }

                            rendered.Add(new FrameBitmap(new Bitmap(canvas), ComputeDelayMs(fc.DelayNum, fc.DelayDen)));

                            if (fc.DisposeOp == 1)
                            {
                                using (var g = Graphics.FromImage(canvas))
                                {
                                    g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                                    using (var clearBrush = new SolidBrush(Color.Transparent))
                                        g.FillRectangle(clearBrush, new Rectangle(fc.XOffset, fc.YOffset, fc.Width, fc.Height));
                                }
                            }
                            else if (fc.DisposeOp == 2 && previousSnapshot != null)
                            {
                                using (var g = Graphics.FromImage(canvas))
                                {
                                    g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                                    g.DrawImageUnscaled(previousSnapshot, 0, 0);
                                }
                            }
                        }
                    }
                    finally
                    {
                        previousSnapshot?.Dispose();
                    }
                }

                if (rendered.Count == 0)
                    return false;

                frames = rendered.ToArray();
                return true;
            }
            catch
            {
                if (frames != null)
                {
                    foreach (var frame in frames)
                        frame?.Dispose();
                }
                frames = null;
                return false;
            }
        }

        private static bool IsCopyThroughChunk(string type)
        {
            return type == "PLTE"
                || type == "tRNS"
                || type == "gAMA"
                || type == "cHRM"
                || type == "sRGB"
                || type == "iCCP"
                || type == "bKGD"
                || type == "pHYs"
                || type == "sBIT";
        }

        private static int ComputeDelayMs(ushort delayNum, ushort delayDen)
        {
            int den = delayDen == 0 ? 100 : delayDen;
            int ms = (int)Math.Round(delayNum * 1000.0 / den);
            return ms < 10 ? 10 : ms;
        }

        private static MemoryStream BuildFramePng(
            PngChunk ihdr,
            List<PngChunk> otherChunks,
            List<PngChunk> idatChunks,
            int frameWidth,
            int frameHeight)
        {
            var ihdrData = (byte[])ihdr.Data.Clone();
            WriteInt32Be(ihdrData, 0, frameWidth);
            WriteInt32Be(ihdrData, 4, frameHeight);

            var ms = new MemoryStream();
            ms.Write(PngSignature, 0, PngSignature.Length);
            WriteChunk(ms, "IHDR", ihdrData);
            for (int i = 0; i < otherChunks.Count; i++)
                WriteChunk(ms, otherChunks[i].Type, otherChunks[i].Data);
            for (int i = 0; i < idatChunks.Count; i++)
                WriteChunk(ms, "IDAT", idatChunks[i].Data);
            WriteChunk(ms, "IEND", Array.Empty<byte>());
            ms.Position = 0;
            return ms;
        }

        private static List<PngChunk> ReadChunks(byte[] fileBytes)
        {
            var chunks = new List<PngChunk>();
            int offset = 8;
            while (offset + 12 <= fileBytes.Length)
            {
                int length = ReadInt32Be(fileBytes, offset);
                if (length < 0 || offset + 12 + length > fileBytes.Length)
                    break;

                string type = System.Text.Encoding.ASCII.GetString(fileBytes, offset + 4, 4);
                var data = new byte[length];
                if (length > 0)
                    Buffer.BlockCopy(fileBytes, offset + 8, data, 0, length);

                chunks.Add(new PngChunk(type, data));
                offset += 12 + length;
                if (type == "IEND")
                    break;
            }
            return chunks;
        }

        private static bool TryParseFcTl(byte[] data, out FcTl fcTl)
        {
            fcTl = default(FcTl);
            if (data == null || data.Length < 26)
                return false;

            fcTl = new FcTl
            {
                Width = ReadInt32Be(data, 4),
                Height = ReadInt32Be(data, 8),
                XOffset = ReadInt32Be(data, 12),
                YOffset = ReadInt32Be(data, 16),
                DelayNum = ReadUInt16Be(data, 20),
                DelayDen = ReadUInt16Be(data, 22),
                DisposeOp = data[24],
                BlendOp = data[25]
            };
            return fcTl.Width > 0 && fcTl.Height > 0;
        }

        private static void WriteChunk(Stream stream, string type, byte[] data)
        {
            byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
            WriteInt32Be(stream, data.Length);
            stream.Write(typeBytes, 0, 4);
            if (data.Length > 0)
                stream.Write(data, 0, data.Length);

            uint crc = Crc32.Compute(typeBytes, 0, 4);
            crc = Crc32.Compute(crc, data, 0, data.Length);
            WriteUInt32Be(stream, crc);
        }

        private static int ReadInt32Be(byte[] buffer, int offset)
        {
            return (buffer[offset] << 24)
                | (buffer[offset + 1] << 16)
                | (buffer[offset + 2] << 8)
                | buffer[offset + 3];
        }

        private static ushort ReadUInt16Be(byte[] buffer, int offset)
        {
            return (ushort)((buffer[offset] << 8) | buffer[offset + 1]);
        }

        private static void WriteInt32Be(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }

        private static void WriteInt32Be(Stream stream, int value)
        {
            stream.WriteByte((byte)(value >> 24));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }

        private static void WriteUInt32Be(Stream stream, uint value)
        {
            stream.WriteByte((byte)(value >> 24));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }

        private sealed class PngChunk
        {
            public string Type { get; }
            public byte[] Data { get; }

            public PngChunk(string type, byte[] data)
            {
                Type = type;
                Data = data ?? Array.Empty<byte>();
            }
        }

        private sealed class FrameDef
        {
            public FcTl? FcTl;
            public readonly List<PngChunk> IdatChunks = new List<PngChunk>();
        }

        private struct FcTl
        {
            public int Width;
            public int Height;
            public int XOffset;
            public int YOffset;
            public ushort DelayNum;
            public ushort DelayDen;
            public byte DisposeOp;
            public byte BlendOp;
        }

        private static class Crc32
        {
            private static readonly uint[] Table = CreateTable();

            public static uint Compute(byte[] data, int offset, int length)
            {
                return Compute(0xFFFFFFFF, data, offset, length) ^ 0xFFFFFFFF;
            }

            public static uint Compute(uint crc, byte[] data, int offset, int length)
            {
                if (data == null || length <= 0)
                    return crc;

                for (int i = 0; i < length; i++)
                    crc = Table[(crc ^ data[offset + i]) & 0xFF] ^ (crc >> 8);
                return crc;
            }

            private static uint[] CreateTable()
            {
                var table = new uint[256];
                for (uint i = 0; i < 256; i++)
                {
                    uint c = i;
                    for (int k = 0; k < 8; k++)
                        c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                    table[i] = c;
                }
                return table;
            }
        }
    }
}
