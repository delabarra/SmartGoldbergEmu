using System.Collections.Generic;

namespace SmartGoldbergEmu.Constants
{
    // Steamworks Common Redistributables (app 228980) depot ids → name + installscript under Steamworks Shared.
    public static class SteamCommonRedistributableConstants
    {
        public const uint AppId = 228980;

        public const string InstallScriptFileName = "installscript.vdf";
        public const string RunAsAdminScriptFileName = "runasadmin.vdf";

        // HasRun markers when a game script omits HasRunKey (Steam default).
        public const string DefaultHasRunKeyFormat =
            @"HKEY_LOCAL_MACHINE\Software\Valve\Steam\Apps\{0}";

        // SGE mirror when HKLM write is denied (UAC). Checked before re-running.
        public const string SgeHasRunRegistryRoot =
            @"Software\SmartGoldbergEmu\SteamInstallScripts";

        // SteamRE DepotDownloader bootstrap (anonymous download of free app 228980 depots).
        public const string DepotDownloaderVersionTag = "DepotDownloader_3.4.0";
        public const string DepotDownloaderWindowsX64ZipFileName = "DepotDownloader-windows-x64.zip";
        public const string DepotDownloaderExecutableFileName = "DepotDownloader.exe";
        public const string DepotDownloaderToolsFolderName = "DepotDownloader";

        public static string DepotDownloaderWindowsX64DownloadUrl =>
            "https://github.com/SteamRE/DepotDownloader/releases/download/"
            + DepotDownloaderVersionTag
            + "/"
            + DepotDownloaderWindowsX64ZipFileName;

        public sealed class DepotInfo
        {
            public DepotInfo(string displayName, string relativeInstallScriptPath)
            {
                DisplayName = displayName;
                RelativeInstallScriptPath = relativeInstallScriptPath;
            }

            public string DisplayName { get; }
            public string RelativeInstallScriptPath { get; }
        }

        // Names match SteamDB / docs/design/steam-common-redistributables.md.
        public static readonly IReadOnlyDictionary<uint, DepotInfo> Depots =
            new Dictionary<uint, DepotInfo>
            {
                { 228981, new DepotInfo("Visual C++ 2005 Redistributable", @"_CommonRedist\vcredist\2005\installscript.vdf") },
                { 228982, new DepotInfo("Visual C++ 2008 Redistributable", @"_CommonRedist\vcredist\2008\installscript.vdf") },
                { 228983, new DepotInfo("Visual C++ 2010 Redistributable", @"_CommonRedist\vcredist\2010\installscript.vdf") },
                { 228984, new DepotInfo("Visual C++ 2012 Redistributable", @"_CommonRedist\vcredist\2012\installscript.vdf") },
                { 228985, new DepotInfo("Visual C++ 2013 Redistributable", @"_CommonRedist\vcredist\2013\installscript.vdf") },
                { 228986, new DepotInfo("Visual C++ 2015 Redistributable", @"_CommonRedist\vcredist\2015\installscript.vdf") },
                { 228987, new DepotInfo("Visual C++ 2017 Redistributable", @"_CommonRedist\vcredist\2017\installscript.vdf") },
                { 228988, new DepotInfo("Visual C++ 2019 Redistributable", @"_CommonRedist\vcredist\2019\installscript.vdf") },
                { 228989, new DepotInfo("Visual C++ 2022 Redistributable", @"_CommonRedist\vcredist\2022\installscript.vdf") },
                { 228990, new DepotInfo("DirectX End-User Runtime (June 2010)", @"_CommonRedist\DirectX\Jun2010\installscript.vdf") },
                { 229000, new DepotInfo("Microsoft .NET Framework 3.5", @"_CommonRedist\DotNet\3.5\installscript.vdf") },
                { 229001, new DepotInfo("Microsoft .NET Framework 3.5 Client Profile", @"_CommonRedist\DotNet\3.5 Client Profile\installscript.vdf") },
                { 229002, new DepotInfo("Microsoft .NET Framework 4.0", @"_CommonRedist\DotNet\4.0\installscript.vdf") },
                { 229003, new DepotInfo("Microsoft .NET Framework 4.0 Client Profile", @"_CommonRedist\DotNet\4.0 Client Profile\installscript.vdf") },
                { 229004, new DepotInfo("Microsoft .NET Framework 4.5.2", @"_CommonRedist\DotNet\4.5.2\installscript.vdf") },
                { 229005, new DepotInfo("Microsoft .NET Framework 4.6", @"_CommonRedist\DotNet\4.6\installscript.vdf") },
                { 229006, new DepotInfo("Microsoft .NET Framework 4.7", @"_CommonRedist\DotNet\4.7\installscript.vdf") },
                { 229007, new DepotInfo("Microsoft .NET Framework 4.8", @"_CommonRedist\DotNet\4.8\installscript.vdf") },
                { 229010, new DepotInfo("Microsoft XNA Framework 3.0", @"_CommonRedist\XNA\3.0\installscript.vdf") },
                { 229011, new DepotInfo("Microsoft XNA Framework 3.1", @"_CommonRedist\XNA\3.1\installscript.vdf") },
                { 229012, new DepotInfo("Microsoft XNA Framework 4.0", @"_CommonRedist\XNA\4.0\installscript.vdf") },
                { 229020, new DepotInfo("OpenAL 2.0.7.0", @"_CommonRedist\OpenAL\2.0.7.0\installscript.vdf") },
                { 229030, new DepotInfo("NVIDIA PhysX 8.09.04", @"_CommonRedist\PhysX\8.09.04\installscript.vdf") },
                { 229031, new DepotInfo("NVIDIA PhysX 9.12.1031", @"_CommonRedist\PhysX\9.12.1031\installscript.vdf") },
                { 229032, new DepotInfo("NVIDIA PhysX 9.13.1220", @"_CommonRedist\PhysX\9.13.1220\installscript.vdf") },
                { 229033, new DepotInfo("NVIDIA PhysX 9.14.0702", @"_CommonRedist\PhysX\9.14.0702\installscript.vdf") },
            };

        // Compatibility alias for call sites that only need the script path.
        public static readonly IReadOnlyDictionary<uint, string> DepotInstallScriptRelativePaths =
            BuildPathMap();

        public static bool TryGetDepot(uint depotId, out DepotInfo info) =>
            Depots.TryGetValue(depotId, out info);

        public static string GetDepotDisplayName(uint depotId)
        {
            DepotInfo info;
            if (Depots.TryGetValue(depotId, out info) && !string.IsNullOrWhiteSpace(info.DisplayName))
                return info.DisplayName;
            return "Steam redistributable " + depotId;
        }

        private static IReadOnlyDictionary<uint, string> BuildPathMap()
        {
            var map = new Dictionary<uint, string>(Depots.Count);
            foreach (KeyValuePair<uint, DepotInfo> pair in Depots)
                map[pair.Key] = pair.Value.RelativeInstallScriptPath;
            return map;
        }
    }
}
