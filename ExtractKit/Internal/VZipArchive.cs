using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace SmartGoldbergEmu.ExtractKit.Internal
{
    internal static class VZipArchive
    {
        private const int HeaderSize = 7;
        private const int PropsSize = 5;
        private const int FooterSize = 10;
        private const int CopyBufferSize = 65536;

        public static byte[] Decompress(byte[] data)
        {
            ParseHeader(data, out byte[] props, out int compressedOffset, out int compressedLength, out uint expectedCrc, out uint expectedSize);

            var output = new byte[(int)expectedSize];
            DecodeLzma(data, props, compressedOffset, compressedLength, output, expectedCrc);
            return output;
        }

        // Decompress VZip to a temp .zip on disk, then extract named entries with a bounded copy buffer.
        public static void ExtractEntriesToFiles(byte[] vzipData, IReadOnlyList<KeyValuePair<string, string>> entryPathToDestFile)
        {
            if (entryPathToDestFile == null || entryPathToDestFile.Count == 0)
                throw new ArgumentException("At least one entry mapping is required.", nameof(entryPathToDestFile));

            byte[] zipBytes = Decompress(vzipData);
            WriteZipAndExtractEntries(zipBytes, entryPathToDestFile);
        }

        public static void ExtractEntriesToFilesFromPath(
            string vzipFilePath,
            IReadOnlyList<KeyValuePair<string, string>> entryPathToDestFile)
        {
            if (string.IsNullOrWhiteSpace(vzipFilePath))
                throw new ArgumentException("VZip path is required.", nameof(vzipFilePath));
            if (!File.Exists(vzipFilePath))
                throw new FileNotFoundException("VZip package not found.", vzipFilePath);
            if (entryPathToDestFile == null || entryPathToDestFile.Count == 0)
                throw new ArgumentException("At least one entry mapping is required.", nameof(entryPathToDestFile));

            byte[] data = File.ReadAllBytes(vzipFilePath);
            byte[] zipBytes;
            try
            {
                zipBytes = Decompress(data);
            }
            finally
            {
                // Drop compressed package before holding the unzipped ZIP so peaks do not stack.
                data = null;
            }

            WriteZipAndExtractEntries(zipBytes, entryPathToDestFile);
        }

        private static void WriteZipAndExtractEntries(
            byte[] zipBytes,
            IReadOnlyList<KeyValuePair<string, string>> entryPathToDestFile)
        {
            if (zipBytes == null)
                throw new ExtractKitException("VZip decompress produced no output.");

            string tempZipPath = Path.Combine(
                Path.GetTempPath(),
                "sge-vzip-" + Guid.NewGuid().ToString("N") + ".zip");

            try
            {
                File.WriteAllBytes(tempZipPath, zipBytes);
                zipBytes = null;

                using (var stream = new FileStream(tempZipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false))
                {
                    var buffer = new byte[CopyBufferSize];
                    for (int i = 0; i < entryPathToDestFile.Count; i++)
                    {
                        string entryPath = entryPathToDestFile[i].Key;
                        string destPath = entryPathToDestFile[i].Value;
                        if (string.IsNullOrWhiteSpace(entryPath))
                            throw new ArgumentException("Entry path is required.");
                        if (string.IsNullOrWhiteSpace(destPath))
                            throw new ArgumentException("Destination path is required.");

                        ZipArchiveEntry entry = zip.GetEntry(entryPath);
                        if (entry == null)
                            throw new ExtractKitException("Entry not found in VZip archive: " + entryPath);

                        string destDir = Path.GetDirectoryName(destPath);
                        if (!string.IsNullOrEmpty(destDir))
                            Directory.CreateDirectory(destDir);

                        using (Stream entryStream = entry.Open())
                        using (var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            int read;
                            while ((read = entryStream.Read(buffer, 0, buffer.Length)) > 0)
                                fileStream.Write(buffer, 0, read);
                        }
                    }
                }
            }
            finally
            {
                zipBytes = null;
                try
                {
                    if (File.Exists(tempZipPath))
                        File.Delete(tempZipPath);
                }
                catch
                {
                }
            }
        }

        public static byte[] ExtractEntry(byte[] data, string entryPath)
        {
            if (string.IsNullOrWhiteSpace(entryPath))
                throw new ArgumentException("Entry path is required.", nameof(entryPath));

            string tempDir = Path.Combine(Path.GetTempPath(), "sge-vzip-entry-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string destPath = Path.Combine(tempDir, "entry.bin");
            try
            {
                ExtractEntriesToFiles(
                    data,
                    new[] { new KeyValuePair<string, string>(entryPath, destPath) });
                return File.ReadAllBytes(destPath);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, recursive: true);
                }
                catch
                {
                }
            }
        }

        private static void ParseHeader(
            byte[] data,
            out byte[] props,
            out int compressedOffset,
            out int compressedLength,
            out uint expectedCrc,
            out uint expectedSize)
        {
            if (data == null || data.Length < 18)
                throw new ExtractKitException("Invalid VZip payload.");
            if (data[0] != (byte)'V' || data[1] != (byte)'Z' || data[2] != (byte)'a')
                throw new ExtractKitException("Unsupported VZip header.");
            if (data[data.Length - 2] != (byte)'z' || data[data.Length - 1] != (byte)'v')
                throw new ExtractKitException("Invalid VZip footer.");

            expectedCrc = BitConverter.ToUInt32(data, data.Length - FooterSize);
            expectedSize = BitConverter.ToUInt32(data, data.Length - FooterSize + 4);
            compressedOffset = HeaderSize + PropsSize;
            compressedLength = data.Length - compressedOffset - FooterSize;
            if (compressedLength <= 0)
                throw new ExtractKitException("Invalid VZip payload size.");
            if (expectedSize > int.MaxValue)
                throw new ExtractKitException("VZip output is too large.");

            props = new byte[PropsSize];
            Buffer.BlockCopy(data, HeaderSize, props, 0, PropsSize);
        }

        private static void DecodeLzma(
            byte[] data,
            byte[] props,
            int compressedOffset,
            int compressedLength,
            byte[] output,
            uint expectedCrc)
        {
            int destLen = output.Length;
            int srcLen = compressedLength;
            ELzmaStatus status;
            // Decode in place from the VZip buffer (no second full compressed copy).
            int res = LzmaDec.LzmaDecode(
                output,
                ref destLen,
                data,
                compressedOffset,
                ref srcLen,
                props,
                0,
                (uint)PropsSize,
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
