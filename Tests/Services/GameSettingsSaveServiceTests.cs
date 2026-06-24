using System.IO;
using System.Threading.Tasks;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;
using SmartGoldbergEmu.Tests.Fakes;
using SmartGoldbergEmu.Tests.TestSupport;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    public sealed class GameSettingsSaveServiceTests
    {
        private const ulong TestAppId = 480;

        [Fact]
        public async Task SaveEmulatorSettingsFromRequestAsync_add_mode_writes_main_ini_via_save_all()
        {
            using (var ctx = CreateContext())
            {
                var snapshot = ctx.EmulatorConfig.LoadGameSettingsSnapshot(TestAppId);
                snapshot.Main.Offline = true;

                var request = BuildRequest(ctx, isEditMode: false, snapshot, customStatsRawJson: string.Empty);
                GameSettingsSaveResult result = await ctx.Service.SaveEmulatorSettingsFromRequestAsync(request);

                Assert.True(result.IsSuccess, result.ErrorMessage);
                string mainPath = Path.Combine(ctx.SteamSettingsPath, PathConstants.GoldbergMainIniFileName);
                Assert.True(File.Exists(mainPath));
                Assert.Contains("offline=1", File.ReadAllText(mainPath), System.StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public async Task SaveEmulatorSettingsFromRequestAsync_edit_mode_writes_only_changed_main_ini()
        {
            using (var ctx = CreateContext())
            {
                var snapshot = ctx.EmulatorConfig.LoadGameSettingsSnapshot(TestAppId);
                snapshot.Main.Offline = true;

                var request = BuildRequest(ctx, isEditMode: true, snapshot, customStatsRawJson: string.Empty);
                GameSettingsSaveResult result = await ctx.Service.SaveEmulatorSettingsFromRequestAsync(request);

                Assert.True(result.IsSuccess, result.ErrorMessage);
                string mainPath = Path.Combine(ctx.SteamSettingsPath, PathConstants.GoldbergMainIniFileName);
                string appPath = Path.Combine(ctx.SteamSettingsPath, PathConstants.GoldbergAppIniFileName);
                Assert.True(File.Exists(mainPath));
                Assert.False(File.Exists(appPath));
            }
        }

        [Fact]
        public async Task SaveEmulatorSettingsFromRequestAsync_edit_mode_returns_invalid_custom_stats_for_malformed_json()
        {
            using (var ctx = CreateContext())
            {
                var snapshot = ctx.EmulatorConfig.LoadGameSettingsSnapshot(TestAppId);
                var request = BuildRequest(ctx, isEditMode: true, snapshot, customStatsRawJson: "{ not json");

                GameSettingsSaveResult result = await ctx.Service.SaveEmulatorSettingsFromRequestAsync(request);

                Assert.False(result.IsSuccess);
                Assert.True(result.HasCustomStatsJsonError);
            }
        }

        [Fact]
        public async Task SaveEmulatorSettingsFromRequestAsync_edit_mode_writes_valid_stats_json()
        {
            using (var ctx = CreateContext())
            {
                var snapshot = ctx.EmulatorConfig.LoadGameSettingsSnapshot(TestAppId);
                const string statsJson = "[{\"name\":\"STAT_WINS\",\"default\":0}]";
                var request = BuildRequest(ctx, isEditMode: true, snapshot, customStatsRawJson: statsJson);

                GameSettingsSaveResult result = await ctx.Service.SaveEmulatorSettingsFromRequestAsync(request);

                Assert.True(result.IsSuccess, result.ErrorMessage);
                string statsPath = Path.Combine(ctx.SteamSettingsPath, PathConstants.GoldbergStatsJsonFileName);
                Assert.True(File.Exists(statsPath));
                Assert.Contains("STAT_WINS", File.ReadAllText(statsPath), System.StringComparison.OrdinalIgnoreCase);
            }
        }

        private static GameSettingsSaveTestContext CreateContext()
        {
            string gamesRoot = TestFileHelper.CreateTempDirectory("sge-save-service-games-");
            string globalRoot = TestFileHelper.CreateTempDirectory("sge-save-service-global-");
            var cfgService = new GoldbergCfgService(globalRoot);
            var emulatorConfig = new EmulatorConfigService(gamesRoot, globalRoot, cfgService);
            var goldbergFiles = new GoldbergFilesService(gamesRoot);
            var service = new GameSettingsSaveService(
                emulatorConfig,
                new FakeRegistryService(),
                goldbergFiles,
                new GameImageService(),
                new SteamProductInfoService());

            return new GameSettingsSaveTestContext(gamesRoot, globalRoot, emulatorConfig, service);
        }

        private static GameSettingsSaveRequest BuildRequest(
            GameSettingsSaveTestContext ctx,
            bool isEditMode,
            GameSettingsSnapshot snapshot,
            string customStatsRawJson)
        {
            return new GameSettingsSaveRequest
            {
                GameConfig = new GameConfig { AppId = TestAppId, AppName = "Test Game" },
                IsEditMode = isEditMode,
                CustomStatsRawJson = customStatsRawJson,
                BuildSnapshot = () => snapshot,
                ResolveAchievementLanguage = s => s?.User?.Language ?? ApplicationConstants.DefaultLanguage,
                SaveDlcAndPaths = () => { },
                SaveAdditionalGoldbergFiles = () => { },
            };
        }

        private sealed class GameSettingsSaveTestContext : System.IDisposable
        {
            public string GamesRoot { get; }
            public EmulatorConfigService EmulatorConfig { get; }
            public GameSettingsSaveService Service { get; }
            public string SteamSettingsPath { get; }
            private readonly string _globalRoot;

            public GameSettingsSaveTestContext(
                string gamesRoot,
                string globalRoot,
                EmulatorConfigService emulatorConfig,
                GameSettingsSaveService service)
            {
                GamesRoot = gamesRoot;
                _globalRoot = globalRoot;
                EmulatorConfig = emulatorConfig;
                Service = service;
                SteamSettingsPath = Path.Combine(gamesRoot, TestAppId.ToString(), PathConstants.SteamSettingsFolderName);
            }

            public void Dispose()
            {
                TryDeleteDirectory(GamesRoot);
                TryDeleteDirectory(_globalRoot);
            }

            private static void TryDeleteDirectory(string path)
            {
                try
                {
                    if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                        Directory.Delete(path, recursive: true);
                }
                catch
                {
                }
            }
        }
    }
}
