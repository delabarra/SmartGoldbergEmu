using System;
using System.IO;

namespace SmartGoldbergEmu.ExtractKit.Internal
{
    public static class ArchiveExtractor
    {
        public static void ExtractAll(string archivePath, string destinationDirectory)
        {
            if (string.IsNullOrEmpty(archivePath))
                throw new ArgumentException("Archive path is required.", nameof(archivePath));
            if (string.IsNullOrEmpty(destinationDirectory))
                throw new ArgumentException("Destination directory is required.", nameof(destinationDirectory));

            ArchiveFormat format = ArchiveFormatSniffer.Detect(archivePath);
            if (format == ArchiveFormat.Unknown)
                throw new ExtractKitException("Unsupported or unrecognized archive format: " + archivePath);

            Directory.CreateDirectory(destinationDirectory);

            if (format == ArchiveFormat.Zip)
            {
                using (var stream = File.OpenRead(archivePath))
                {
                    var zip = new Internal.ZipArchiveCore();
                    zip.Open(stream);
                    zip.ExtractAll(destinationDirectory);
                }

                return;
            }

            using (var reader = ArchiveReader.Open(archivePath))
            {
                foreach (ArchiveEntry entry in reader.Entries)
                {
                    if (entry.IsDirectory)
                        continue;
                    reader.ExtractEntry(entry, destinationDirectory, flatFileName: false);
                }
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
    }
}
