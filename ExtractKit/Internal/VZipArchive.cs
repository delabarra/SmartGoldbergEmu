using System;
using System.IO;
using System.IO.Compression;

namespace SmartGoldbergEmu.ExtractKit.Internal
{
    internal static class VZipArchive
    {
        public static byte[] Decompress(byte[] data)
        {
            if (data == null || data.Length < 18)
                throw new ExtractKitException("Invalid VZip payload.");
            if (data[0] != (byte)'V' || data[1] != (byte)'Z' || data[2] != (byte)'a')
                throw new ExtractKitException("Unsupported VZip header.");
            if (data[data.Length - 2] != (byte)'z' || data[data.Length - 1] != (byte)'v')
                throw new ExtractKitException("Invalid VZip footer.");

            const int headerSize = 7;
            const int propsSize = 5;
            const int footerSize = 10;

            uint expectedCrc = BitConverter.ToUInt32(data, data.Length - footerSize);
            uint expectedSize = BitConverter.ToUInt32(data, data.Length - footerSize + 4);
            int compressedOffset = headerSize + propsSize;
            int compressedLength = data.Length - compressedOffset - footerSize;
            if (compressedLength <= 0)
                throw new ExtractKitException("Invalid VZip payload size.");
            if (expectedSize > int.MaxValue)
                throw new ExtractKitException("VZip output is too large.");

            var props = new byte[propsSize];
            Buffer.BlockCopy(data, headerSize, props, 0, propsSize);
            var compressed = new byte[compressedLength];
            Buffer.BlockCopy(data, compressedOffset, compressed, 0, compressedLength);

            var output = new byte[(int)expectedSize];
            int destLen = output.Length;
            int srcLen = compressed.Length;
            ELzmaStatus status;
            int res = LzmaDec.LzmaDecode(
                output,
                ref destLen,
                compressed,
                0,
                ref srcLen,
                props,
                0,
                (uint)propsSize,
                ELzmaFinishMode.LzmaFinishEnd,
                out status,
                SzAlloc.Instance);

            if (res != SzRes.Ok)
                throw new ExtractKitException("Failed to decompress VZip package.");
            if (destLen != output.Length)
                throw new ExtractKitException("Decompressed VZip size mismatch.");

            uint actualCrc = ComputeCrc32(output);
            if (actualCrc != expectedCrc)
                throw new ExtractKitException("Decompressed VZip CRC mismatch.");

            return output;
        }

        public static byte[] ExtractEntry(byte[] data, string entryPath)
        {
            if (string.IsNullOrWhiteSpace(entryPath))
                throw new ArgumentException("Entry path is required.", nameof(entryPath));

            byte[] zipBytes = Decompress(data);
            using (var stream = new MemoryStream(zipBytes))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false))
            {
                var entry = zip.GetEntry(entryPath);
                if (entry == null)
                    throw new ExtractKitException("Entry not found in VZip archive: " + entryPath);
                using (var entryStream = entry.Open())
                using (var ms = new MemoryStream())
                {
                    entryStream.CopyTo(ms);
                    return ms.ToArray();
                }
            }
        }

        private static uint ComputeCrc32(byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            for (int i = 0; i < data.Length; i++)
            {
                uint index = (crc ^ data[i]) & 0xFF;
                crc = (crc >> 8) ^ Crc32Table[index];
            }
            return crc ^ 0xFFFFFFFF;
        }

        private static readonly uint[] Crc32Table = CreateCrc32Table();

        private static uint[] CreateCrc32Table()
        {
            const uint poly = 0xEDB88320;
            var table = new uint[256];
            for (uint i = 0; i < table.Length; i++)
            {
                uint c = i;
                for (int j = 0; j < 8; j++)
                    c = (c & 1) != 0 ? poly ^ (c >> 1) : c >> 1;
                table[i] = c;
            }
            return table;
        }
    }
}
