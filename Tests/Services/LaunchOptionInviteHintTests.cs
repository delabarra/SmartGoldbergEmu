using System.Collections.Generic;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    public sealed class LaunchOptionInviteHintTests
    {
        [Fact]
        public void TryMatchLaunchOptionHint_matches_unique_safe_executable()
        {
            var options = new List<LaunchOption>
            {
                new LaunchOption { Description = "Play", Executable = "Game.exe", Type = "default" },
                new LaunchOption { Description = "VR", Executable = "GameVR.exe", Type = "none" }
            };
            var hint = new LanLaunchOptionHint
            {
                Executable = "GameVR.exe",
                Description = "VR",
                Type = "none"
            };

            Assert.True(LaunchOptionService.TryMatchLaunchOptionHint(options, hint, out LaunchOption matched));
            Assert.Equal("GameVR.exe", matched.Executable);
        }

        [Fact]
        public void TryMatchLaunchOptionHint_rejects_restricted_type_even_when_unique()
        {
            var options = new List<LaunchOption>
            {
                new LaunchOption { Description = "Editor", Executable = "Editor.exe", Type = "config" }
            };
            var hint = new LanLaunchOptionHint
            {
                Executable = "Editor.exe",
                Description = "Editor",
                Type = "config"
            };

            Assert.False(LaunchOptionService.TryMatchLaunchOptionHint(options, hint, out _));
        }

        [Fact]
        public void TryMatchLaunchOptionHint_rejects_user_type()
        {
            var options = new List<LaunchOption>
            {
                new LaunchOption
                {
                    Description = "My shortcut",
                    Executable = "custom.exe",
                    Type = SteamPicsKeyNames.LaunchOptionTypeUser
                }
            };
            var hint = new LanLaunchOptionHint
            {
                Executable = "custom.exe",
                Description = "My shortcut",
                Type = SteamPicsKeyNames.LaunchOptionTypeUser
            };

            Assert.False(LaunchOptionService.TryMatchLaunchOptionHint(options, hint, out _));
        }

        [Fact]
        public void TryMatchLaunchOptionHint_rejects_beta_key()
        {
            var options = new List<LaunchOption>
            {
                new LaunchOption { Description = "Beta", Executable = "Game.exe", Type = "default", BetaKey = "beta" }
            };
            var hint = new LanLaunchOptionHint
            {
                Executable = "Game.exe",
                Description = "Beta",
                BetaKey = "beta"
            };

            Assert.False(LaunchOptionService.TryMatchLaunchOptionHint(options, hint, out _));
        }

        [Fact]
        public void TryMatchLaunchOptionHint_normalizes_path_separators()
        {
            var options = new List<LaunchOption>
            {
                new LaunchOption { Description = "x64", Executable = "bin\\x64\\Game.exe", Type = "none" }
            };
            var hint = new LanLaunchOptionHint
            {
                Executable = "bin/x64/Game.exe",
                Description = "x64"
            };

            Assert.True(LaunchOptionService.TryMatchLaunchOptionHint(options, hint, out LaunchOption matched));
            Assert.Equal("bin\\x64\\Game.exe", matched.Executable);
        }

        [Fact]
        public void TryMatchLaunchOptionHint_does_not_filename_fallback_when_hint_has_directory()
        {
            var options = new List<LaunchOption>
            {
                new LaunchOption { Description = "Play", Executable = "Game.exe", Type = "default" }
            };
            var hint = new LanLaunchOptionHint
            {
                Executable = "Win64\\Game.exe",
                Description = "Play"
            };

            Assert.False(LaunchOptionService.TryMatchLaunchOptionHint(options, hint, out _));
        }

        [Fact]
        public void TryMatchLaunchOptionHint_matches_bare_filename_against_relative_local_path()
        {
            var options = new List<LaunchOption>
            {
                new LaunchOption { Description = "Play", Executable = "Win64\\Game.exe", Type = "default" }
            };
            var hint = new LanLaunchOptionHint
            {
                Executable = "Game.exe",
                Description = "Play"
            };

            Assert.True(LaunchOptionService.TryMatchLaunchOptionHint(options, hint, out LaunchOption matched));
            Assert.Equal("Win64\\Game.exe", matched.Executable);
        }

        [Fact]
        public void TryMatchLaunchOptionHint_matches_description_when_executable_empty()
        {
            var options = new List<LaunchOption>
            {
                new LaunchOption { Description = "Play", Executable = "a.exe", Type = "default" },
                new LaunchOption { Description = "VR Mode", Executable = "b.exe", Type = "none" }
            };
            var hint = new LanLaunchOptionHint
            {
                Description = "VR Mode"
            };

            Assert.True(LaunchOptionService.TryMatchLaunchOptionHint(options, hint, out LaunchOption matched));
            Assert.Equal("VR Mode", matched.Description);
        }

        [Fact]
        public void TryMatchLaunchOptionHint_rejects_ambiguous_executable_matches()
        {
            var options = new List<LaunchOption>
            {
                new LaunchOption { Description = "A", Executable = "Game.exe", Type = "default", OsArch = "64" },
                new LaunchOption { Description = "B", Executable = "Game.exe", Type = "default", OsArch = "32" }
            };
            var hint = new LanLaunchOptionHint
            {
                Executable = "Game.exe",
                Type = "default"
            };

            Assert.False(LaunchOptionService.TryMatchLaunchOptionHint(options, hint, out _));
        }

        [Fact]
        public void TryMatchLaunchOptionHint_uses_osarch_to_break_ties()
        {
            var options = new List<LaunchOption>
            {
                new LaunchOption { Description = "A", Executable = "Game.exe", Type = "default", OsArch = "64" },
                new LaunchOption { Description = "B", Executable = "Game.exe", Type = "default", OsArch = "32" }
            };
            var hint = new LanLaunchOptionHint
            {
                Executable = "Game.exe",
                Type = "default",
                OsArch = "64"
            };

            Assert.True(LaunchOptionService.TryMatchLaunchOptionHint(options, hint, out LaunchOption matched));
            Assert.Equal("64", matched.OsArch);
        }

        [Fact]
        public void TryMatchLaunchOptionHint_returns_false_for_prefer_library_path()
        {
            var options = new List<LaunchOption>
            {
                new LaunchOption { Description = "Play", Executable = "Game.exe", Type = "default" }
            };
            var hint = new LanLaunchOptionHint { PreferLibraryPath = true };

            Assert.False(LaunchOptionService.TryMatchLaunchOptionHint(options, hint, out _));
        }

        [Fact]
        public void FromLaunchOption_null_means_prefer_library_path()
        {
            LanLaunchOptionHint hint = LanLaunchOptionHint.FromLaunchOption(null);
            Assert.True(hint.PreferLibraryPath);
            Assert.True(hint.HasMatchableFields);
        }

        [Fact]
        public void FromLaunchOption_copies_identity_fields()
        {
            var option = new LaunchOption
            {
                Description = "Editor",
                Executable = "tools\\edit.exe",
                Type = "config",
                OsArch = "64",
                BetaKey = "beta",
                Parameters = "-edit"
            };

            LanLaunchOptionHint hint = LanLaunchOptionHint.FromLaunchOption(option);
            Assert.False(hint.PreferLibraryPath);
            Assert.Equal("Editor", hint.Description);
            Assert.Equal("tools\\edit.exe", hint.Executable);
            Assert.Equal("config", hint.Type);
            Assert.Equal("64", hint.OsArch);
            Assert.Equal("beta", hint.BetaKey);
        }
    }
}
