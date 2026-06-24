using System.IO;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Services;
using SmartGoldbergEmu.Tests.TestSupport;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    public sealed class GoldbergFilesServiceAdditionalFilesSaveTests
    {
        private const ulong TestAppId = 480;

        [Fact]
        public void SaveAdditionalFiles_edit_mode_rejects_invalid_branches_json()
        {
            string gamesRoot = TestFileHelper.CreateTempDirectory("sge-branches-");
            try
            {
                var service = new GoldbergFilesService(gamesRoot);
                var failures = service.SaveAdditionalFiles(new GoldbergFilesService.AdditionalFilesSaveRequest
                {
                    AppId = TestAppId,
                    IsEditMode = true,
                    HasBranches = true,
                    BranchesJson = "{ not json"
                });

                Assert.Single(failures);
                Assert.Equal("branches", failures[0].Key);
                Assert.False(failures[0].Result.IsSuccess);
            }
            finally
            {
                try { Directory.Delete(gamesRoot, recursive: true); } catch { }
            }
        }

        [Fact]
        public void SaveAdditionalFiles_edit_mode_rejects_invalid_achievements_json()
        {
            string gamesRoot = TestFileHelper.CreateTempDirectory("sge-achievements-");
            try
            {
                var service = new GoldbergFilesService(gamesRoot);
                var failures = service.SaveAdditionalFiles(new GoldbergFilesService.AdditionalFilesSaveRequest
                {
                    AppId = TestAppId,
                    IsEditMode = true,
                    HasAchievements = true,
                    AchievementsJson = "[ not valid"
                });

                Assert.Single(failures);
                Assert.Equal("achievements", failures[0].Key);
                Assert.False(failures[0].Result.IsSuccess);
            }
            finally
            {
                try { Directory.Delete(gamesRoot, recursive: true); } catch { }
            }
        }

        [Fact]
        public void SaveAdditionalFiles_edit_mode_writes_valid_branches_json()
        {
            string gamesRoot = TestFileHelper.CreateTempDirectory("sge-branches-");
            try
            {
                var service = new GoldbergFilesService(gamesRoot);
                const string branchesJson = "{\"branches\":{\"beta\":{\"description\":\"Beta branch\"}}}";
                var failures = service.SaveAdditionalFiles(new GoldbergFilesService.AdditionalFilesSaveRequest
                {
                    AppId = TestAppId,
                    IsEditMode = true,
                    HasBranches = true,
                    BranchesJson = branchesJson
                });

                Assert.Empty(failures);
                string path = Path.Combine(
                    gamesRoot,
                    TestAppId.ToString(),
                    PathConstants.SteamSettingsFolderName,
                    PathConstants.GoldbergBranchesJsonFileName);
                Assert.True(File.Exists(path));
                Assert.Contains("beta", File.ReadAllText(path), System.StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                try { Directory.Delete(gamesRoot, recursive: true); } catch { }
            }
        }
        [Fact]
        public void SaveSubscribedGroups_writes_file_and_deletes_when_empty()
        {
            string gamesRoot = TestFileHelper.CreateTempDirectory("sge-subscribed-groups-");
            try
            {
                var service = new GoldbergFilesService(gamesRoot);
                string path = Path.Combine(
                    gamesRoot,
                    TestAppId.ToString(),
                    PathConstants.SteamSettingsFolderName,
                    PathConstants.GoldbergSubscribedGroupsFileName);

                var writeFailures = service.SaveAdditionalFiles(new GoldbergFilesService.AdditionalFilesSaveRequest
                {
                    AppId = TestAppId,
                    IsEditMode = true,
                    HasSubscribedGroups = true,
                    SubscribedGroups = "103582791433980119\r\n 103582791438562929 "
                });

                Assert.Empty(writeFailures);
                Assert.True(File.Exists(path));
                var lines = File.ReadAllLines(path);
                Assert.Equal(2, lines.Length);
                Assert.Equal("103582791433980119", lines[0]);
                Assert.Equal("103582791438562929", lines[1]);

                var deleteFailures = service.SaveAdditionalFiles(new GoldbergFilesService.AdditionalFilesSaveRequest
                {
                    AppId = TestAppId,
                    IsEditMode = true,
                    HasSubscribedGroups = true,
                    SubscribedGroups = "   \r\n"
                });

                Assert.Empty(deleteFailures);
                Assert.False(File.Exists(path));
            }
            finally
            {
                try { Directory.Delete(gamesRoot, recursive: true); } catch { }
            }
        }

        [Fact]
        public void SaveSubscribedGroupsClans_writes_file_and_deletes_when_empty()
        {
            string gamesRoot = TestFileHelper.CreateTempDirectory("sge-subscribed-clans-");
            try
            {
                var service = new GoldbergFilesService(gamesRoot);
                string path = Path.Combine(
                    gamesRoot,
                    TestAppId.ToString(),
                    PathConstants.SteamSettingsFolderName,
                    PathConstants.GoldbergSubscribedGroupsClansFileName);

                var writeFailures = service.SaveAdditionalFiles(new GoldbergFilesService.AdditionalFilesSaveRequest
                {
                    AppId = TestAppId,
                    IsEditMode = true,
                    HasSubscribedGroupsClans = true,
                    SubscribedGroupsClans = "000000000000000000\tGroup Name\tClan Tag"
                });

                Assert.Empty(writeFailures);
                Assert.True(File.Exists(path));
                Assert.Equal("000000000000000000\tGroup Name\tClan Tag", File.ReadAllText(path).TrimEnd('\r', '\n'));

                var deleteFailures = service.SaveAdditionalFiles(new GoldbergFilesService.AdditionalFilesSaveRequest
                {
                    AppId = TestAppId,
                    IsEditMode = true,
                    HasSubscribedGroupsClans = true,
                    SubscribedGroupsClans = string.Empty
                });

                Assert.Empty(deleteFailures);
                Assert.False(File.Exists(path));
            }
            finally
            {
                try { Directory.Delete(gamesRoot, recursive: true); } catch { }
            }
        }
    }
}
