using System;
using System.IO;
using SmartGoldbergEmu.ExtractKit.Internal;

namespace SmartGoldbergEmu.ExtractKit
{
    // Opens an update archive once and extracts individual files flat into a folder.
    public sealed class ArchiveExtractSession : IDisposable
    {
        private readonly ArchiveReader _reader;

        public ArchiveExtractSession(string archivePath)
        {
            if (string.IsNullOrWhiteSpace(archivePath))
                throw new ArgumentException("Archive path is required.", nameof(archivePath));

            string path = ArchivePath.RequireSupportedArchive(archivePath, nameof(archivePath));
            _reader = ArchiveReader.Open(path);
        }

        public void ExtractSingleFileFlat(string fileInArchive, string destinationFolder)
        {
            if (!TryExtractSingleFileFlat(fileInArchive, destinationFolder))
                throw new FileNotFoundException("Archive entry was not found: " + fileInArchive);
        }

        public bool TryExtractSingleFileFlat(string fileInArchive, string destinationFolder)
        {
            if (!_reader.TryGetEntry(fileInArchive, out ArchiveEntry entry))
                return false;

            _reader.ExtractEntry(entry, destinationFolder, flatFileName: true);
            return true;
        }

        public void Dispose()
        {
            _reader?.Dispose();
        }
    }
}
