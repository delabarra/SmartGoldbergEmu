using System.IO;
using System.Text;
using System.Threading.Tasks;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;
using SmartGoldbergEmu.Tests.TestSupport;
using SmartGoldbergEmu.Validation;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    public sealed class GameSetupServiceTests
    {
        [Fact]
        public async Task CreateGameConfigAsync_sets_steam_dll_when_steam_api_missing()
        {
            string gameRoot = TestFileHelper.CreateTempDirectory("sge-setup-noapi-");
            string gamesIniRoot = TestFileHelper.CreateTempDirectory("sge-games-");
            try
            {
                string exePath = Path.Combine(gameRoot, "game.exe");
                File.WriteAllBytes(exePath, Encoding.ASCII.GetBytes("MZ"));

                var service = new GameSetupService(new GameDataService(gamesIniRoot));
                GameConfig config = await service.CreateGameConfigAsync(
                    exePath,
                    new GameSetupResult { AppId = 480, GameName = "Spacewar" },
                    fetchDlc: false);

                Assert.Equal(GoldbergLaunchMode.SteamDllBesideExe, config.LaunchMode);
                Assert.Equal(gameRoot, config.StartFolder);
            }
            finally
            {
                try { Directory.Delete(gameRoot, recursive: true); } catch { }
                try { Directory.Delete(gamesIniRoot, recursive: true); } catch { }
            }
        }

        [Fact]
        public async Task CreateGameConfigAsync_keeps_default_steam_client_when_steam_api_present()
        {
            string gameRoot = TestFileHelper.CreateTempDirectory("sge-setup-api-");
            string gamesIniRoot = TestFileHelper.CreateTempDirectory("sge-games-");
            try
            {
                string exePath = Path.Combine(gameRoot, "game.exe");
                File.WriteAllBytes(exePath, Encoding.ASCII.GetBytes("MZ"));
                File.WriteAllBytes(
                    Path.Combine(gameRoot, SteamApiValidator.SteamApiDll64),
                    Encoding.ASCII.GetBytes("steam_api64"));

                var service = new GameSetupService(new GameDataService(gamesIniRoot));
                GameConfig config = await service.CreateGameConfigAsync(
                    exePath,
                    new GameSetupResult { AppId = 480, GameName = "Spacewar" },
                    fetchDlc: false);

                Assert.Equal(GoldbergLaunchMode.SteamClient, config.LaunchMode);
            }
            finally
            {
                try { Directory.Delete(gameRoot, recursive: true); } catch { }
                try { Directory.Delete(gamesIniRoot, recursive: true); } catch { }
            }
        }
    }
}
