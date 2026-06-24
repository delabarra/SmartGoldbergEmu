using System.Collections.Generic;
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
    }
}
