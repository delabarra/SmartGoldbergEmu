namespace SmartGoldbergEmu.ExtractKit.Internal
{
    internal static class EntryPath
    {
        internal static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            string normalized = path.Replace('\\', '/');
            while (normalized.StartsWith("./", System.StringComparison.Ordinal))
                normalized = normalized.Substring(2);
            if (normalized.StartsWith("/", System.StringComparison.Ordinal))
                normalized = normalized.Substring(1);
            return normalized;
        }
    }
}
