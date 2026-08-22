using System;
using System.IO;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using Xunit;

namespace SmartGoldbergEmu.Tests.Helpers
{
    public sealed class GameFolderPathHelperMissingInstallTests
    {
        [Fact]
        public void TryResolveStoredExecutable_false_when_exe_deleted()
        {
            string root = Path.Combine(Path.GetTempPath(), "sge-missing-" + Guid.NewGuid().ToString("N"));
            string gameFolder = Path.Combine(root, "Game");
            Directory.CreateDirectory(gameFolder);

            var game = new GameConfig
            {
                AppId = 1,
                StartFolder = gameFolder,
                Path = "game.exe"
            };

            try
            {
                Assert.False(GameFolderPathHelper.TryResolveStoredExecutable(game, out _));
                Assert.True(GameFolderPathHelper.TryGetExistingExecutableDirectory(game, out string directory));
                Assert.Equal(Path.GetFullPath(gameFolder), Path.GetFullPath(directory));
            }
            finally
            {
                TryDeleteRoot(root);
            }
        }

        [Fact]
        public void TryGetExistingExecutableDirectory_false_when_folder_deleted()
        {
            string root = Path.Combine(Path.GetTempPath(), "sge-missing-" + Guid.NewGuid().ToString("N"));
            string gameFolder = Path.Combine(root, "Game");

            var game = new GameConfig
            {
                AppId = 1,
                StartFolder = gameFolder,
                Path = "game.exe"
            };

            Assert.False(GameFolderPathHelper.TryResolveStoredExecutable(game, out _));
            Assert.False(GameFolderPathHelper.TryGetExistingExecutableDirectory(game, out _));
        }

        [Fact]
        public void GetMissingStoredExecutableMessage_includes_stored_path()
        {
            var game = new GameConfig { Path = "Megabonk.exe" };
            Assert.Equal(
                "Game executable not found: Megabonk.exe",
                GameFolderPathHelper.GetMissingStoredExecutableMessage(game));
        }

        [Fact]
        public void GetMissingStoredExecutableMessage_empty_path()
        {
            Assert.Equal(
                "Game executable path cannot be empty.",
                GameFolderPathHelper.GetMissingStoredExecutableMessage(new GameConfig()));
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
