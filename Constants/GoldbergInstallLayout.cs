using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SmartGoldbergEmu.Constants
{
    // Maps Windows release archive paths under release/ to install paths under goldberg\.
    // Fork archives use experimental/x32 and experimental/x64; we install steam_api/steamclient flat under SteamApi\.
    public static class GoldbergInstallLayout
    {
        public const string ArchiveReleasePrefix = "release/";

        // Install-local folder names under goldberg/.
        public const string SteamApiFolderName = "SteamApi";
        public const string SteamClientFolderName = "SteamClient";
        public const string SteamOldFolderName = "SteamOld";
        public const string ColdClientLoaderFolderName = "ColdClientLoader";
        // Steamworks Shared layout (_CommonRedist\...) for downloaded common redistributables.
        public const string CommonRedistFolderName = "CommonRedist";
        public const string ExtraDllsFolderName = "extra_dlls";

        // Pre-rename install folders; migrated into the names above on startup / Goldberg Update.
        public const string LegacyStandardSteamApiFolderName = "experimental";
        public const string LegacySteamClientFolderName = "steamclient_experimental";
        public const string LegacySteamOldFolderName = "steam_old";
        public const string LegacyColdClientFolderName = "coldclient";
        // Former top-level drop folder; migrated into SteamClient/extra_dlls.
        public const string LegacySteamClientExtraDllsFolderName = "steamclient_extra_dlls";

        public const string ExperimentalX32ArchiveFolderName = "x32";
        public const string GoldbergReadmeFileName = "readme.txt";
        public const string ShippedSteamClientExtraDll32FileName = "steamclient_extra_x86.dll";
        public const string ShippedSteamClientExtraDll64FileName = "steamclient_extra_x64.dll";

        private static readonly GoldbergInstallFile[] ReleaseFiles =
        {
            new GoldbergInstallFile(
                "experimental/x32/steam_api.dll",
                PathConstants.GoldbergStandardSteamApiDll32,
                SteamApiFolderName),
            new GoldbergInstallFile(
                "experimental/x32/steamclient.dll",
                PathConstants.GoldbergSteamClientDll32,
                SteamApiFolderName),
            new GoldbergInstallFile(
                "experimental/x64/steam_api64.dll",
                PathConstants.GoldbergStandardSteamApiDll64,
                SteamApiFolderName),
            new GoldbergInstallFile(
                "experimental/x64/steamclient64.dll",
                PathConstants.GoldbergSteamClientDll64,
                SteamApiFolderName),
            new GoldbergInstallFile(
                "steamclient_experimental/steamclient.dll",
                PathConstants.GoldbergSteamClientDll32,
                SteamClientFolderName),
            new GoldbergInstallFile(
                "steamclient_experimental/steamclient64.dll",
                PathConstants.GoldbergSteamClientDll64,
                SteamClientFolderName),
            new GoldbergInstallFile(
                "steamclient_experimental/GameOverlayRenderer.dll",
                PathConstants.GoldbergGameOverlayRendererDll32,
                SteamClientFolderName),
            new GoldbergInstallFile(
                "steamclient_experimental/GameOverlayRenderer64.dll",
                PathConstants.GoldbergGameOverlayRendererDll64,
                SteamClientFolderName),
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

        private static readonly GoldbergInstallFile[] ColdClientFiles =
        {
            new GoldbergInstallFile(
                "steamclient_experimental/steamclient_loader_x86.exe",
                PathConstants.GoldbergColdClientLoaderExe32,
                ColdClientLoaderFolderName),
            new GoldbergInstallFile(
                "steamclient_experimental/steamclient_loader_x64.exe",
                PathConstants.GoldbergColdClientLoaderExe64,
                ColdClientLoaderFolderName),
            new GoldbergInstallFile(
                "steamclient_experimental/extra_dlls/" + ShippedSteamClientExtraDll32FileName,
                ShippedSteamClientExtraDll32FileName,
                ColdClientLoaderFolderName + "/" + ExtraDllsFolderName),
            new GoldbergInstallFile(
                "steamclient_experimental/extra_dlls/" + ShippedSteamClientExtraDll64FileName,
                ShippedSteamClientExtraDll64FileName,
                ColdClientLoaderFolderName + "/" + ExtraDllsFolderName),
        };

        public static IReadOnlyList<GoldbergInstallFile> GetColdClientInstallFiles() => ColdClientFiles;

        public static bool AreColdClientInstallFilesMissing(string goldbergRootDirectory)
        {
            if (string.IsNullOrEmpty(goldbergRootDirectory) || !Directory.Exists(goldbergRootDirectory))
                return true;

            TryMigrateGoldbergInstallFolders(goldbergRootDirectory);

            foreach (GoldbergInstallFile file in ColdClientFiles)
            {
                if (!File.Exists(GetInstalledFilePath(goldbergRootDirectory, file)))
                    return true;
            }

            return false;
        }

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

            TryMigrateGoldbergInstallFolders(goldbergRootDirectory);

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

        public static string GetSteamClientExtraDllsDirectory(string goldbergRootDirectory)
        {
            if (string.IsNullOrEmpty(goldbergRootDirectory))
                throw new ArgumentException("Goldberg root directory is required.", nameof(goldbergRootDirectory));

            return Path.Combine(
                goldbergRootDirectory,
                SteamClientFolderName,
                ExtraDllsFolderName);
        }

        public static void WriteGoldbergReadmeFile(string goldbergRootDirectory)
        {
            if (string.IsNullOrEmpty(goldbergRootDirectory))
                return;

            Directory.CreateDirectory(goldbergRootDirectory);
            string extraDllsDir = GetSteamClientExtraDllsDirectory(goldbergRootDirectory);
            Directory.CreateDirectory(extraDllsDir);
            string readmePath = Path.Combine(extraDllsDir, GoldbergReadmeFileName);
            File.WriteAllText(readmePath, BuildGoldbergReadmeText(), Encoding.UTF8);
        }

        public static string BuildGoldbergReadmeText()
        {
            return "Legacy optional extra DLLs folder."
                + "\r\n\r\n"
                + "Place per-game DLLs in games/{appId}/steam_settings/load_dlls/ (top-level .dll only; Goldberg load_dlls is not recursive). That folder is kept across launches."
                + "\r\n\r\n"
                + "Do not put steamclient_extra_x86.dll or steamclient_extra_x64.dll in load_dlls (large FPS drop). Those belong under goldberg/ColdClientLoader/extra_dlls for ColdClient mode."
                + "\r\n\r\n"
                + "Architecture in the file name:"
                + "\r\n\r\n"
                + "32-bit only: name must contain x32 or x86 (example: plugin_x32.dll)"
                + "\r\n"
                + "64-bit only: name must contain x64 (example: plugin_x64.dll)"
                + "\r\n"
                + "Both 32-bit and 64-bit: name has neither x32, x86, nor x64 (example: plugin.dll)";
        }

        public static bool IsShippedSteamClientExtraDll(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            string name = Path.GetFileName(fileName);
            return string.Equals(name, ShippedSteamClientExtraDll32FileName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, ShippedSteamClientExtraDll64FileName, StringComparison.OrdinalIgnoreCase);
        }

        // Previous installs used experimental / steamclient_experimental / steam_old (and optional coldclient).
        // Rename those folders only — no file copy/merge.
        public static void TryMigrateGoldbergInstallFolders(string goldbergRootDirectory)
        {
            if (string.IsNullOrEmpty(goldbergRootDirectory) || !Directory.Exists(goldbergRootDirectory))
                return;

            TryRenameInstallFolder(goldbergRootDirectory, LegacyStandardSteamApiFolderName, SteamApiFolderName);
            TryRenameInstallFolder(goldbergRootDirectory, LegacySteamClientFolderName, SteamClientFolderName);
            TryRenameInstallFolder(goldbergRootDirectory, LegacySteamOldFolderName, SteamOldFolderName);
            TryRenameInstallFolder(goldbergRootDirectory, LegacyColdClientFolderName, ColdClientLoaderFolderName);
        }

        // Moves legacy drop folders into goldberg/SteamClient/extra_dlls when found.
        public static void TryMigrateLegacySteamClientExtraDlls(string goldbergRootDirectory)
        {
            if (string.IsNullOrEmpty(goldbergRootDirectory) || !Directory.Exists(goldbergRootDirectory))
                return;

            string destDir = GetSteamClientExtraDllsDirectory(goldbergRootDirectory);
            TryMigrateDirectoryInto(Path.Combine(goldbergRootDirectory, LegacySteamClientExtraDllsFolderName), destDir);
            // Brief mistaken path from an earlier build; fold into extra_dlls if present.
            TryMigrateDirectoryInto(
                Path.Combine(goldbergRootDirectory, SteamClientFolderName, PathConstants.GoldbergLoadDllsFolderName),
                destDir);
            TryMigrateDirectoryInto(
                Path.Combine(goldbergRootDirectory, LegacySteamClientFolderName, PathConstants.GoldbergLoadDllsFolderName),
                destDir);
        }

        private static void TryRenameInstallFolder(
            string goldbergRootDirectory,
            string legacyFolderName,
            string currentFolderName)
        {
            if (string.Equals(legacyFolderName, currentFolderName, StringComparison.Ordinal))
                return;

            string sourceDirectory = FindChildDirectory(goldbergRootDirectory, legacyFolderName);
            if (sourceDirectory == null)
                return;

            string destinationDirectory = Path.Combine(goldbergRootDirectory, currentFolderName);

            // steam_old → SteamOld needs a temp hop on case-insensitive Windows.
            if (string.Equals(legacyFolderName, currentFolderName, StringComparison.OrdinalIgnoreCase))
            {
                string onDiskName = Path.GetFileName(
                    sourceDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.Equals(onDiskName, currentFolderName, StringComparison.Ordinal))
                    return;

                string tempDirectory = Path.Combine(goldbergRootDirectory, currentFolderName + ".sge_rename_tmp");
                try
                {
                    if (Directory.Exists(tempDirectory))
                        return;
                    Directory.Move(sourceDirectory, tempDirectory);
                    Directory.Move(tempDirectory, destinationDirectory);
                }
                catch
                {
                }

                return;
            }

            // Target already present: leave both alone (no copy/merge).
            if (FindChildDirectory(goldbergRootDirectory, currentFolderName) != null)
                return;

            try
            {
                Directory.Move(sourceDirectory, destinationDirectory);
            }
            catch
            {
            }
        }

        private static string FindChildDirectory(string parentDirectory, string folderName)
        {
            if (string.IsNullOrEmpty(parentDirectory) || string.IsNullOrEmpty(folderName) || !Directory.Exists(parentDirectory))
                return null;

            try
            {
                foreach (string entry in Directory.EnumerateDirectories(parentDirectory))
                {
                    if (string.Equals(Path.GetFileName(entry), folderName, StringComparison.OrdinalIgnoreCase))
                        return entry;
                }
            }
            catch
            {
            }

            return null;
        }

        private static void TryMigrateDirectoryInto(string sourceDirectory, string destinationDirectory)
        {
            if (string.IsNullOrEmpty(sourceDirectory) || !Directory.Exists(sourceDirectory))
                return;

            string sourceFull = Path.GetFullPath(sourceDirectory);
            string destFull = Path.GetFullPath(destinationDirectory);
            if (string.Equals(sourceFull, destFull, StringComparison.OrdinalIgnoreCase))
                return;

            Directory.CreateDirectory(destinationDirectory);

            try
            {
                MoveDirectoryContentsPreferringDestination(sourceDirectory, destinationDirectory);
            }
            catch
            {
            }

            TryDeleteDirectory(sourceDirectory);
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

            TryMigrateGoldbergInstallFolders(goldbergRootDirectory);
            TryMigrateLegacySteamClientExtraDlls(goldbergRootDirectory);
            TryDeleteDirectory(Path.Combine(goldbergRootDirectory, "steam_old_lib"));
            RemoveNestedArchiveLayoutFolders(goldbergRootDirectory, SteamApiFolderName);
            RemoveNestedArchiveLayoutFolders(goldbergRootDirectory, LegacyStandardSteamApiFolderName);
            RemoveShippedSteamClientExtraDlls(goldbergRootDirectory);
        }

        private static void RemoveNestedArchiveLayoutFolders(string goldbergRootDirectory, string steamApiFolderName)
        {
            TryDeleteDirectory(Path.Combine(goldbergRootDirectory, steamApiFolderName, "x86"));
            TryDeleteDirectory(Path.Combine(goldbergRootDirectory, steamApiFolderName, "x64"));
            TryDeleteDirectory(Path.Combine(goldbergRootDirectory, steamApiFolderName, ExperimentalX32ArchiveFolderName));
        }

        private static void RemoveShippedSteamClientExtraDlls(string goldbergRootDirectory)
        {
            string extraDir = GetSteamClientExtraDllsDirectory(goldbergRootDirectory);
            if (!Directory.Exists(extraDir))
                return;

            foreach (string fileName in new[] { ShippedSteamClientExtraDll32FileName, ShippedSteamClientExtraDll64FileName })
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

        // Moves files/dirs from source into dest; when a dest path already exists, keep dest and drop the source copy.
        private static void MoveDirectoryContentsPreferringDestination(string sourceDirectory, string destinationDirectory)
        {
            foreach (string sourceFile in Directory.GetFiles(sourceDirectory))
            {
                string name = Path.GetFileName(sourceFile);
                try
                {
                    if (IsShippedSteamClientExtraDll(name))
                    {
                        File.Delete(sourceFile);
                        continue;
                    }

                    string destFile = Path.Combine(destinationDirectory, name);
                    if (File.Exists(destFile))
                        File.Delete(sourceFile);
                    else
                        File.Move(sourceFile, destFile);
                }
                catch
                {
                }
            }

            foreach (string sourceSubDir in Directory.GetDirectories(sourceDirectory))
            {
                string name = Path.GetFileName(sourceSubDir);
                string destSubDir = Path.Combine(destinationDirectory, name);
                try
                {
                    Directory.CreateDirectory(destSubDir);
                    MoveDirectoryContentsPreferringDestination(sourceSubDir, destSubDir);
                    TryDeleteDirectory(sourceSubDir);
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
