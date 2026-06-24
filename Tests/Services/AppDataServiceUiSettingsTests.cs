using System.IO;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;
using SmartGoldbergEmu.Tests.TestSupport;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    public sealed class AppDataServiceUiSettingsTests
    {
        [Fact]
        public void TryMigrateUiSettingsFromConfig_moves_theme_and_window_out_of_cfg()
        {
            string dir = TestFileHelper.CreateTempDirectory("sge-ui-settings-");
            try
            {
                string configPath = Path.Combine(dir, "settings.ini");
                string uiSettingsPath = Path.Combine(dir, "ui_settings.ini");
                File.WriteAllText(configPath,
                    "[application]\r\n" +
                    "view_mode=Icons\r\n" +
                    "theme_mode=Dark\r\n" +
                    "\r\n" +
                    "[window]\r\n" +
                    "size=330,450\r\n" +
                    "location=826,284\r\n" +
                    "state=Normal\r\n");

                var service = new AppDataService(configPath, dir, uiSettingsPath, new IniFileService());
                service.TryMigrateUiSettingsFromConfig();

                var settings = service.LoadApplicationSettings();
                Assert.Equal(ThemeMode.Dark, settings.ThemeMode);
                Assert.Equal(330, settings.WindowState.Size.Width);
                Assert.Equal(450, settings.WindowState.Size.Height);
                Assert.Equal(826, settings.WindowState.Location.X);
                Assert.Equal(284, settings.WindowState.Location.Y);

                string cfgText = File.ReadAllText(configPath);
                Assert.DoesNotContain("theme_mode", cfgText);
                Assert.DoesNotContain("[window]", cfgText);
                Assert.Contains("view_mode=Icons", cfgText);

                Assert.True(File.Exists(uiSettingsPath));
                string uiText = File.ReadAllText(uiSettingsPath);
                Assert.Contains("theme_mode=Dark", uiText);
                Assert.Contains("size=330,450", uiText);
                Assert.Contains("location=826,284", uiText);
                Assert.Contains("state=Normal", uiText);
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }

        [Fact]
        public void SaveApplicationSettings_writes_ui_values_to_uisettings_only()
        {
            string dir = TestFileHelper.CreateTempDirectory("sge-ui-settings-save-");
            try
            {
                string configPath = Path.Combine(dir, "settings.ini");
                string uiSettingsPath = Path.Combine(dir, "ui_settings.ini");
                var service = new AppDataService(configPath, dir, uiSettingsPath, new IniFileService());

                var settings = new ApplicationSettings
                {
                    ViewMode = ApplicationConstants.ViewModeIcons,
                    ThemeMode = ThemeMode.Light,
                    WindowState = new WindowState
                    {
                        Size = new System.Drawing.Size(400, 500),
                        Location = new System.Drawing.Point(100, 200)
                    }
                };

                var result = service.SaveApplicationSettings(settings);
                Assert.True(result.IsValid);

                string cfgText = File.ReadAllText(configPath);
                Assert.DoesNotContain("theme_mode", cfgText);
                Assert.DoesNotContain("[window]", cfgText);
                Assert.Contains("view_mode=" + ApplicationConstants.ViewModeIcons, cfgText);

                string uiText = File.ReadAllText(uiSettingsPath);
                Assert.Contains("theme_mode=Light", uiText);
                Assert.Contains("size=400,500", uiText);
                Assert.Contains("location=100,200", uiText);
                Assert.Contains("state=Normal", uiText);
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }
        [Fact]
        public void LoadApplicationSettings_reads_ui_values_when_cfg_missing()
        {
            string dir = TestFileHelper.CreateTempDirectory("sge-ui-settings-load-");
            try
            {
                string configPath = Path.Combine(dir, "settings.ini");
                string uiSettingsPath = Path.Combine(dir, "ui_settings.ini");
                File.WriteAllText(uiSettingsPath,
                    "[application]\r\n" +
                    "theme_mode=Dark\r\n" +
                    "\r\n" +
                    "[window]\r\n" +
                    "size=330,450\r\n" +
                    "location=1123,476\r\n" +
                    "state=Normal\r\n");

                var service = new AppDataService(configPath, dir, uiSettingsPath, new IniFileService());
                var settings = service.LoadApplicationSettings();

                Assert.Equal(ThemeMode.Dark, settings.ThemeMode);
                Assert.Equal(330, settings.WindowState.Size.Width);
                Assert.Equal(450, settings.WindowState.Size.Height);
                Assert.Equal(1123, settings.WindowState.Location.X);
                Assert.Equal(476, settings.WindowState.Location.Y);
                Assert.True(service.HasPersistedWindowLayout());
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }

        [Fact]
        public void LoadApplicationSettings_reads_window_from_ui_settings_when_cfg_has_no_window_section()
        {
            string dir = TestFileHelper.CreateTempDirectory("sge-ui-settings-cfg-no-window-");
            try
            {
                string configPath = Path.Combine(dir, "settings.ini");
                string uiSettingsPath = Path.Combine(dir, "ui_settings.ini");
                File.WriteAllText(configPath,
                    "[application]\r\n" +
                    "view_mode=Icons\r\n" +
                    "is_first_run=false\r\n");
                File.WriteAllText(uiSettingsPath,
                    "[window]\r\n" +
                    "size=330,450\r\n" +
                    "location=1123,476\r\n" +
                    "state=Normal\r\n");

                var service = new AppDataService(configPath, dir, uiSettingsPath, new IniFileService());
                var settings = service.LoadApplicationSettings();

                Assert.Equal(1123, settings.WindowState.Location.X);
                Assert.Equal(476, settings.WindowState.Location.Y);
                Assert.True(service.HasPersistedWindowLayout());
                Assert.True(service.HasPersistedWindowLocation());
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }

        [Fact]
        public void HasPersistedWindowLocation_is_false_when_only_size_is_saved()
        {
            string dir = TestFileHelper.CreateTempDirectory("sge-ui-settings-no-location-");
            try
            {
                string configPath = Path.Combine(dir, "settings.ini");
                string uiSettingsPath = Path.Combine(dir, "ui_settings.ini");
                File.WriteAllText(uiSettingsPath,
                    "[window]\r\n" +
                    "size=330,450\r\n" +
                    "state=Normal\r\n");

                var service = new AppDataService(configPath, dir, uiSettingsPath, new IniFileService());

                Assert.True(service.HasPersistedWindowLayout());
                Assert.False(service.HasPersistedWindowLocation());
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }
    }
}
