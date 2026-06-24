using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SmartGoldbergEmu.Constants
{
    // Maps emu-win-release.7z paths under release/ to install paths under goldberg\.
    // Fork archives use experimental/x32 and experimental/x64; we install all DLLs flat under experimental\.
    public static class GoldbergInstallLayout
    {
        public const string ArchiveReleasePrefix = "release/";

        public const string ExperimentalFolderName = "experimental";
        public const string ExperimentalX32ArchiveFolderName = "x32";
        public const string SteamClientExperimentalFolderName = "steamclient_experimental";
        public const string SteamOldFolderName = "steam_old";
        public const string SteamClientExtraDllsFolderName = "steamclient_extra_dlls";
        public const string GoldbergReadmeFileName = "readme.txt";

        private static readonly GoldbergInstallFile[] ReleaseFiles =
        {
            new GoldbergInstallFile(
                "experimental/x32/steam_api.dll",
                PathConstants.GoldbergStandardSteamApiDll32,
                ExperimentalFolderName),
            new GoldbergInstallFile(
                "experimental/x32/steamclient.dll",
                PathConstants.GoldbergSteamClientDll32,
                ExperimentalFolderName),
            new GoldbergInstallFile(
                "experimental/x64/steam_api64.dll",
                PathConstants.GoldbergStandardSteamApiDll64,
                ExperimentalFolderName),
            new GoldbergInstallFile(
                "experimental/x64/steamclient64.dll",
                PathConstants.GoldbergSteamClientDll64,
                ExperimentalFolderName),
            new GoldbergInstallFile("steamclient_experimental/steamclient.dll", PathConstants.GoldbergSteamClientDll32),
            new GoldbergInstallFile("steamclient_experimental/steamclient64.dll", PathConstants.GoldbergSteamClientDll64),
            new GoldbergInstallFile("steamclient_experimental/GameOverlayRenderer.dll", PathConstants.GoldbergGameOverlayRendererDll32),
            new GoldbergInstallFile("steamclient_experimental/GameOverlayRenderer64.dll", PathConstants.GoldbergGameOverlayRendererDll64),
            new GoldbergInstallFile("steam_old_lib/Steam.dll", PathConstants.GoldbergSteamDllFileName, SteamOldFolderName),
        };

        // Flat DLLs from older installs at goldberg\ root (pre-subfolder layout).
        private static readonly string[] LegacyFlatFileNames =
        {
            PathConstants.GoldbergSteamClientDll32,
            PathConstants.GoldbergSteamClientDll64,
            PathConstants.GoldbergGameOverlayRendererDll32,
            PathConstants.GoldbergGameOverlayRendererDll64,
            PathConstants.GoldbergStandardSteamApiDll32,
            PathConstants.GoldbergStandardSteamApiDll64,
            PathConstants.GoldbergSteamDllFileName,
            PathConstants.GoldbergSteamOriginalDllFileName,
        };

        public static IReadOnlyList<GoldbergInstallFile> GetReleaseInstallFiles() => ReleaseFiles;

        public static string GetInstalledFilePath(string goldbergRootDirectory, GoldbergInstallFile file)
        {
            if (string.IsNullOrEmpty(goldbergRootDirectory))
                throw new ArgumentException("Goldberg root directory is required.", nameof(goldbergRootDirectory));
            if (string.IsNullOrEmpty(file.FileName))
                throw new ArgumentException("Install file name is required.", nameof(file));

            string relativeDir = file.InstallRelativeDirectory;
            return string.IsNullOrEmpty(relativeDir)
                ? Path.Combine(goldbergRootDirectory, file.FileName)
                : Path.Combine(goldbergRootDirectory, relativeDir, file.FileName);
        }

        public static bool AreReleaseInstallFilesMissing(string goldbergRootDirectory)
        {
            if (string.IsNullOrEmpty(goldbergRootDirectory) || !Directory.Exists(goldbergRootDirectory))
                return true;

            foreach (GoldbergInstallFile file in ReleaseFiles)
            {
                if (!File.Exists(GetInstalledFilePath(goldbergRootDirectory, file)))
                    return true;
            }

            return false;
        }

        public static string ToArchivePath(string releaseRelativePath)
        {
            return ArchiveReleasePrefix + releaseRelativePath.Replace('\\', '/');
        }

        // Fork releases renamed experimental/x86 to experimental/x32; try both when extracting.
        public static IEnumerable<string> GetArchivePathCandidates(GoldbergInstallFile file)
        {
            yield return file.ArchivePath;

            string relative = file.ReleaseRelativePath.Replace('\\', '/');
            string alternateRelative = null;
            if (relative.IndexOf("experimental/x32/", StringComparison.Ordinal) >= 0)
                alternateRelative = relative.Replace("experimental/x32/", "experimental/x86/");
            else if (relative.IndexOf("experimental/x86/", StringComparison.Ordinal) >= 0)
                alternateRelative = relative.Replace("experimental/x86/", "experimental/x32/");

            if (!string.IsNullOrEmpty(alternateRelative)
                && !string.Equals(alternateRelative, relative, StringComparison.Ordinal))
            {
                yield return ToArchivePath(alternateRelative);
            }
        }

        public static void WriteGoldbergReadmeFile(string goldbergRootDirectory)
        {
            if (string.IsNullOrEmpty(goldbergRootDirectory))
                return;

            Directory.CreateDirectory(goldbergRootDirectory);
            Directory.CreateDirectory(Path.Combine(goldbergRootDirectory, SteamClientExtraDllsFolderName));
            string readmePath = Path.Combine(goldbergRootDirectory, GoldbergReadmeFileName);
            File.WriteAllText(readmePath, BuildGoldbergReadmeText(), Encoding.UTF8);
        }

        public static string BuildGoldbergReadmeText()
        {
            return "Optional extra DLLs — the only way SmartGoldbergEmu loads DLLs besides emulator files and Steam.dll mode."
                + "\r\n\r\n"
                + "Place .dll files here. At launch they are copied into each game's steam_settings/load_dlls folder; Goldberg loads them from there when the game starts. The per-game load_dlls folder is removed when the game exits. Do not use inject tools or put DLLs beside the game exe for extras."
                + "\r\n\r\n"
                + "Architecture in the file name:"
                + "\r\n\r\n"
                + "32-bit only: name must contain x32 (example: steamclient_extra_x32.dll)"
                + "\r\n"
                + "64-bit only: name must contain x64 (example: steamclient_extra_x64.dll)"
                + "\r\n"
                + "Both 32-bit and 64-bit: name has neither x32 nor x64 (example: steamclient_extra.dll)"
                + "\r\n\r\n"
                + "Optional load_order.txt in this folder is copied when the game has none yet."
                + "\r\n\r\n"
                + "Do not put these DLLs directly in steam_settings/load_dlls inside a game; use this folder instead.";
        }

        public static void RemoveLegacyFlatDllsFromGoldbergRoot(string goldbergRootDirectory)
        {
            if (string.IsNullOrEmpty(goldbergRootDirectory) || !Directory.Exists(goldbergRootDirectory))
                return;

            foreach (string fileName in LegacyFlatFileNames)
            {
                string path = Path.Combine(goldbergRootDirectory, fileName);
                if (!File.Exists(path))
                    continue;
                try
                {
                    File.Delete(path);
                }
                catch
                {
                }
            }
        }

        public static void RemoveLegacyGoldbergSubfolders(string goldbergRootDirectory)
        {
            if (string.IsNullOrEmpty(goldbergRootDirectory) || !Directory.Exists(goldbergRootDirectory))
                return;

            TryDeleteDirectory(Path.Combine(goldbergRootDirectory, "steam_old_lib"));
            TryDeleteDirectory(Path.Combine(goldbergRootDirectory, SteamClientExperimentalFolderName, "extra_dlls"));
            TryDeleteDirectory(Path.Combine(goldbergRootDirectory, ExperimentalFolderName, "x86"));
            TryDeleteDirectory(Path.Combine(goldbergRootDirectory, ExperimentalFolderName, "x64"));
            TryDeleteDirectory(Path.Combine(goldbergRootDirectory, ExperimentalFolderName, ExperimentalX32ArchiveFolderName));
            RemoveShippedSteamClientExtraDlls(goldbergRootDirectory);
        }

        private static void RemoveShippedSteamClientExtraDlls(string goldbergRootDirectory)
        {
            string extraDir = Path.Combine(goldbergRootDirectory, SteamClientExtraDllsFolderName);
            if (!Directory.Exists(extraDir))
                return;

            foreach (string fileName in new[] { "steamclient_extra_x86.dll", "steamclient_extra_x64.dll" })
            {
                string path = Path.Combine(extraDir, fileName);
                if (!File.Exists(path))
                    continue;
                try
                {
                    File.Delete(path);
                }
                catch
                {
                }
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            if (!Directory.Exists(path))
                return;
            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch
            {
            }
        }

        public readonly struct GoldbergInstallFile
        {
            public GoldbergInstallFile(string releaseRelativePath, string fileName, string installRelativeDirectory = null)
            {
                ReleaseRelativePath = releaseRelativePath;
                FileName = fileName;
                InstallRelativeDirectoryOverride = installRelativeDirectory;
            }

            public string ReleaseRelativePath { get; }
            public string FileName { get; }
            public string InstallRelativeDirectoryOverride { get; }

            public string ArchivePath => ToArchivePath(ReleaseRelativePath);

            public string InstallRelativeDirectory
            {
                get
                {
                    if (!string.IsNullOrEmpty(InstallRelativeDirectoryOverride))
                        return InstallRelativeDirectoryOverride.Replace('/', Path.DirectorySeparatorChar);

                    string relative = ReleaseRelativePath.Replace('/', Path.DirectorySeparatorChar);
                    string dir = Path.GetDirectoryName(relative);
                    return dir ?? string.Empty;
                }
            }
        }
    }
}
