using System;

namespace SmartGoldbergEmu.ExtractKit
{
    public sealed class UnsupportedArchiveFormatException : Exception
    {
        public UnsupportedArchiveFormatException(string path)
            : base("Only .7z and .zip update archives are supported: " + path)
        {
            Path = path;
        }

        public string Path { get; }
    }
}
