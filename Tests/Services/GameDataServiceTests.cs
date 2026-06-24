using System;
using System.Collections.Generic;
using System.IO;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;
using SmartGoldbergEmu.Tests.TestSupport;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    public sealed class GameDataServiceTests
    {
        [Fact]
        public void SaveGameLibrary_and_LoadGameLibrary_round_trip()
        {
            string gamesRoot = TestFileHelper.CreateTempDirectory("sge-games-");
            try
            {
                var service = new GameDataService(gamesRoot);
                var game = new GameConfig
                {
                    AppId = 480,
                    AppName = "Spacewar",
                    Path = @"C:\Games\Spacewar\spacewar.exe",
                    StartFolder = @"C:\Games\Spacewar",
                    Parameters = "-novid",
                    WorkingDirectory = @"C:\Games\Spacewar",
                    GameGuid = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    LaunchMode = GoldbergLaunchMode.StandardSteamApi
                };

                var save = service.SaveGameLibrary(new List<GameConfig> { game });
                Assert.True(save.IsValid);

                var loaded = service.LoadGameLibrary();
                Assert.Single(loaded);
                Assert.Equal(480UL, loaded[0].AppId);
                Assert.Equal("Spacewar", loaded[0].AppName);
                Assert.Equal(GoldbergLaunchMode.StandardSteamApi, loaded[0].LaunchMode);
                Assert.Equal(game.GameGuid, loaded[0].GameGuid);
            }
            finally
            {
                try { Directory.Delete(gamesRoot, recursive: true); } catch { }
            }
        }

        [Fact]
        public void AddGame_rejects_duplicate_guid()
        {
            string gamesRoot = TestFileHelper.CreateTempDirectory("sge-games-");
            try
            {
                var service = new GameDataService(gamesRoot);
                var guid = Guid.Parse("22222222-2222-2222-2222-222222222222");
                var game = CreateSampleGame(guid, 268910, @"C:\Games\Cuphead\cuphead.exe");
                Assert.True(service.AddGame(game).IsValid);

                var duplicate = CreateSampleGame(guid, 225140, @"C:\Games\Duke\duke.exe");
                var result = service.AddGame(duplicate);
                Assert.False(result.IsValid);
                Assert.Contains("GUID", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                try { Directory.Delete(gamesRoot, recursive: true); } catch { }
            }
        }

        private static GameConfig CreateSampleGame(Guid guid, ulong appId, string exePath)
        {
            return new GameConfig
            {
                AppId = appId,
                AppName = "Test Game",
                Path = exePath,
                StartFolder = Path.GetDirectoryName(exePath),
                Parameters = string.Empty,
                WorkingDirectory = Path.GetDirectoryName(exePath),
                GameGuid = guid,
                LaunchMode = GoldbergLaunchMode.SteamClient
            };
        }
    }
}
