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

        public bool TryGetEntryUncompressedSize(string fileInArchive, out long size)
        {
            size = 0;
            if (!_reader.TryGetEntry(fileInArchive, out ArchiveEntry entry) || entry.IsDirectory)
                return false;

            size = entry.Size;
            return true;
        }

        public void ExtractSingleFileFlat(string fileInArchive, string destinationFolder)
        {
            if (!TryExtractSingleFileFlat(fileInArchive, destinationFolder))
                throw new FileNotFoundException("Archive entry was not found: " + fileInArchive);
        }

        // decodeProgress(decodedBytes, folderUnpackBytes) fires while a 7z folder is decoded
        // (including BCJ2 inner streams mapped onto the folder unpack size).
        public bool TryExtractSingleFileFlat(
            string fileInArchive,
            string destinationFolder,
            Action<long, long> decodeProgress = null)
        {
            if (!_reader.TryGetEntry(fileInArchive, out ArchiveEntry entry))
                return false;

            _reader.ExtractEntry(entry, destinationFolder, flatFileName: true, decodeProgress);
            return true;
        }

        public void Dispose()
        {
            _reader?.Dispose();
        }
    }
}
