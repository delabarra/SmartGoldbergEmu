using System;
using System.Collections.Generic;
using System.IO;
using SmartGoldbergEmu.ExtractKit.Internal;

namespace SmartGoldbergEmu.ExtractKit.Internal
{
    public sealed class ArchiveReader : IDisposable
    {
        private readonly ArchiveFormat _format;
        private readonly string _archivePath;
        private FileStream _fileStream;
        private SevenZipArchiveCore _sevenZip;
        private ZipArchiveCore _zip;
        private BlockCache _blockCache;
        private List<ArchiveEntry> _entries;
        private Dictionary<string, ArchiveEntry> _entriesByPath;

        private ArchiveReader(string archivePath, ArchiveFormat format)
        {
            _archivePath = archivePath;
            _format = format;
        }

        public static ArchiveReader Open(string archivePath)
        {
            if (string.IsNullOrEmpty(archivePath))
                throw new ArgumentException("Archive path is required.", nameof(archivePath));

            ArchiveFormat format = ArchiveFormatSniffer.Detect(archivePath);
            if (format == ArchiveFormat.Unknown)
                throw new ExtractKitException("Unsupported or unrecognized archive format: " + archivePath);

            var reader = new ArchiveReader(archivePath, format);
            reader.OpenCore();
            return reader;
        }

        public IReadOnlyList<ArchiveEntry> Entries
        {
            get
            {
                EnsureOpen();
                return _entries;
            }
        }

        public bool TryGetEntry(string entryPath, out ArchiveEntry entry)
        {
            EnsureOpen();
            entry = null;
            if (string.IsNullOrEmpty(entryPath))
                return false;

            return _entriesByPath.TryGetValue(EntryPath.Normalize(entryPath), out entry);
        }

        public void ExtractEntry(ArchiveEntry entry, string destinationDirectory, bool flatFileName)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            if (string.IsNullOrEmpty(destinationDirectory))
                throw new ArgumentException("Destination directory is required.", nameof(destinationDirectory));

            EnsureOpen();
            Directory.CreateDirectory(destinationDirectory);

            if (entry.IsDirectory)
                return;

            string destPath = flatFileName
                ? Path.Combine(destinationDirectory, Path.GetFileName(entry.Path))
                : Path.Combine(destinationDirectory, entry.Path.Replace('/', Path.DirectorySeparatorChar));

            if (!flatFileName)
            {
                string destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir))
                    Directory.CreateDirectory(destDir);
            }

            ExtractEntryCore(entry.Index, destPath, flatFileName);
        }

        public void Dispose()
        {
            _sevenZip?.Close();
            _sevenZip = null;
            _zip = null;
            _blockCache = null;
            _entries = null;
            _entriesByPath = null;
            if (_fileStream != null)
            {
                _fileStream.Dispose();
                _fileStream = null;
            }
        }

        private void OpenCore()
        {
            _fileStream = File.Open(_archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            _entries = new List<ArchiveEntry>();

            if (_format == ArchiveFormat.SevenZip)
            {
                _sevenZip = new SevenZipArchiveCore();
                int res = _sevenZip.Open(_fileStream);
                if (res != SzRes.Ok)
                    throw new ExtractKitException("Failed to open 7z archive (code " + res + ").");

                _blockCache = new BlockCache();
                for (int i = 0; i < _sevenZip.FileCount; i++)
                {
                    string path = EntryPath.Normalize(_sevenZip.GetEntryPath(i));
                    bool isDir = path.EndsWith("/", StringComparison.Ordinal);
                    _entries.Add(new ArchiveEntry(i, path, isDir, 0));
                }

                BuildEntryIndex();
                return;
            }

            _zip = new ZipArchiveCore();
            _zip.Open(_fileStream);
            for (int i = 0; i < _zip.FileCount; i++)
            {
                string path = EntryPath.Normalize(_zip.GetEntryPath(i));
                bool isDir = path.EndsWith("/", StringComparison.Ordinal);
                _entries.Add(new ArchiveEntry(i, path, isDir, 0));
            }

            BuildEntryIndex();
        }

        private void BuildEntryIndex()
        {
            _entriesByPath = new Dictionary<string, ArchiveEntry>(_entries.Count, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _entries.Count; i++)
            {
                ArchiveEntry item = _entries[i];
                if (item.IsDirectory || _entriesByPath.ContainsKey(item.Path))
                    continue;
                _entriesByPath.Add(item.Path, item);
            }
        }

        private void ExtractEntryCore(int index, string destPath, bool flatFileName)
        {
            if (_format == ArchiveFormat.SevenZip)
            {
                string destDir = flatFileName ? Path.GetDirectoryName(destPath) : null;
                if (flatFileName && !string.IsNullOrEmpty(destDir))
                    Directory.CreateDirectory(destDir);

                int offset;
                int outSize;
                int res = _sevenZip.ExtractFile(index, _blockCache, out offset, out outSize);
                if (res != SzRes.Ok)
                    throw new ExtractKitException("7z extraction failed (code " + res + ").");

                using (var fs = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    if (outSize > 0)
                        fs.Write(_blockCache.Buffer, offset, outSize);
                }
                return;
            }

            _zip.ExtractEntryToFile(index, destPath);
        }

        private void EnsureOpen()
        {
            if (_fileStream == null)
                throw new ObjectDisposedException(nameof(ArchiveReader));
        }

        internal static string NormalizeEntryPath(string path) => EntryPath.Normalize(path);
    }
}
