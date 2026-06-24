using System.IO;
using System.Linq;
using SmartGoldbergEmu.Constants;
using Xunit;

namespace SmartGoldbergEmu.Tests.Constants
{
    public sealed class GoldbergInstallLayoutTests
    {
        [Fact]
        public void GetReleaseInstallFiles_maps_expected_archive_entries()
        {
            var files = GoldbergInstallLayout.GetReleaseInstallFiles();
            Assert.Equal(9, files.Count);
            Assert.Contains(files, f => f.ArchivePath == "release/experimental/x32/steam_api.dll");
            Assert.Contains(files, f => f.ArchivePath == "release/experimental/x64/steam_api64.dll");
            Assert.Contains(files, f => f.ArchivePath == "release/steam_old_lib/Steam.dll");

            var x32SteamApi = files.First(f => f.FileName == PathConstants.GoldbergStandardSteamApiDll32);
            Assert.Equal(GoldbergInstallLayout.ExperimentalFolderName, x32SteamApi.InstallRelativeDirectory);

            var x64SteamApi = files.First(f => f.FileName == PathConstants.GoldbergStandardSteamApiDll64);
            Assert.Equal(GoldbergInstallLayout.ExperimentalFolderName, x64SteamApi.InstallRelativeDirectory);
        }

        [Fact]
        public void GetArchivePathCandidates_includes_x86_fallback_for_x32_entries()
        {
            var file = new GoldbergInstallLayout.GoldbergInstallFile(
                "experimental/x32/steam_api.dll",
                PathConstants.GoldbergStandardSteamApiDll32,
                GoldbergInstallLayout.ExperimentalFolderName);

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
        public void AreReleaseInstallFilesMissing_is_true_when_experimental_steam_api_missing()
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
            Assert.Contains("x64", text);
            Assert.Contains("load_dlls", text);
        }
    }
}
