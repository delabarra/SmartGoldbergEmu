using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using Xunit;

namespace SmartGoldbergEmu.Tests.Helpers
{
    public sealed class ApngAnimationDecoderTests
    {
        [Fact]
        public void TryDecode_partial_source_frame_updates_canvas_not_stale_first_frame()
        {
            byte[] apng = BuildTwoFrameApng(
                canvasSize: 8,
                firstColor: Color.FromArgb(255, 220, 40, 40),
                overlaySize: 4,
                overlayX: 2,
                overlayY: 2,
                overlayColor: Color.FromArgb(255, 30, 80, 220),
                delayNum: 10,
                delayDen: 100);

            ApngAnimationDecoder.FrameBitmap[] frames = null;
            try
            {
                Assert.True(ApngAnimationDecoder.TryDecode(apng, out frames));
                Assert.NotNull(frames);
                Assert.Equal(2, frames.Length);

                Assert.Equal(8, frames[0].Bitmap.Width);
                Assert.Equal(8, frames[0].Bitmap.Height);
                AssertEqualColor(Color.FromArgb(255, 220, 40, 40), frames[0].Bitmap.GetPixel(0, 0));
                AssertEqualColor(Color.FromArgb(255, 220, 40, 40), frames[0].Bitmap.GetPixel(3, 3));

                AssertEqualColor(Color.FromArgb(255, 220, 40, 40), frames[1].Bitmap.GetPixel(0, 0));
                AssertEqualColor(Color.FromArgb(255, 30, 80, 220), frames[1].Bitmap.GetPixel(3, 3));
                Assert.True(frames[0].DelayMilliseconds >= 10);
            }
            finally
            {
                DisposeFrames(frames);
            }
        }

        [Fact]
        public void TryDecode_rejects_non_png()
        {
            ApngAnimationDecoder.FrameBitmap[] frames;
            Assert.False(ApngAnimationDecoder.TryDecode(Encoding.ASCII.GetBytes("not a png"), out frames));
            Assert.Null(frames);
        }

        [Fact]
        public void TryDecode_local_clientui_spinner_later_frames_differ_when_present()
        {
            string path = PathConstants.LocalAppDataSteamClientUiHashedImagePath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            ApngAnimationDecoder.FrameBitmap[] frames = null;
            try
            {
                Assert.True(ApngAnimationDecoder.TryDecode(path, out frames));
                Assert.NotNull(frames);
                Assert.True(frames.Length >= 2);

                bool foundDifference = false;
                Bitmap first = frames[0].Bitmap;
                for (int i = 1; i < frames.Length && !foundDifference; i++)
                {
                    Bitmap next = frames[i].Bitmap;
                    if (next.Width != first.Width || next.Height != first.Height)
                    {
                        foundDifference = true;
                        break;
                    }

                    for (int y = 0; y < first.Height && !foundDifference; y += 7)
                    {
                        for (int x = 0; x < first.Width && !foundDifference; x += 7)
                        {
                            if (first.GetPixel(x, y) != next.GetPixel(x, y))
                                foundDifference = true;
                        }
                    }
                }

                Assert.True(foundDifference, "Decoded APNG frames were identical; animation would look frozen.");
            }
            finally
            {
                DisposeFrames(frames);
            }
        }

        private static void AssertEqualColor(Color expected, Color actual)
        {
            Assert.True(
                expected.A == actual.A && expected.R == actual.R && expected.G == actual.G && expected.B == actual.B,
                "Expected " + expected + " but was " + actual);
        }

        private static void DisposeFrames(ApngAnimationDecoder.FrameBitmap[] frames)
        {
            if (frames == null)
                return;
            for (int i = 0; i < frames.Length; i++)
                frames[i]?.Dispose();
        }

        private static byte[] BuildTwoFrameApng(
            int canvasSize,
            Color firstColor,
            int overlaySize,
            int overlayX,
            int overlayY,
            Color overlayColor,
            ushort delayNum,
            ushort delayDen)
        {
            using (var first = SolidBitmap(canvasSize, canvasSize, firstColor))
            using (var overlay = SolidBitmap(overlaySize, overlaySize, overlayColor))
            {
                byte[] firstPng = SavePng(first);
                byte[] overlayPng = SavePng(overlay);
                var firstChunks = ReadChunks(firstPng);
                var overlayChunks = ReadChunks(overlayPng);

                PngChunk ihdr = FindChunk(firstChunks, "IHDR");
                PngChunk overlayIhdr = FindChunk(overlayChunks, "IHDR");
                Assert.Equal(ihdr.Data[8], overlayIhdr.Data[8]);
                Assert.Equal(ihdr.Data[9], overlayIhdr.Data[9]);

                using (var ms = new MemoryStream())
                {
                    ms.Write(PngSignature, 0, PngSignature.Length);
                    WriteChunk(ms, ihdr.Type, ihdr.Data);

                    foreach (var chunk in firstChunks)
                    {
                        if (IsCopyThroughChunk(chunk.Type))
                            WriteChunk(ms, chunk.Type, chunk.Data);
                    }

                    WriteChunk(ms, "acTL", BuildAcTl(numFrames: 2, numPlays: 0));
                    int seq = 0;
                    WriteChunk(ms, "fcTL", BuildFcTl(seq++, canvasSize, canvasSize, 0, 0, delayNum, delayDen, disposeOp: 0, blendOp: 0));
                    foreach (var chunk in firstChunks)
                    {
                        if (chunk.Type == "IDAT")
                            WriteChunk(ms, "IDAT", chunk.Data);
                    }

                    WriteChunk(ms, "fcTL", BuildFcTl(seq++, overlaySize, overlaySize, overlayX, overlayY, delayNum, delayDen, disposeOp: 0, blendOp: 0));
                    foreach (var chunk in overlayChunks)
                    {
                        if (chunk.Type != "IDAT")
                            continue;
                        var fdat = new byte[4 + chunk.Data.Length];
                        WriteInt32Be(fdat, 0, seq++);
                        Buffer.BlockCopy(chunk.Data, 0, fdat, 4, chunk.Data.Length);
                        WriteChunk(ms, "fdAT", fdat);
                    }

                    WriteChunk(ms, "IEND", Array.Empty<byte>());
                    return ms.ToArray();
                }
            }
        }

        private static Bitmap SolidBitmap(int width, int height, Color color)
        {
            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                graphics.Clear(color);
            }
            return bitmap;
        }

        private static byte[] SavePng(Bitmap bitmap)
        {
            using (var ms = new MemoryStream())
            {
                bitmap.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }

        private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

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

        private static PngChunk FindChunk(List<PngChunk> chunks, string type)
        {
            for (int i = 0; i < chunks.Count; i++)
            {
                if (chunks[i].Type == type)
                    return chunks[i];
            }
            throw new InvalidOperationException("Missing PNG chunk " + type);
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
                string type = Encoding.ASCII.GetString(fileBytes, offset + 4, 4);
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

        private static byte[] BuildAcTl(int numFrames, int numPlays)
        {
            var data = new byte[8];
            WriteInt32Be(data, 0, numFrames);
            WriteInt32Be(data, 4, numPlays);
            return data;
        }

        private static byte[] BuildFcTl(
            int sequence,
            int width,
            int height,
            int xOffset,
            int yOffset,
            ushort delayNum,
            ushort delayDen,
            byte disposeOp,
            byte blendOp)
        {
            var data = new byte[26];
            WriteInt32Be(data, 0, sequence);
            WriteInt32Be(data, 4, width);
            WriteInt32Be(data, 8, height);
            WriteInt32Be(data, 12, xOffset);
            WriteInt32Be(data, 16, yOffset);
            data[20] = (byte)(delayNum >> 8);
            data[21] = (byte)delayNum;
            data[22] = (byte)(delayDen >> 8);
            data[23] = (byte)delayDen;
            data[24] = disposeOp;
            data[25] = blendOp;
            return data;
        }

        private static void WriteChunk(Stream stream, string type, byte[] data)
        {
            byte[] typeBytes = Encoding.ASCII.GetBytes(type);
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
