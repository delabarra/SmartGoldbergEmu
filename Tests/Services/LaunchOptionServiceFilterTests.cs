using System;
using System.Collections.Generic;
using System.IO;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    public sealed class LaunchOptionServiceFilterTests
    {
        private readonly LaunchOptionService _service = new LaunchOptionService(new SteamProductInfoService(), new ThemeService());

        [Fact]
        public void FilterLaunchOptionsForUi_keeps_user_options_when_restricted_types_excluded()
        {
            var options = new List<LaunchOption>
            {
                new LaunchOption { Description = "Default", Type = "default" },
                new LaunchOption { Description = "Beta branch", Type = SteamPicsKeyNames.LaunchOptionTypeBeta },
                new LaunchOption { Description = "My shortcut", Type = SteamPicsKeyNames.LaunchOptionTypeUser },
            };

            var filtered = _service.FilterLaunchOptionsForUi(options, excludeRestrictedTypes: true);

            Assert.Equal(2, filtered.Count);
            Assert.Contains(filtered, o => o.Description == "Default");
            Assert.Contains(filtered, o => o.Description == "My shortcut");
            Assert.DoesNotContain(filtered, o => o.Description == "Beta branch");
        }

        [Fact]
        public void FilterLaunchOptionsForUi_returns_all_options_when_not_restricted()
        {
            var options = new List<LaunchOption>
            {
                new LaunchOption { Description = "Default", Type = "default" },
                new LaunchOption { Description = "Beta branch", Type = SteamPicsKeyNames.LaunchOptionTypeBeta },
            };

            var filtered = _service.FilterLaunchOptionsForUi(options, excludeRestrictedTypes: false);

            Assert.Equal(2, filtered.Count);
        }

        [Fact]
        public void FilterLaunchOptionsForUi_orders_default_before_other_types()
        {
            var options = new List<LaunchOption>
            {
                new LaunchOption { Description = "Editor", Type = "config" },
                new LaunchOption { Description = "Play", Type = "default" },
            };

            var filtered = _service.FilterLaunchOptionsForUi(options, excludeRestrictedTypes: false);

            Assert.Equal("Play", filtered[0].Description);
            Assert.Equal("Editor", filtered[1].Description);
        }

        [Fact]
        public void FilterLaunchOptionsForUi_returns_empty_list_for_null_input()
        {
            var filtered = _service.FilterLaunchOptionsForUi(null, excludeRestrictedTypes: true);
            Assert.Empty(filtered);
        }

        [Fact]
        public void FilterLaunchOptionsForUi_excludes_type_none_even_when_showing_extra()
        {
            var options = new List<LaunchOption>
            {
                new LaunchOption { Executable = "StreetsOfRogue.exe", Type = SteamPicsKeyNames.LaunchOptionTypeNone, OsArch = "64" },
                new LaunchOption { Executable = "StreetsOfRogue32.exe", Type = SteamPicsKeyNames.LaunchOptionTypeNone, OsArch = "32" },
                new LaunchOption { Description = "Play", Type = "default", Executable = "play.exe" },
            };

            var filtered = _service.FilterLaunchOptionsForUi(options, excludeRestrictedTypes: false);

            Assert.Single(filtered);
            Assert.Equal("Play", filtered[0].Description);
        }

        [Fact]
        public void PickBestAutomaticLaunchOption_prefers_native_osarch_among_type_none()
        {
            var options = new List<LaunchOption>
            {
                new LaunchOption { Executable = "StreetsOfRogue32.exe", Type = SteamPicsKeyNames.LaunchOptionTypeNone, OsArch = "32" },
                new LaunchOption { Executable = "StreetsOfRogue.exe", Type = SteamPicsKeyNames.LaunchOptionTypeNone, OsArch = "64" },
            };

            var picked = LaunchOptionService.PickBestAutomaticLaunchOption(options);

            Assert.NotNull(picked);
            if (Environment.Is64BitOperatingSystem)
                Assert.Equal("StreetsOfRogue.exe", picked.Executable);
            else
                Assert.Equal("StreetsOfRogue32.exe", picked.Executable);
        }

        [Fact]
        public void ShouldPreferStoredExecutableOverAutomaticOption_when_type_none_differs_from_library_path()
        {
            string root = Path.Combine(Path.GetTempPath(), "sge_launch_opt_" + Guid.NewGuid().ToString("N"));
            string x64Dir = Path.Combine(root, "x64");
            Directory.CreateDirectory(x64Dir);
            string rootExe = Path.Combine(root, "Game.exe");
            string x64Exe = Path.Combine(x64Dir, "Game.exe");
            File.WriteAllBytes(rootExe, new byte[] { 0x4D, 0x5A });
            File.WriteAllBytes(x64Exe, new byte[] { 0x4D, 0x5A });

            try
            {
                var game = new GameConfig
                {
                    AppId = 1,
                    StartFolder = root,
                    Path = @"x64\Game.exe",
                };
                var automatic = new LaunchOption
                {
                    Executable = "Game.exe",
                    Type = SteamPicsKeyNames.LaunchOptionTypeNone,
                };

                Assert.True(LaunchOptionService.ShouldPreferStoredExecutableOverAutomaticOption(game, automatic));
                Assert.False(LaunchOptionService.ShouldPreferStoredExecutableOverAutomaticOption(
                    game,
                    new LaunchOption { Executable = "Game.exe", Type = "default" }));
            }
            finally
            {
                try { Directory.Delete(root, recursive: true); } catch { }
            }
        }
    }
}
