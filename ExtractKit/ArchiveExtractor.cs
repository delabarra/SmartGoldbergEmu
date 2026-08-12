using System;
using System.IO;

namespace SmartGoldbergEmu.ExtractKit.Internal
{
    public static class ArchiveExtractor
    {
        // progressCallback(completedBytes, totalBytes, currentEntryFileName) — before each file,
        // during solid 7z decode, and once at 100%.
        // cancellationCheck may throw OperationCanceledException when true mid-decode.
        public static void ExtractAll(
            string archivePath,
            string destinationDirectory,
            Action<long, long, string> progressCallback = null,
            Func<bool> cancellationCheck = null)
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
                    ThrowIfCancelled(cancellationCheck);

                    ArchiveEntry entry = reader.Entries[i];
                    if (entry.IsDirectory)
                        continue;

                    string displayName = Path.GetFileName(entry.Path.TrimEnd('/'));
                    long entryWeight = useFileCount ? 1 : Math.Max(0, entry.Size);
                    long baseCompleted = completedBytes;

                    progressCallback?.Invoke(baseCompleted, totalBytes, displayName);

                    long lastFolderSize = 0;
                    Action<long, long> decodeProgress = null;
                    if ((progressCallback != null || cancellationCheck != null) && entryWeight > 0 && totalBytes > 0)
                    {
                        decodeProgress = (decoded, folderSize) =>
                        {
                            ThrowIfCancelled(cancellationCheck);

                            lastFolderSize = folderSize;
                            if (progressCallback == null)
                                return;

                            long remaining = totalBytes - baseCompleted;
                            long weight = ArchiveExtractProgress.DecodeWeight(entryWeight, folderSize, remaining);
                            long partial = ArchiveExtractProgress.ScaleBytes(weight, decoded, folderSize);
                            progressCallback(baseCompleted + partial, totalBytes, displayName);
                        };
                    }

                    reader.ExtractEntry(entry, destinationDirectory, flatFileName: false, decodeProgress);
                    long remainingAfter = totalBytes - baseCompleted;
                    if (remainingAfter < 0)
                        remainingAfter = 0;
                    completedBytes = baseCompleted + ArchiveExtractProgress.CompletedDelta(
                        entryWeight, lastFolderSize, remainingAfter);
                    progressCallback?.Invoke(completedBytes, totalBytes, displayName);
                }

                ThrowIfCancelled(cancellationCheck);

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

        private static void ThrowIfCancelled(Func<bool> cancellationCheck)
        {
            if (cancellationCheck != null && cancellationCheck())
                throw new OperationCanceledException("Archive extraction was cancelled.");
        }
    }
}
