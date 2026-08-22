namespace SmartGoldbergEmu.ExtractKit.Internal
{
    public sealed class ArchiveEntry
    {
        internal ArchiveEntry(int index, string path, bool isDirectory, long size)
        {
            Index = index;
            Path = path;
            IsDirectory = isDirectory;
            Size = size;
        }

        public int Index { get; }
        public string Path { get; }
        public bool IsDirectory { get; }
        public long Size { get; }
    }
}
