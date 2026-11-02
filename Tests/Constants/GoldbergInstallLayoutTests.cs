using System;
using System.IO;
using System.Linq;
using SmartGoldbergEmu.Constants;
using Xunit;

namespace SmartGoldbergEmu.Tests.Constants
{
    public sealed class GoldbergInstallLayoutTests
    {
        [Fact]
        public void GetColdClientInstallFiles_maps_loaders_and_extra_dlls_under_ColdClientLoader()
        {
            var files = GoldbergInstallLayout.GetColdClientInstallFiles();
            Assert.Equal(4, files.Count);
            Assert.Contains(files, f => f.FileName == PathConstants.GoldbergColdClientLoaderExe32
                && f.InstallRelativeDirectory == GoldbergInstallLayout.ColdClientLoaderFolderName);
            Assert.Contains(files, f => f.FileName == PathConstants.GoldbergColdClientLoaderExe64
                && f.InstallRelativeDirectory == GoldbergInstallLayout.ColdClientLoaderFolderName);
            Assert.Contains(files, f => f.FileName == GoldbergInstallLayout.ShippedSteamClientExtraDll32FileName
                && f.InstallRelativeDirectory.Replace('\\', '/') == "ColdClientLoader/extra_dlls");
            Assert.Contains(files, f => f.FileName == GoldbergInstallLayout.ShippedSteamClientExtraDll64FileName
                && f.InstallRelativeDirectory.Replace('\\', '/') == "ColdClientLoader/extra_dlls");
            Assert.DoesNotContain(files, f => f.FileName == PathConstants.GoldbergSteamClientDll64);
        }

        [Fact]
        public void GetReleaseInstallFiles_maps_expected_archive_entries()
        {
            var files = GoldbergInstallLayout.GetReleaseInstallFiles();
            Assert.Equal(8, files.Count);
            Assert.Contains(files, f => f.ArchivePath == "release/experimental/x32/steam_api.dll");
            Assert.Contains(files, f => f.ArchivePath == "release/experimental/x64/steam_api64.dll");
            Assert.Contains(files, f => f.ArchivePath == "release/steamclient_experimental/steamclient.dll");
            Assert.DoesNotContain(files, f => f.ArchivePath == "release/steam_old_lib/Steam.dll");

            var x32SteamApi = files.First(f => f.FileName == PathConstants.GoldbergStandardSteamApiDll32);
            Assert.Equal(GoldbergInstallLayout.SteamApiFolderName, x32SteamApi.InstallRelativeDirectory);

            var x64SteamApi = files.First(f => f.FileName == PathConstants.GoldbergStandardSteamApiDll64);
            Assert.Equal(GoldbergInstallLayout.SteamApiFolderName, x64SteamApi.InstallRelativeDirectory);

            var steamClient = files.First(f => f.ArchivePath == "release/steamclient_experimental/steamclient.dll");
            Assert.Equal(GoldbergInstallLayout.SteamClientFolderName, steamClient.InstallRelativeDirectory);
        }

        [Fact]
        public void GetArchivePathCandidates_includes_x86_fallback_for_x32_entries()
        {
            var file = new GoldbergInstallLayout.GoldbergInstallFile(
                "experimental/x32/steam_api.dll",
                PathConstants.GoldbergStandardSteamApiDll32,
                GoldbergInstallLayout.SteamApiFolderName);

            var candidates = GoldbergInstallLayout.GetArchivePathCandidates(file).ToList();
            Assert.Equal(2, candidates.Count);
            Assert.Equal("release/experimental/x32/steam_api.dll", candidates[0]);
            Assert.Equal("release/experimental/x86/steam_api.dll", candidates[1]);
        }

        [Fact]
        public void ToArchivePath_prefixes_release_folder()
        {
            Assert.Equal("release/experimental/x32/steam_api.dll",
                GoldbergInstallLayout.ToArchivePath("experimental/x32/steam_api.dll"));
        }

        [Fact]
        public void AreReleaseInstallFilesMissing_is_false_when_all_release_files_exist()
        {
            string root = Path.Combine(Path.GetTempPath(), "sge-goldberg-" + Path.GetRandomFileName());
            try
            {
                foreach (GoldbergInstallLayout.GoldbergInstallFile file in GoldbergInstallLayout.GetReleaseInstallFiles())
                {
                    string path = GoldbergInstallLayout.GetInstalledFilePath(root, file);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, new byte[] { 0 });
                }

                Assert.False(GoldbergInstallLayout.AreReleaseInstallFilesMissing(root));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void AreReleaseInstallFilesMissing_is_true_when_standard_steam_api_missing()
        {
            string root = Path.Combine(Path.GetTempPath(), "sge-goldberg-" + Path.GetRandomFileName());
            try
            {
                foreach (GoldbergInstallLayout.GoldbergInstallFile file in GoldbergInstallLayout.GetReleaseInstallFiles())
                {
                    if (file.ArchivePath.Contains("experimental/x64/steam_api64.dll"))
                        continue;

                    string path = GoldbergInstallLayout.GetInstalledFilePath(root, file);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, new byte[] { 0 });
                }

                Assert.True(GoldbergInstallLayout.AreReleaseInstallFilesMissing(root));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void BuildGoldbergReadmeText_documents_load_dlls_arch_markers()
        {
            string text = GoldbergInstallLayout.BuildGoldbergReadmeText();
            Assert.Contains("x32", text);
            Assert.Contains("x86", text);
            Assert.Contains("x64", text);
            Assert.Contains("load_dlls", text);
            Assert.Contains("games/{appId}/steam_settings/load_dlls", text);
            Assert.Contains("ColdClientLoader/extra_dlls", text);
            Assert.Contains(GoldbergInstallLayout.ShippedSteamClientExtraDll32FileName, text);
            Assert.Contains(GoldbergInstallLayout.ShippedSteamClientExtraDll64FileName, text);
            Assert.DoesNotContain("load_order", text);
        }

        [Theory]
        [InlineData("steamclient_extra_x86.dll", true)]
        [InlineData("steamclient_extra_x64.dll", true)]
        [InlineData("STEAMCLIENT_EXTRA_X64.DLL", true)]
        [InlineData("plugin_x64.dll", false)]
        [InlineData(null, false)]
        [InlineData("", false)]
        public void IsShippedSteamClientExtraDll_matches_upstream_inject_samples(string fileName, bool expected)
        {
            Assert.Equal(expected, GoldbergInstallLayout.IsShippedSteamClientExtraDll(fileName));
        }

        [Fact]
        public void TryMigrateGoldbergInstallFolders_renames_previous_install_layout()
        {
            // Matches previous SGE goldberg layout: experimental / steamclient_experimental / steam_old.
            string root = Path.Combine(Path.GetTempPath(), "sge-goldberg-folders-" + Path.GetRandomFileName());
            try
            {
                string legacyExperimental = Path.Combine(root, GoldbergInstallLayout.LegacyStandardSteamApiFolderName);
                string legacySteamClient = Path.Combine(root, GoldbergInstallLayout.LegacySteamClientFolderName);
                string legacySteamOld = Path.Combine(root, GoldbergInstallLayout.LegacySteamOldFolderName);
                Directory.CreateDirectory(legacyExperimental);
                Directory.CreateDirectory(Path.Combine(legacySteamClient, GoldbergInstallLayout.ExtraDllsFolderName));
                Directory.CreateDirectory(legacySteamOld);
                File.WriteAllBytes(Path.Combine(legacyExperimental, PathConstants.GoldbergStandardSteamApiDll32), new byte[] { 1 });
                File.WriteAllBytes(Path.Combine(legacyExperimental, PathConstants.GoldbergStandardSteamApiDll64), new byte[] { 2 });
                File.WriteAllBytes(Path.Combine(legacyExperimental, PathConstants.GoldbergSteamClientDll32), new byte[] { 3 });
                File.WriteAllBytes(Path.Combine(legacyExperimental, PathConstants.GoldbergSteamClientDll64), new byte[] { 4 });
                File.WriteAllBytes(Path.Combine(legacySteamClient, PathConstants.GoldbergSteamClientDll32), new byte[] { 5 });
                File.WriteAllBytes(Path.Combine(legacySteamClient, PathConstants.GoldbergSteamClientDll64), new byte[] { 6 });
                File.WriteAllBytes(Path.Combine(legacySteamClient, PathConstants.GoldbergGameOverlayRendererDll32), new byte[] { 7 });
                File.WriteAllBytes(Path.Combine(legacySteamClient, PathConstants.GoldbergGameOverlayRendererDll64), new byte[] { 8 });
                File.WriteAllBytes(Path.Combine(legacySteamOld, PathConstants.GoldbergSteamDllFileName), new byte[] { 9 });
                File.WriteAllBytes(Path.Combine(legacySteamOld, PathConstants.GoldbergSteamOriginalDllFileName), new byte[] { 10 });

                GoldbergInstallLayout.TryMigrateGoldbergInstallFolders(root);

                Assert.Null(FindChildDirectoryName(root, GoldbergInstallLayout.LegacyStandardSteamApiFolderName));
                Assert.Null(FindChildDirectoryName(root, GoldbergInstallLayout.LegacySteamClientFolderName));
                Assert.Null(FindChildDirectoryName(root, GoldbergInstallLayout.LegacySteamOldFolderName));
                Assert.Equal(
                    GoldbergInstallLayout.SteamApiFolderName,
                    FindChildDirectoryName(root, GoldbergInstallLayout.SteamApiFolderName));
                Assert.Equal(
                    GoldbergInstallLayout.SteamClientFolderName,
                    FindChildDirectoryName(root, GoldbergInstallLayout.SteamClientFolderName));
                Assert.Equal(
                    GoldbergInstallLayout.SteamOldFolderName,
                    FindChildDirectoryName(root, GoldbergInstallLayout.SteamOldFolderName));
                Assert.True(File.Exists(Path.Combine(root, GoldbergInstallLayout.SteamApiFolderName, PathConstants.GoldbergStandardSteamApiDll32)));
                Assert.True(File.Exists(Path.Combine(root, GoldbergInstallLayout.SteamClientFolderName, PathConstants.GoldbergGameOverlayRendererDll64)));
                Assert.True(Directory.Exists(Path.Combine(root, GoldbergInstallLayout.SteamClientFolderName, GoldbergInstallLayout.ExtraDllsFolderName)));
                Assert.True(File.Exists(Path.Combine(root, GoldbergInstallLayout.SteamOldFolderName, PathConstants.GoldbergSteamDllFileName)));
                Assert.False(GoldbergInstallLayout.AreReleaseInstallFilesMissing(root));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        private static string FindChildDirectoryName(string parentDirectory, string folderName)
        {
            foreach (string entry in Directory.EnumerateDirectories(parentDirectory))
            {
                string name = Path.GetFileName(entry);
                if (string.Equals(name, folderName, StringComparison.OrdinalIgnoreCase))
                    return name;
            }

            return null;
        }

        [Fact]
        public void TryMigrateGoldbergInstallFolders_skips_rename_when_destination_already_exists()
        {
            string root = Path.Combine(Path.GetTempPath(), "sge-goldberg-folders-pref-" + Path.GetRandomFileName());
            try
            {
                string legacy = Path.Combine(root, GoldbergInstallLayout.LegacyStandardSteamApiFolderName);
                string current = Path.Combine(root, GoldbergInstallLayout.SteamApiFolderName);
                Directory.CreateDirectory(legacy);
                Directory.CreateDirectory(current);
                File.WriteAllBytes(Path.Combine(legacy, "steam_api.dll"), new byte[] { 1 });
                File.WriteAllBytes(Path.Combine(current, "steam_api.dll"), new byte[] { 9 });

                GoldbergInstallLayout.TryMigrateGoldbergInstallFolders(root);

                Assert.True(Directory.Exists(legacy));
                Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(Path.Combine(legacy, "steam_api.dll")));
                Assert.Equal(new byte[] { 9 }, File.ReadAllBytes(Path.Combine(current, "steam_api.dll")));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void TryMigrateLegacySteamClientExtraDlls_moves_content_and_removes_legacy_folder()
        {
            string root = Path.Combine(Path.GetTempPath(), "sge-goldberg-migrate-" + Path.GetRandomFileName());
            try
            {
                string legacyDir = Path.Combine(root, GoldbergInstallLayout.LegacySteamClientExtraDllsFolderName);
                Directory.CreateDirectory(legacyDir);
                File.WriteAllBytes(Path.Combine(legacyDir, "plugin_x64.dll"), new byte[] { 1 });
                Directory.CreateDirectory(Path.Combine(legacyDir, "nested"));
                File.WriteAllBytes(Path.Combine(legacyDir, "nested", "helper.dll"), new byte[] { 2 });
                File.WriteAllText(Path.Combine(legacyDir, "notes.txt"), "plugin_x64.dll");

                string existingDest = GoldbergInstallLayout.GetSteamClientExtraDllsDirectory(root);
                Directory.CreateDirectory(existingDest);
                File.WriteAllBytes(Path.Combine(existingDest, "keep_me.dll"), new byte[] { 9 });

                GoldbergInstallLayout.TryMigrateLegacySteamClientExtraDlls(root);

                Assert.False(Directory.Exists(legacyDir));
                Assert.True(File.Exists(Path.Combine(existingDest, "plugin_x64.dll")));
                Assert.True(File.Exists(Path.Combine(existingDest, "nested", "helper.dll")));
                Assert.True(File.Exists(Path.Combine(existingDest, "notes.txt")));
                Assert.True(File.Exists(Path.Combine(existingDest, "keep_me.dll")));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void TryMigrateLegacySteamClientExtraDlls_prefers_existing_destination_file()
        {
            string root = Path.Combine(Path.GetTempPath(), "sge-goldberg-migrate-" + Path.GetRandomFileName());
            try
            {
                string legacyDir = Path.Combine(root, GoldbergInstallLayout.LegacySteamClientExtraDllsFolderName);
                Directory.CreateDirectory(legacyDir);
                File.WriteAllBytes(Path.Combine(legacyDir, "shared.dll"), new byte[] { 1 });

                string destDir = GoldbergInstallLayout.GetSteamClientExtraDllsDirectory(root);
                Directory.CreateDirectory(destDir);
                File.WriteAllBytes(Path.Combine(destDir, "shared.dll"), new byte[] { 7 });

                GoldbergInstallLayout.TryMigrateLegacySteamClientExtraDlls(root);

                Assert.False(Directory.Exists(legacyDir));
                Assert.Equal(new byte[] { 7 }, File.ReadAllBytes(Path.Combine(destDir, "shared.dll")));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void TryMigrateLegacySteamClientExtraDlls_folds_mistaken_steamclient_load_dlls()
        {
            string root = Path.Combine(Path.GetTempPath(), "sge-goldberg-migrate-" + Path.GetRandomFileName());
            try
            {
                string mistakenDir = Path.Combine(
                    root,
                    GoldbergInstallLayout.SteamClientFolderName,
                    PathConstants.GoldbergLoadDllsFolderName);
                Directory.CreateDirectory(mistakenDir);
                File.WriteAllBytes(Path.Combine(mistakenDir, "from_load_dlls.dll"), new byte[] { 3 });

                GoldbergInstallLayout.TryMigrateLegacySteamClientExtraDlls(root);

                string destDir = GoldbergInstallLayout.GetSteamClientExtraDllsDirectory(root);
                Assert.False(Directory.Exists(mistakenDir));
                Assert.True(File.Exists(Path.Combine(destDir, "from_load_dlls.dll")));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void TryMigrateLegacySteamClientExtraDlls_folds_legacy_steamclient_load_dlls()
        {
            string root = Path.Combine(Path.GetTempPath(), "sge-goldberg-migrate-legacy-load-" + Path.GetRandomFileName());
            try
            {
                string mistakenDir = Path.Combine(
                    root,
                    GoldbergInstallLayout.LegacySteamClientFolderName,
                    PathConstants.GoldbergLoadDllsFolderName);
                Directory.CreateDirectory(mistakenDir);
                File.WriteAllBytes(Path.Combine(mistakenDir, "from_legacy_load_dlls.dll"), new byte[] { 4 });

                GoldbergInstallLayout.TryMigrateLegacySteamClientExtraDlls(root);

                string destDir = GoldbergInstallLayout.GetSteamClientExtraDllsDirectory(root);
                Assert.False(Directory.Exists(mistakenDir));
                Assert.True(File.Exists(Path.Combine(destDir, "from_legacy_load_dlls.dll")));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void TryMigrateLegacySteamClientExtraDlls_drops_shipped_inject_samples()
        {
            string root = Path.Combine(Path.GetTempPath(), "sge-goldberg-migrate-skip-" + Path.GetRandomFileName());
            try
            {
                string legacyDir = Path.Combine(root, GoldbergInstallLayout.LegacySteamClientExtraDllsFolderName);
                Directory.CreateDirectory(legacyDir);
                File.WriteAllBytes(Path.Combine(legacyDir, "plugin_x64.dll"), new byte[] { 1 });
                File.WriteAllBytes(
                    Path.Combine(legacyDir, GoldbergInstallLayout.ShippedSteamClientExtraDll64FileName),
                    new byte[] { 2 });

                GoldbergInstallLayout.TryMigrateLegacySteamClientExtraDlls(root);

                string destDir = GoldbergInstallLayout.GetSteamClientExtraDllsDirectory(root);
                Assert.False(Directory.Exists(legacyDir));
                Assert.True(File.Exists(Path.Combine(destDir, "plugin_x64.dll")));
                Assert.False(File.Exists(Path.Combine(destDir, GoldbergInstallLayout.ShippedSteamClientExtraDll64FileName)));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void RemoveLegacyGoldbergSubfolders_strips_shipped_inject_samples()
        {
            string root = Path.Combine(Path.GetTempPath(), "sge-goldberg-strip-" + Path.GetRandomFileName());
            try
            {
                string extraDir = GoldbergInstallLayout.GetSteamClientExtraDllsDirectory(root);
                Directory.CreateDirectory(extraDir);
                File.WriteAllBytes(Path.Combine(extraDir, "plugin_x64.dll"), new byte[] { 1 });
                File.WriteAllBytes(
                    Path.Combine(extraDir, GoldbergInstallLayout.ShippedSteamClientExtraDll32FileName),
                    new byte[] { 2 });
                File.WriteAllBytes(
                    Path.Combine(extraDir, GoldbergInstallLayout.ShippedSteamClientExtraDll64FileName),
                    new byte[] { 3 });

                GoldbergInstallLayout.RemoveLegacyGoldbergSubfolders(root);

                Assert.True(File.Exists(Path.Combine(extraDir, "plugin_x64.dll")));
                Assert.False(File.Exists(Path.Combine(extraDir, GoldbergInstallLayout.ShippedSteamClientExtraDll32FileName)));
                Assert.False(File.Exists(Path.Combine(extraDir, GoldbergInstallLayout.ShippedSteamClientExtraDll64FileName)));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }
    }
}
