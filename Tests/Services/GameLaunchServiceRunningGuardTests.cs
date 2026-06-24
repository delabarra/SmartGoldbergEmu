using System.IO;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;
using SmartGoldbergEmu.Tests.Fakes;
using SmartGoldbergEmu.Tests.TestSupport;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    [Collection("GameLaunch")]
    public sealed class GameLaunchServiceRunningGuardTests
    {
        [Fact]
        public void IsGameRunning_returns_false_when_no_session_is_active()
        {
            string gamesRoot = TestFileHelper.CreateTempDirectory("sge-guard-");
            var service = new GameLaunchService(
                new NullLogService(),
                new EmulatorConfigService(gamesRoot, Path.Combine(gamesRoot, "global.cfg")));
            var game = new GameConfig
            {
                AppId = 12345,
                AppName = "Test Game",
                Path = @"C:\Games\Test\game.exe",
            };

            Assert.False(service.IsGameRunning(game.AppId, game));
        }

        [Fact]
        public void IsGameRunning_returns_false_for_zero_app_id()
        {
            string gamesRoot = TestFileHelper.CreateTempDirectory("sge-guard-zero-");
            var service = new GameLaunchService(
                new NullLogService(),
                new EmulatorConfigService(gamesRoot, Path.Combine(gamesRoot, "global.cfg")));

            Assert.False(service.IsGameRunning(0, new GameConfig { AppId = 0 }));
        }
    }
}
