using System;
using System.IO;
using System.Threading.Tasks;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;
using SteamKit;
using Xunit;

namespace SmartGoldbergEmu.Tests.Helpers
{
    public sealed class SteamPicsKeyValueHelperVdfCacheTests
    {
        [Fact]
        public void TryLoadExportedAppPicsFromValveFile_returns_null_when_file_missing()
        {
            string gamesDir = Path.Combine(Path.GetTempPath(), "sge-vdf-missing-" + Guid.NewGuid().ToString("N"));
            try
            {
                Assert.Null(SteamPicsKeyValueHelper.TryLoadExportedAppPicsFromValveFile(gamesDir, 480));
            }
            finally
            {
                if (Directory.Exists(gamesDir))
                    Directory.Delete(gamesDir, recursive: true);
            }
        }

        [Fact]
        public async Task ExtractLaunchOptionsAsync_works_from_in_memory_appinfo_root()
        {
            const ulong appId = 99112233;
            var game = new GameConfig
            {
                AppId = appId,
                AppName = "Offline test",
                AppPicsKeyValue = BuildLaunchOptionPicsRoot()
            };

            using (var service = new SteamProductInfoService())
            {
                var launchService = new LaunchOptionService(service, new ThemeService());
                var options = await launchService.ExtractLaunchOptionsAsync(game);

                Assert.Single(options);
                Assert.Equal("Play Game", options[0].Description);
                Assert.Equal("game.exe", options[0].Executable);
            }
        }

        [Fact]
        public async Task ExtractLaunchOptionsAsync_uses_pics_root_from_exported_vdf_shape()
        {
            const ulong appId = 99112233;
            string gamesDir = Path.Combine(Path.GetTempPath(), "sge-vdf-cache-" + Guid.NewGuid().ToString("N"));
            string vdfPath = PathConstants.CombineGamesPerAppValveDataFilePath(gamesDir, appId.ToString());
            Directory.CreateDirectory(Path.GetDirectoryName(vdfPath));

            KeyValue appInfoRoot = BuildLaunchOptionPicsRoot();
            using (var exportService = new SteamProductInfoService())
            {
                Assert.True(exportService.ExportAppPicsToValveTextFile(appId.ToString(), appInfoRoot, vdfPath));
            }

            string exportedText = File.ReadAllText(vdfPath);
            Assert.Contains("launch", exportedText);
            KeyValue directParse = KeyValue.ParseVdf(System.Text.Encoding.UTF8.GetBytes(exportedText));
            Assert.NotNull(directParse);
            Assert.NotEmpty(directParse.Children);

            try
            {
                KeyValue loaded = SteamPicsKeyValueHelper.TryLoadExportedAppPicsFromValveFile(gamesDir, appId);
                Assert.NotNull(loaded);
                Assert.NotNull(loaded.Children);
                Assert.NotEmpty(loaded.Children);
                Assert.NotNull(SteamPicsKeyValueHelper.FindChild(loaded, PathConstants.SteamAppsCommonDirectoryName));

                var game = new GameConfig
                {
                    AppId = appId,
                    AppName = "Offline test",
                    AppPicsKeyValue = loaded
                };

                using (var service = new SteamProductInfoService())
                {
                    var launchService = new LaunchOptionService(service, new ThemeService());
                    var options = await launchService.ExtractLaunchOptionsAsync(game);

                    Assert.Single(options);
                    Assert.Equal("Play Game", options[0].Description);
                    Assert.Equal("game.exe", options[0].Executable);
                }
            }
            finally
            {
                if (Directory.Exists(gamesDir))
                    Directory.Delete(gamesDir, recursive: true);
            }
        }

        private static KeyValue BuildLaunchOptionPicsRoot()
        {
            var appInfo = new KeyValue(SteamPicsKeyNames.AppInfo);
            var common = new KeyValue(PathConstants.SteamAppsCommonDirectoryName);
            var launch = new KeyValue(SteamPicsKeyNames.Launch);
            var entry = new KeyValue("0");
            entry.Children.Add(new KeyValue(SteamPicsKeyNames.Description, "Play Game"));
            entry.Children.Add(new KeyValue(SteamPicsKeyNames.Executable, "game.exe"));
            entry.Children.Add(new KeyValue(SteamPicsKeyNames.Type, "default"));
            launch.Children.Add(entry);
            common.Children.Add(launch);
            appInfo.Children.Add(common);
            return appInfo;
        }
    }
}
