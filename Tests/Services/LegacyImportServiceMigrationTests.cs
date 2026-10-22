using System.IO;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Services;
using SmartGoldbergEmu.Tests.TestSupport;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    public sealed class LegacyImportServiceMigrationTests
    {
        [Theory]
        [InlineData("Tile", ApplicationConstants.ViewModeTile)]
        [InlineData("Store Capsule", ApplicationConstants.ViewModeTile)]
        [InlineData("Compact Tiles", ApplicationConstants.ViewModeCompactTiles)]
        [InlineData("digital cover", ApplicationConstants.ViewModeCompactTiles)]
        public void TryMigrateApplicationConfigViewMode_rewrites_renamed_values(string legacyValue, string expected)
        {
            string dir = TestFileHelper.CreateTempDirectory("sge-migrate-viewmode-");
            try
            {
                string configPath = Path.Combine(dir, "settings.ini");
                File.WriteAllText(configPath, "[application]\r\nview_mode=" + legacyValue + "\r\n");

                bool migrated = LegacyImportService.TryMigrateApplicationConfigViewMode(configPath, new IniFileService());

                Assert.True(migrated);
                Assert.Contains("view_mode=" + expected, File.ReadAllText(configPath));
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }

        [Theory]
        [InlineData(ApplicationConstants.ViewModeIcons)]
        [InlineData(ApplicationConstants.ViewModeTile)]
        public void TryMigrateApplicationConfigViewMode_leaves_current_values_untouched(string currentValue)
        {
            string dir = TestFileHelper.CreateTempDirectory("sge-migrate-viewmode-current-");
            try
            {
                string configPath = Path.Combine(dir, "settings.ini");
                string original = "[application]\r\nview_mode=" + currentValue + "\r\n";
                File.WriteAllText(configPath, original);

                bool migrated = LegacyImportService.TryMigrateApplicationConfigViewMode(configPath, new IniFileService());

                Assert.False(migrated);
                Assert.Equal(original, File.ReadAllText(configPath));
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }

        [Fact]
        public void TryRemoveLegacyGenerateInterfacesFolder_removes_folder_and_empty_tools_parent()
        {
            string dir = TestFileHelper.CreateTempDirectory("sge-migrate-geninterfaces-");
            try
            {
                string toolsDir = Path.Combine(dir, PathConstants.LauncherDevToolsFolderName);
                string generateDir = Path.Combine(toolsDir, PathConstants.GoldbergGenerateInterfacesToolFolderName);
                Directory.CreateDirectory(generateDir);
                File.WriteAllText(Path.Combine(generateDir, "generate_interfaces_x64.exe"), "stub");

                Assert.True(LegacyImportService.TryRemoveLegacyGenerateInterfacesFolder(dir));

                Assert.False(Directory.Exists(toolsDir));
                Assert.False(LegacyImportService.TryRemoveLegacyGenerateInterfacesFolder(dir));
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }

        [Fact]
        public void TryMigrateUserLaunchOptions_moves_entries_and_keeps_other_user_settings()
        {
            string dir = TestFileHelper.CreateTempDirectory("sge-migrate-launchopts-");
            try
            {
                string gamesDir = Path.Combine(dir, PathConstants.GamesDirectoryFolderName);
                string steamSettings = PathConstants.CombineGameSteamSettingsDirectory(gamesDir, "480");
                Directory.CreateDirectory(steamSettings);
                string legacyIni = Path.Combine(steamSettings, PathConstants.GoldbergUserIniFileName);
                string targetIni = Path.Combine(gamesDir, "480", PathConstants.LauncherUserLaunchOptionsIniFileName);
                File.WriteAllText(legacyIni,
                    "[user::general]\r\nticket=abc\r\n\r\n" +
                    "[user::launch_options]\r\noption_1_name=Safe Mode\r\noption_1_executable=game.exe\r\noption_1_parameters=-safe\r\noption_1_workingdir=bin\r\n" +
                    "option_2_name=DX11\r\noption_2_executable=game_dx11.exe\r\n");

                Assert.Equal(2, LegacyImportService.TryMigrateUserLaunchOptions(gamesDir));

                var iniService = new IniFileService();
                var legacy = iniService.ParseFile(legacyIni);
                Assert.Equal("abc", iniService.GetValue(legacy, "user::general", "ticket"));
                Assert.DoesNotContain(legacy.Lines, l => l.Section == "user::launch_options");

                var target = iniService.ParseFile(targetIni);
                Assert.Equal("Safe Mode", iniService.GetValue(target, "user::launch_options", "option_1_name"));
                Assert.Equal("game.exe", iniService.GetValue(target, "user::launch_options", "option_1_executable"));
                Assert.Equal("-safe", iniService.GetValue(target, "user::launch_options", "option_1_parameters"));
                Assert.Equal("bin", iniService.GetValue(target, "user::launch_options", "option_1_workingdir"));
                Assert.Equal("DX11", iniService.GetValue(target, "user::launch_options", "option_2_name"));
                Assert.Equal("game_dx11.exe", iniService.GetValue(target, "user::launch_options", "option_2_executable"));

                Assert.Equal(0, LegacyImportService.TryMigrateUserLaunchOptions(gamesDir));
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }

        [Fact]
        public void TryMigrateUserLaunchOptions_keeps_existing_target_names_and_deletes_emptied_legacy_file()
        {
            string dir = TestFileHelper.CreateTempDirectory("sge-migrate-launchopts-merge-");
            try
            {
                string gamesDir = Path.Combine(dir, PathConstants.GamesDirectoryFolderName);
                string steamSettings = PathConstants.CombineGameSteamSettingsDirectory(gamesDir, "480");
                Directory.CreateDirectory(steamSettings);
                string legacyIni = Path.Combine(steamSettings, PathConstants.GoldbergUserIniFileName);
                string targetIni = Path.Combine(gamesDir, "480", PathConstants.LauncherUserLaunchOptionsIniFileName);
                File.WriteAllText(targetIni,
                    "[user::launch_options]\r\noption_1_name=Safe Mode\r\noption_1_executable=current.exe\r\n");
                File.WriteAllText(legacyIni,
                    "# header\r\n[user::launch_options]\r\noption_1_name=safe mode\r\noption_1_executable=old.exe\r\n" +
                    "option_2_name=Editor\r\noption_2_executable=editor.exe\r\n");

                Assert.Equal(1, LegacyImportService.TryMigrateUserLaunchOptions(gamesDir));

                Assert.False(File.Exists(legacyIni));
                var iniService = new IniFileService();
                var target = iniService.ParseFile(targetIni);
                Assert.Equal("current.exe", iniService.GetValue(target, "user::launch_options", "option_1_executable"));
                Assert.Equal("Editor", iniService.GetValue(target, "user::launch_options", "option_2_name"));
                Assert.Equal("editor.exe", iniService.GetValue(target, "user::launch_options", "option_2_executable"));
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }

        [Fact]
        public void TryMigrateUserLaunchOptions_leaves_configs_user_ini_without_launch_options_untouched()
        {
            string dir = TestFileHelper.CreateTempDirectory("sge-migrate-launchopts-none-");
            try
            {
                string gamesDir = Path.Combine(dir, PathConstants.GamesDirectoryFolderName);
                string steamSettings = PathConstants.CombineGameSteamSettingsDirectory(gamesDir, "480");
                Directory.CreateDirectory(steamSettings);
                string legacyIni = Path.Combine(steamSettings, PathConstants.GoldbergUserIniFileName);
                string original = "[user::general]\r\nlanguage=german\r\n";
                File.WriteAllText(legacyIni, original);

                Assert.Equal(0, LegacyImportService.TryMigrateUserLaunchOptions(gamesDir));

                Assert.Equal(original, File.ReadAllText(legacyIni));
                Assert.False(File.Exists(Path.Combine(gamesDir, "480", PathConstants.LauncherUserLaunchOptionsIniFileName)));
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }

        [Fact]
        public void TryStripObsoleteP2pPacketSharingMode_strips_global_and_per_game_configs_only_where_present()
        {
            string dir = TestFileHelper.CreateTempDirectory("sge-migrate-p2p-");
            try
            {
                string globalDir = Path.Combine(dir, "global");
                string gamesDir = Path.Combine(dir, PathConstants.GamesDirectoryFolderName);
                string globalIni = Path.Combine(globalDir, PathConstants.GoldbergMainIniFileName);
                string affectedIni = Path.Combine(
                    PathConstants.CombineGameSteamSettingsDirectory(gamesDir, "480"), PathConstants.GoldbergMainIniFileName);
                string untouchedIni = Path.Combine(
                    PathConstants.CombineGameSteamSettingsDirectory(gamesDir, "570"), PathConstants.GoldbergMainIniFileName);
                Directory.CreateDirectory(globalDir);
                Directory.CreateDirectory(Path.GetDirectoryName(affectedIni));
                Directory.CreateDirectory(Path.GetDirectoryName(untouchedIni));

                File.WriteAllText(globalIni, "[main::connectivity]\r\nold_p2p_packet_sharing_mode=1\r\n[main::general]\r\nnew_app_ticket=1\r\n");
                File.WriteAllText(affectedIni, "[main::connectivity]\r\nlisten_port=47584\r\nold_p2p_packet_sharing_mode=0\r\n");
                string untouchedOriginal = "[main::connectivity]\r\nlisten_port=47584\r\n";
                File.WriteAllText(untouchedIni, untouchedOriginal);

                var iniService = new IniFileService();
                int stripped = LegacyImportService.TryStripObsoleteP2pPacketSharingMode(globalDir, gamesDir, iniService);

                Assert.Equal(2, stripped);
                Assert.Null(iniService.GetValue(iniService.ParseFile(globalIni), "main::connectivity", "old_p2p_packet_sharing_mode"));
                Assert.Equal("1", iniService.GetValue(iniService.ParseFile(globalIni), "main::general", "new_app_ticket"));
                var affected = iniService.ParseFile(affectedIni);
                Assert.Null(iniService.GetValue(affected, "main::connectivity", "old_p2p_packet_sharing_mode"));
                Assert.Equal("47584", iniService.GetValue(affected, "main::connectivity", "listen_port"));
                Assert.Equal(untouchedOriginal, File.ReadAllText(untouchedIni));
                Assert.Equal(0, LegacyImportService.TryStripObsoleteP2pPacketSharingMode(globalDir, gamesDir, iniService));
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }

        [Fact]
        public void TryRemoveLegacyGenerateInterfacesFolder_keeps_tools_folder_with_other_content()
        {
            string dir = TestFileHelper.CreateTempDirectory("sge-migrate-geninterfaces-keep-");
            try
            {
                string toolsDir = Path.Combine(dir, PathConstants.LauncherDevToolsFolderName);
                Directory.CreateDirectory(Path.Combine(toolsDir, PathConstants.GoldbergGenerateInterfacesToolFolderName));
                string userFile = Path.Combine(toolsDir, "notes.txt");
                File.WriteAllText(userFile, "keep");

                Assert.True(LegacyImportService.TryRemoveLegacyGenerateInterfacesFolder(dir));

                Assert.False(Directory.Exists(Path.Combine(toolsDir, PathConstants.GoldbergGenerateInterfacesToolFolderName)));
                Assert.True(File.Exists(userFile));
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }
    }
}
