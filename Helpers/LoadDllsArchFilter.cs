using System;
using System.IO;

namespace SmartGoldbergEmu.Helpers
{
    // Filename rules for steam_settings/load_dlls (x64 / x32 / any).
    public static class LoadDllsArchFilter
    {
        public const string ArchMarkerX64 = "x64";
        public const string ArchMarkerX32 = "x32";

        public static bool MatchesProcessArchitecture(string dllFileName, bool useX64)
        {
            if (string.IsNullOrWhiteSpace(dllFileName))
                return false;

            string baseName = Path.GetFileNameWithoutExtension(dllFileName);
            if (baseName.Length == 0)
                return false;

            bool hasX64 = baseName.IndexOf(ArchMarkerX64, StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasX32 = baseName.IndexOf(ArchMarkerX32, StringComparison.OrdinalIgnoreCase) >= 0;

            if (hasX64 && !hasX32)
                return useX64;

            if (hasX32 && !hasX64)
                return !useX64;

            return true;
        }
    }
}
