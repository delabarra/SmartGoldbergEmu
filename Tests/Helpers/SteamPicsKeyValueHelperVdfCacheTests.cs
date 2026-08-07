using System;
using System.IO;
using System.Threading.Tasks;
using AppDataKit;
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
        public void AppCatalogSnapshotStore_TryLoad_returns_false_when_file_missing()
        {
            string gamesDir = Path.Combine(Path.GetTempPath(), "sge-catalog-missing-" + Guid.NewGuid().ToString("N"));
            try
            {
                Assert.False(AppCatalogSnapshotStore.TryLoad(480, out AppCatalogSnapshot snapshot, gamesDir));
                Assert.Null(snapshot);
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
                AppInfo = BuildLaunchOptionAppInfoRoot()
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

            AppInfoKeyValue appInfoRoot = BuildLaunchOptionAppInfoRoot();
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
                KeyValue loaded = KeyValue.ParseVdf(File.ReadAllBytes(vdfPath));
                Assert.NotNull(loaded);
                Assert.NotNull(loaded.Children);
                Assert.NotEmpty(loaded.Children);
                Assert.NotNull(SteamPicsKeyValueHelper.FindChild(loaded, PathConstants.SteamAppsCommonDirectoryName));

                var game = new GameConfig
                {
                    AppId = appId,
                    AppName = "Offline test",
                    AppInfo = AppDataKitBridgeService.ConvertFromSteamKit(loaded)
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

        private static AppInfoKeyValue BuildLaunchOptionAppInfoRoot()
        {
            var appInfo = new AppInfoKeyValue(SteamPicsKeyNames.AppInfo);
            var common = new AppInfoKeyValue(PathConstants.SteamAppsCommonDirectoryName);
            var launch = new AppInfoKeyValue(SteamPicsKeyNames.Launch);
            var entry = new AppInfoKeyValue("0");
            entry.Children.Add(new AppInfoKeyValue(SteamPicsKeyNames.Description, "Play Game"));
            entry.Children.Add(new AppInfoKeyValue(SteamPicsKeyNames.Executable, "game.exe"));
            entry.Children.Add(new AppInfoKeyValue(SteamPicsKeyNames.Type, "default"));
            launch.Children.Add(entry);
            common.Children.Add(launch);
            appInfo.Children.Add(common);
            return appInfo;
        }
    }
}
