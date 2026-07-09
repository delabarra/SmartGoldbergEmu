using System;
using System.IO;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using Xunit;

namespace SmartGoldbergEmu.Tests.Helpers
{
    public sealed class GameFolderPathHelperIconViewTests
    {
        [Fact]
        public void TryResolveListViewIconSourcePath_prefers_steam_resource_icon_over_game_executable()
        {
            string root = Path.Combine(Path.GetTempPath(), "sge-iconview-" + Guid.NewGuid().ToString("N"));
            string gameFolder = Path.Combine(root, "Game");
            string resources = Path.Combine(root, "games", "42", PathConstants.GamesPerAppResourcesFolderName);
            Directory.CreateDirectory(gameFolder);
            Directory.CreateDirectory(resources);

            string exePath = Path.Combine(gameFolder, "game.exe");
            string iconPath = Path.Combine(resources, PathConstants.GetSteamGameResourcesClientIconFileName(42));
            File.WriteAllText(exePath, string.Empty);
            File.WriteAllText(iconPath, string.Empty);

            var game = new GameConfig
            {
                AppId = 42,
                StartFolder = gameFolder,
                Path = "game.exe"
            };

            Assert.True(
                GameFolderPathHelper.TryResolveListViewIconSourcePath(game, iconPath, out string resolved));
            Assert.Equal(Path.GetFullPath(iconPath), Path.GetFullPath(resolved));
        }

        [Fact]
        public void TryResolveListViewIconSourcePath_falls_back_to_game_executable_when_no_steam_icon()
        {
            string root = Path.Combine(Path.GetTempPath(), "sge-iconview-" + Guid.NewGuid().ToString("N"));
            string gameFolder = Path.Combine(root, "Game");
            Directory.CreateDirectory(gameFolder);

            string exePath = Path.Combine(gameFolder, "game.exe");
            File.WriteAllText(exePath, string.Empty);

            var game = new GameConfig
            {
                AppId = 42,
                StartFolder = gameFolder,
                Path = "game.exe"
            };

            Assert.True(
                GameFolderPathHelper.TryResolveListViewIconSourcePath(game, null, out string resolved));
            Assert.Equal(Path.GetFullPath(exePath), Path.GetFullPath(resolved));

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
