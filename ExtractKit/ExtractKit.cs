using System;
using System.IO;
using SmartGoldbergEmu.ExtractKit.Internal;

namespace SmartGoldbergEmu.ExtractKit
{
    // Decompress-only .zip / .7z support for launcher and Goldberg updates.
    public static class ExtractKit
    {
        public static bool IsSupportedPath(string path)
        {
            return ArchivePath.IsSupportedPath(path);
        }

        public static void ExtractAll(string archivePath, string outputDirectory)
        {
            string path = ArchivePath.RequireSupportedArchive(archivePath, nameof(archivePath));
            if (string.IsNullOrWhiteSpace(outputDirectory))
                throw new ArgumentException("Output directory is required.", nameof(outputDirectory));

            Directory.CreateDirectory(outputDirectory);
            ArchiveExtractor.ExtractAll(path, outputDirectory);
        }

        public static byte[] DecompressVzip(byte[] vzipData)
        {
            return VZipArchive.Decompress(vzipData);
        }

        public static byte[] ExtractVzipEntry(byte[] vzipData, string entryPath)
        {
            return VZipArchive.ExtractEntry(vzipData, entryPath);
        }
    }
}
