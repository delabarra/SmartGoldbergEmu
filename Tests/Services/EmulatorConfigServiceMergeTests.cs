using System;
using System.IO;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Tests.TestSupport;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    public sealed class EmulatorConfigServiceMergeTests
    {
        private const ulong TestAppId = 480;

        [Fact]
        public void LoadGameSettingsSnapshot_uses_global_main_settings_when_per_game_folder_missing()
        {
            using (var ctx = new EmulatorConfigTestContext())
            {
                ctx.CfgService.SaveGlobalMainSettings(new MainSettings { Offline = true, RecordPlaytime = true });

                GameSettingsSnapshot snapshot = ctx.Service.LoadGameSettingsSnapshot(TestAppId);

                Assert.False(snapshot.Main.Offline);
                Assert.True(snapshot.Main.RecordPlaytime);
            }
        }

        [Fact]
        public void LoadGameSettingsSnapshot_merges_per_game_main_flag_over_global()
        {
            using (var ctx = new EmulatorConfigTestContext())
            {
                ctx.CfgService.SaveGlobalMainSettings(new MainSettings { Offline = false, RecordPlaytime = true });
                ctx.WriteGameMainIni(
                    TestAppId,
                    "[main::connectivity]\r\noffline=1\r\n");

                GameSettingsSnapshot snapshot = ctx.Service.LoadGameSettingsSnapshot(TestAppId);

                Assert.True(snapshot.Main.Offline);
                Assert.True(snapshot.Main.RecordPlaytime);
            }
        }

        [Fact]
        public void LoadGameSettingsSnapshot_merges_per_game_listen_port_over_global_default()
        {
            using (var ctx = new EmulatorConfigTestContext())
            {
                ctx.CfgService.SaveGlobalMainSettings(new MainSettings { ListenPort = 47584 });
                ctx.WriteGameMainIni(
                    TestAppId,
                    "[main::connectivity]\r\nlisten_port=9999\r\n");

                GameSettingsSnapshot snapshot = ctx.Service.LoadGameSettingsSnapshot(TestAppId);

                Assert.Equal(9999, snapshot.Main.ListenPort);
            }
        }

        [Fact]
        public void LoadGameSettingsSnapshot_does_not_inherit_global_ticket_into_per_game_user_settings()
        {
            using (var ctx = new EmulatorConfigTestContext())
            {
                ctx.CfgService.SaveGlobalUserSettings(new UserSettings
                {
                    Ticket = "GLOBALTICKET",
                    Language = "english",
                });
                Directory.CreateDirectory(ctx.GetGameSteamSettingsPath(TestAppId));

                GameSettingsSnapshot snapshot = ctx.Service.LoadGameSettingsSnapshot(TestAppId);

                Assert.Equal(string.Empty, snapshot.User.Ticket);
                Assert.Equal("english", snapshot.User.Language);
            }
        }

        [Fact]
        public void LoadGameSettingsSnapshot_uses_per_game_user_language_when_set()
        {
            using (var ctx = new EmulatorConfigTestContext())
            {
                ctx.CfgService.SaveGlobalUserSettings(new UserSettings { Language = "english" });
                ctx.WriteGameUserIni(
                    TestAppId,
                    "[user::general]\r\nlanguage=french\r\n");

                GameSettingsSnapshot snapshot = ctx.Service.LoadGameSettingsSnapshot(TestAppId);

                Assert.Equal("french", snapshot.User.Language);
            }
        }

        [Fact]
        public void SavePerGameTicketAndAltSteamId_persists_values_in_per_game_user_ini()
        {
            using (var ctx = new EmulatorConfigTestContext())
            {
                var saveResult = ctx.Service.SavePerGameTicketAndAltSteamId(TestAppId, "PERGAME", "ALT12345");
                Assert.True(saveResult.IsSuccess, saveResult.ErrorMessage);

                GameSettingsSnapshot snapshot = ctx.Service.LoadGameSettingsSnapshot(TestAppId);
                Assert.Equal("PERGAME", snapshot.User.Ticket);
                Assert.Equal("ALT12345", snapshot.User.AltSteamId);
            }
        }

        [Fact]
        public void HasUnsavedChanges_returns_false_when_snapshot_matches_loaded_state()
        {
            using (var ctx = new EmulatorConfigTestContext())
            {
                GameSettingsSnapshot snapshot = ctx.Service.LoadGameSettingsSnapshot(TestAppId);
                Assert.False(ctx.Service.HasUnsavedChanges(snapshot));
            }
        }

        [Fact]
        public void HasUnsavedChanges_returns_true_when_network_main_setting_differs()
        {
            using (var ctx = new EmulatorConfigTestContext())
            {
                GameSettingsSnapshot snapshot = ctx.Service.LoadGameSettingsSnapshot(TestAppId);
                snapshot.Main.Offline = true;
                Assert.True(ctx.Service.HasUnsavedChanges(snapshot));
            }
        }

        [Fact]
        public void HasUnsavedChanges_returns_false_after_save_modified_persists_changes()
        {
            using (var ctx = new EmulatorConfigTestContext())
            {
                GameSettingsSnapshot snapshot = ctx.Service.LoadGameSettingsSnapshot(TestAppId);
                snapshot.Main.Offline = true;
                Assert.True(ctx.Service.HasUnsavedChanges(snapshot));

                SaveResult saveResult = ctx.Service.SaveModifiedGameSettings(TestAppId, snapshot);
                Assert.True(saveResult.IsSuccess, saveResult.ErrorMessage);
                Assert.False(ctx.Service.HasUnsavedChanges(snapshot));
            }
        }

        [Fact]
        public void SaveModifiedGameSettings_writes_only_changed_main_ini()
        {
            using (var ctx = new EmulatorConfigTestContext())
            {
                GameSettingsSnapshot snapshot = ctx.Service.LoadGameSettingsSnapshot(TestAppId);
                snapshot.Main.Offline = true;

                SaveResult saveResult = ctx.Service.SaveModifiedGameSettings(TestAppId, snapshot);
                Assert.True(saveResult.IsSuccess, saveResult.ErrorMessage);

                string settingsFolder = ctx.GetGameSteamSettingsPath(TestAppId);
                string mainPath = Path.Combine(settingsFolder, PathConstants.GoldbergMainIniFileName);
                string overlayPath = Path.Combine(settingsFolder, PathConstants.GoldbergOverlayIniFileName);

                Assert.True(File.Exists(mainPath));
                Assert.Contains("offline=1", File.ReadAllText(mainPath), StringComparison.OrdinalIgnoreCase);
                Assert.False(File.Exists(overlayPath));
            }
        }

        [Fact]
        public void LoadGameSettingsSnapshot_merges_per_game_overlay_flag_over_global()
        {
            using (var ctx = new EmulatorConfigTestContext())
            {
                ctx.CfgService.SaveGlobalOverlaySettings(new OverlaySettings { DisableFriendNotification = true });
                ctx.WriteGameOverlayIni(
                    TestAppId,
                    "[overlay::general]\r\nenable_experimental_overlay=1\r\n");

                GameSettingsSnapshot snapshot = ctx.Service.LoadGameSettingsSnapshot(TestAppId);

                Assert.True(snapshot.Overlay.EnableExperimentalOverlay);
                Assert.True(snapshot.Overlay.DisableFriendNotification);
            }
        }

        [Fact]
        public void LoadGameSettingsSnapshot_inherits_global_overlay_when_per_game_has_no_override()
        {
            using (var ctx = new EmulatorConfigTestContext())
            {
                ctx.CfgService.SaveGlobalOverlaySettings(new OverlaySettings { DisableAchievementNotification = true });
                Directory.CreateDirectory(ctx.GetGameSteamSettingsPath(TestAppId));

                GameSettingsSnapshot snapshot = ctx.Service.LoadGameSettingsSnapshot(TestAppId);

                Assert.True(snapshot.Overlay.DisableAchievementNotification);
            }
        }

        [Fact]
        public void LoadGameSettingsSnapshot_loads_per_game_app_settings_without_global_merge()
        {
            using (var ctx = new EmulatorConfigTestContext())
            {
                ctx.WriteGameAppIni(
                    TestAppId,
                    "[app::general]\r\nbranch_name=beta\r\nis_beta_branch=1\r\n");

                GameSettingsSnapshot snapshot = ctx.Service.LoadGameSettingsSnapshot(TestAppId);

                Assert.Equal("beta", snapshot.App.BranchName);
                Assert.True(snapshot.App.IsBetaBranch);
            }
        }

        [Fact]
        public void HasUnsavedChanges_returns_true_when_overlay_setting_differs()
        {
            using (var ctx = new EmulatorConfigTestContext())
            {
                GameSettingsSnapshot snapshot = ctx.Service.LoadGameSettingsSnapshot(TestAppId);
                snapshot.Overlay.DisableFriendNotification = true;
                Assert.True(ctx.Service.HasUnsavedChanges(snapshot));
            }
        }

        [Fact]
        public void HasUnsavedChanges_returns_true_when_app_setting_differs()
        {
            using (var ctx = new EmulatorConfigTestContext())
            {
                GameSettingsSnapshot snapshot = ctx.Service.LoadGameSettingsSnapshot(TestAppId);
                snapshot.App.IsBetaBranch = true;
                Assert.True(ctx.Service.HasUnsavedChanges(snapshot));
            }
        }

        [Fact]
        public void SaveModifiedGameSettings_writes_only_changed_overlay_ini()
        {
            using (var ctx = new EmulatorConfigTestContext())
            {
                GameSettingsSnapshot snapshot = ctx.Service.LoadGameSettingsSnapshot(TestAppId);
                snapshot.Overlay.DisableFriendNotification = true;

                SaveResult saveResult = ctx.Service.SaveModifiedGameSettings(TestAppId, snapshot);
                Assert.True(saveResult.IsSuccess, saveResult.ErrorMessage);

                string settingsFolder = ctx.GetGameSteamSettingsPath(TestAppId);
                string overlayPath = Path.Combine(settingsFolder, PathConstants.GoldbergOverlayIniFileName);
                string mainPath = Path.Combine(settingsFolder, PathConstants.GoldbergMainIniFileName);

                Assert.True(File.Exists(overlayPath));
                Assert.Contains("disable_friend_notification=1", File.ReadAllText(overlayPath), StringComparison.OrdinalIgnoreCase);
                Assert.False(File.Exists(mainPath));
            }
        }

        [Fact]
        public void SaveModifiedGameSettings_writes_only_changed_app_ini()
        {
            using (var ctx = new EmulatorConfigTestContext())
            {
                GameSettingsSnapshot snapshot = ctx.Service.LoadGameSettingsSnapshot(TestAppId);
                snapshot.App.BranchName = "beta";
                snapshot.App.IsBetaBranch = true;

                SaveResult saveResult = ctx.Service.SaveModifiedGameSettings(TestAppId, snapshot);
                Assert.True(saveResult.IsSuccess, saveResult.ErrorMessage);

                string settingsFolder = ctx.GetGameSteamSettingsPath(TestAppId);
                string appPath = Path.Combine(settingsFolder, PathConstants.GoldbergAppIniFileName);
                string mainPath = Path.Combine(settingsFolder, PathConstants.GoldbergMainIniFileName);

                Assert.True(File.Exists(appPath));
                string appIni = File.ReadAllText(appPath);
                Assert.Contains("branch_name=beta", appIni, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("is_beta_branch=1", appIni, StringComparison.OrdinalIgnoreCase);
                Assert.False(File.Exists(mainPath));
            }
        }

        [Fact]
        public void LoadGameSettingsSnapshot_does_not_inherit_global_clan_tag_into_per_game_user_settings()
        {
            using (var ctx = new EmulatorConfigTestContext())
            {
                ctx.CfgService.SaveGlobalUserSettings(new UserSettings { ClanTag = "GLOBALCLAN" });
                Directory.CreateDirectory(ctx.GetGameSteamSettingsPath(TestAppId));

                GameSettingsSnapshot snapshot = ctx.Service.LoadGameSettingsSnapshot(TestAppId);

                Assert.Equal(string.Empty, snapshot.User.ClanTag);
            }
        }

        [Fact]
        public void HasUnsavedChanges_returns_true_when_user_language_differs_from_loaded_state()
        {
            using (var ctx = new EmulatorConfigTestContext())
            {
                ctx.CfgService.SaveGlobalUserSettings(new UserSettings { Language = "english" });
                GameSettingsSnapshot snapshot = ctx.Service.LoadGameSettingsSnapshot(TestAppId);
                snapshot.User.Language = "french";
                Assert.True(ctx.Service.HasUnsavedChanges(snapshot));
            }
        }

        [Fact]
        public void SaveModifiedGameSettings_writes_per_game_user_ini_when_language_differs_from_global()
        {
            using (var ctx = new EmulatorConfigTestContext())
            {
                ctx.CfgService.SaveGlobalUserSettings(new UserSettings { Language = "english" });
                GameSettingsSnapshot snapshot = ctx.Service.LoadGameSettingsSnapshot(TestAppId);
                snapshot.User.Language = "french";

                SaveResult saveResult = ctx.Service.SaveModifiedGameSettings(TestAppId, snapshot);
                Assert.True(saveResult.IsSuccess, saveResult.ErrorMessage);

                string settingsFolder = ctx.GetGameSteamSettingsPath(TestAppId);
                string userPath = Path.Combine(settingsFolder, PathConstants.GoldbergUserIniFileName);
                string mainPath = Path.Combine(settingsFolder, PathConstants.GoldbergMainIniFileName);

                Assert.True(File.Exists(userPath));
                Assert.Contains("language=french", File.ReadAllText(userPath), StringComparison.OrdinalIgnoreCase);
                Assert.False(File.Exists(mainPath));
            }
        }

        [Fact]
        public void LoadGameSettingsSnapshot_ignores_per_game_user_saves_section_uses_global_only()
        {
            using (var ctx = new EmulatorConfigTestContext())
            {
                ctx.CfgService.SaveGlobalUserSettings(new UserSettings
                {
                    Language = "english",
                    LocalSavePath = string.Empty,
                    SavesFolderName = ApplicationConstants.DefaultSavesFolderName,
                });
                ctx.WriteGameUserIni(
                    TestAppId,
                    "[user::saves]\r\nlocal_save_path=C:\\wrong\r\nsaves_folder_name=Other\r\n[user::general]\r\nlanguage=english\r\n");

                GameSettingsSnapshot snapshot = ctx.Service.LoadGameSettingsSnapshot(TestAppId);

                Assert.Equal(string.Empty, snapshot.User.LocalSavePath);
                Assert.Equal(ApplicationConstants.DefaultSavesFolderName, snapshot.User.SavesFolderName);
            }
        }

        [Fact]
        public void StripSaveLocationFromPerGameUserIni_removes_user_saves_keys_from_file()
        {
            using (var ctx = new EmulatorConfigTestContext())
            {
                string userPath = Path.Combine(ctx.GetGameSteamSettingsPath(TestAppId), PathConstants.GoldbergUserIniFileName);
                Directory.CreateDirectory(Path.GetDirectoryName(userPath));
                File.WriteAllText(
                    userPath,
                    "[user::general]\r\nlanguage=english\r\n[user::saves]\r\nlocal_save_path=C:\\legacy\r\n");

                SaveResult result = ctx.Service.StripSaveLocationFromPerGameUserIni(TestAppId);
                Assert.True(result.IsSuccess, result.ErrorMessage);

                string content = File.ReadAllText(userPath);
                Assert.DoesNotContain("local_save_path", content, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("language=english", content);
            }
        }

        [Fact]
        public void SaveModifiedGameSettings_deletes_per_game_user_ini_when_user_matches_global()
        {
            using (var ctx = new EmulatorConfigTestContext())
            {
                ctx.CfgService.SaveGlobalUserSettings(new UserSettings { Language = "english" });
                ctx.WriteGameUserIni(TestAppId, "[user::general]\r\nlanguage=french\r\n");

                GameSettingsSnapshot snapshot = ctx.Service.LoadGameSettingsSnapshot(TestAppId);
                Assert.Equal("french", snapshot.User.Language);

                snapshot.User.Language = "english";
                SaveResult saveResult = ctx.Service.SaveModifiedGameSettings(TestAppId, snapshot);
                Assert.True(saveResult.IsSuccess, saveResult.ErrorMessage);

                string userPath = Path.Combine(ctx.GetGameSteamSettingsPath(TestAppId), PathConstants.GoldbergUserIniFileName);
                Assert.False(File.Exists(userPath));
                Assert.False(ctx.Service.HasUnsavedChanges(snapshot));
            }
        }
    }
}
