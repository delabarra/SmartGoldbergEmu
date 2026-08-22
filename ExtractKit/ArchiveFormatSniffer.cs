using System;
using System.IO;

namespace SmartGoldbergEmu.ExtractKit.Internal
{
    internal enum ArchiveFormat
    {
        Unknown,
        Zip,
        SevenZip
    }

    internal static class ArchiveFormatSniffer
    {
        private static readonly byte[] SevenZipSignature = { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C };

        public static ArchiveFormat Detect(string archivePath)
        {
            if (string.IsNullOrEmpty(archivePath))
                throw new ArgumentException("Archive path is required.", nameof(archivePath));
            if (!File.Exists(archivePath))
                throw new FileNotFoundException("Archive not found.", archivePath);

            using (var stream = File.OpenRead(archivePath))
                return Detect(stream);
        }

        public static ArchiveFormat Detect(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead)
                throw new ArgumentException("Stream must be readable.", nameof(stream));

            long position = stream.CanSeek ? stream.Position : 0;
            try
            {
                byte[] header = new byte[6];
                int read = stream.Read(header, 0, header.Length);
                if (read >= 2 && header[0] == 0x50 && header[1] == 0x4B)
                    return ArchiveFormat.Zip;
                if (read >= Six && Matches(header, SevenZipSignature))
                    return ArchiveFormat.SevenZip;
                return ArchiveFormat.Unknown;
            }
            finally
            {
                if (stream.CanSeek)
                    stream.Position = position;
            }
        }

        private static bool Matches(byte[] buffer, byte[] signature)
        {
            if (buffer.Length < signature.Length)
                return false;
            for (int i = 0; i < signature.Length; i++)
            {
                if (buffer[i] != signature[i])
                    return false;
            }
            return true;
        }

        private const int Six = 6;
    }
}
