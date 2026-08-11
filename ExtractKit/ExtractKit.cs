using System;
using System.Collections.Generic;
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

        // progressCallback(completedBytes, totalBytes, currentEntryFileName) — optional.
        // cancellationCheck aborts between entries and during solid 7z decode (OperationCanceledException).
        public static void ExtractAll(
            string archivePath,
            string outputDirectory,
            Action<long, long, string> progressCallback = null,
            Func<bool> cancellationCheck = null)
        {
            string path = ArchivePath.RequireSupportedArchive(archivePath, nameof(archivePath));
            if (string.IsNullOrWhiteSpace(outputDirectory))
                throw new ArgumentException("Output directory is required.", nameof(outputDirectory));

            Directory.CreateDirectory(outputDirectory);
            ArchiveExtractor.ExtractAll(path, outputDirectory, progressCallback, cancellationCheck);
        }

        public static byte[] DecompressVzip(byte[] vzipData)
        {
            return VZipArchive.Decompress(vzipData);
        }

        public static byte[] ExtractVzipEntry(byte[] vzipData, string entryPath)
        {
            return VZipArchive.ExtractEntry(vzipData, entryPath);
        }

        // Stream CDN packages from disk: decompress once, write named entries to destinations.
        public static void ExtractVzipEntriesToFiles(
            string vzipFilePath,
            IReadOnlyList<KeyValuePair<string, string>> entryPathToDestFile)
        {
            VZipArchive.ExtractEntriesToFilesFromPath(vzipFilePath, entryPathToDestFile);
        }
    }
}
