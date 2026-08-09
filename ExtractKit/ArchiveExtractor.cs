using System;
using System.IO;

namespace SmartGoldbergEmu.ExtractKit.Internal
{
    public static class ArchiveExtractor
    {
        // progressCallback(completedBytes, totalBytes, currentEntryFileName) — before each file,
        // during solid 7z decode, and once at 100%.
        public static void ExtractAll(
            string archivePath,
            string destinationDirectory,
            Action<long, long, string> progressCallback = null)
        {
            if (string.IsNullOrEmpty(archivePath))
                throw new ArgumentException("Archive path is required.", nameof(archivePath));
            if (string.IsNullOrEmpty(destinationDirectory))
                throw new ArgumentException("Destination directory is required.", nameof(destinationDirectory));

            ArchiveFormat format = ArchiveFormatSniffer.Detect(archivePath);
            if (format == ArchiveFormat.Unknown)
                throw new ExtractKitException("Unsupported or unrecognized archive format: " + archivePath);

            Directory.CreateDirectory(destinationDirectory);

            using (var reader = ArchiveReader.Open(archivePath))
            {
                long totalBytes = 0;
                for (int i = 0; i < reader.Entries.Count; i++)
                {
                    ArchiveEntry entry = reader.Entries[i];
                    if (!entry.IsDirectory && entry.Size > 0)
                        totalBytes += entry.Size;
                }

                // Fallback when sizes are missing: treat each file as weight 1.
                bool useFileCount = totalBytes <= 0;
                int totalFiles = 0;
                if (useFileCount)
                {
                    for (int i = 0; i < reader.Entries.Count; i++)
                    {
                        if (!reader.Entries[i].IsDirectory)
                            totalFiles++;
                    }
                    totalBytes = totalFiles;
                }

                long completedBytes = 0;
                for (int i = 0; i < reader.Entries.Count; i++)
                {
                    ArchiveEntry entry = reader.Entries[i];
                    if (entry.IsDirectory)
                        continue;

                    string displayName = Path.GetFileName(entry.Path.TrimEnd('/'));
                    long entryWeight = useFileCount ? 1 : Math.Max(0, entry.Size);
                    long baseCompleted = completedBytes;

                    progressCallback?.Invoke(baseCompleted, totalBytes, displayName);

                    Action<long, long> decodeProgress = null;
                    if (progressCallback != null && entryWeight > 0 && totalBytes > 0)
                    {
                        decodeProgress = (decoded, folderSize) =>
                        {
                            long remaining = totalBytes - baseCompleted;
                            long weight = entryWeight;
                            if (folderSize > entryWeight && remaining > entryWeight)
                                weight = Math.Min(remaining, folderSize);

                            long partial = ScaleBytes(weight, decoded, folderSize);
                            progressCallback(baseCompleted + partial, totalBytes, displayName);
                        };
                    }

                    reader.ExtractEntry(entry, destinationDirectory, flatFileName: false, decodeProgress);
                    completedBytes += entryWeight;
                    progressCallback?.Invoke(completedBytes, totalBytes, displayName);
                }

                if (totalBytes > 0)
                    progressCallback?.Invoke(totalBytes, totalBytes, null);
            }
        }

        public static void ExtractEntry(string archivePath, string entryPath, string destinationDirectory)
        {
            if (string.IsNullOrEmpty(archivePath))
                throw new ArgumentException("Archive path is required.", nameof(archivePath));
            if (string.IsNullOrEmpty(entryPath))
                throw new ArgumentException("Entry path is required.", nameof(entryPath));
            if (string.IsNullOrEmpty(destinationDirectory))
                throw new ArgumentException("Destination directory is required.", nameof(destinationDirectory));

            using (var reader = ArchiveReader.Open(archivePath))
            {
                if (!reader.TryGetEntry(entryPath, out ArchiveEntry entry))
                    throw new ExtractKitException("Entry not found in archive: " + entryPath);

                reader.ExtractEntry(entry, destinationDirectory, flatFileName: true);
            }
        }

        private static long ScaleBytes(long fileWeight, long decodedBytes, long folderUnpackBytes)
        {
            if (fileWeight <= 0)
                return 0;
            if (folderUnpackBytes <= 0)
                return fileWeight;
            if (decodedBytes >= folderUnpackBytes)
                return fileWeight;

            double ratio = decodedBytes / (double)folderUnpackBytes;
            long partial = (long)(fileWeight * ratio);
            if (partial < 0)
                return 0;
            if (partial > fileWeight)
                return fileWeight;
            return partial;
        }
    }
}
