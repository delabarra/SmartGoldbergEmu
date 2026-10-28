using System;
using System.IO;
using SmartGoldbergEmu.Helpers;
using Xunit;

namespace SmartGoldbergEmu.Tests.Helpers
{
    public sealed class GameFolderPathHelperInstallDirHintTests
    {
        [Fact]
        public void ShouldShowGameFolderInstallDirHint_false_when_installdir_unknown()
        {
            Assert.False(GameFolderPathHelper.ShouldShowGameFolderInstallDirHint(
                @"C:\Games\MyGame",
                "game.exe",
                null));
            Assert.False(GameFolderPathHelper.ShouldShowGameFolderInstallDirHint(
                @"C:\Games\MyGame",
                "game.exe",
                "   "));
        }

        [Fact]
        public void ShouldShowGameFolderInstallDirHint_false_when_folder_matches_installdir_root()
        {
            string root = Path.Combine(Path.GetTempPath(), "sge-hint-" + Guid.NewGuid().ToString("N"));
            string gameRoot = Path.Combine(root, "steamapps", "common", "Spacewar");
            string bin = Path.Combine(gameRoot, "bin");
            Directory.CreateDirectory(bin);
            string exe = Path.Combine(bin, "spacewar.exe");
            File.WriteAllText(exe, "x");

            try
            {
                Assert.False(GameFolderPathHelper.ShouldShowGameFolderInstallDirHint(
                    gameRoot,
                    @"bin\spacewar.exe",
                    "Spacewar"));
            }
            finally
            {
                TryDeleteRoot(root);
            }
        }

        [Fact]
        public void ShouldShowGameFolderInstallDirHint_true_when_folder_is_exe_subdir_not_installdir_root()
        {
            string root = Path.Combine(Path.GetTempPath(), "sge-hint-" + Guid.NewGuid().ToString("N"));
            string gameRoot = Path.Combine(root, "steamapps", "common", "Spacewar");
            string bin = Path.Combine(gameRoot, "bin");
            Directory.CreateDirectory(bin);
            string exe = Path.Combine(bin, "spacewar.exe");
            File.WriteAllText(exe, "x");

            try
            {
                Assert.True(GameFolderPathHelper.ShouldShowGameFolderInstallDirHint(
                    bin,
                    "spacewar.exe",
                    "Spacewar"));
            }
            finally
            {
                TryDeleteRoot(root);
            }
        }

        [Fact]
        public void ShouldShowGameFolderInstallDirHint_false_when_paths_incomplete()
        {
            Assert.False(GameFolderPathHelper.ShouldShowGameFolderInstallDirHint(
                string.Empty,
                "game.exe",
                "Spacewar"));
            Assert.False(GameFolderPathHelper.ShouldShowGameFolderInstallDirHint(
                @"C:\Games\Spacewar",
                string.Empty,
                "Spacewar"));
        }

        private static void TryDeleteRoot(string root)
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }
}
