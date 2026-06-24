using SmartGoldbergEmu.Models;
using Xunit;

namespace SmartGoldbergEmu.Tests.Models
{
    public sealed class GoldbergLaunchModeAvailabilityTests
    {
        [Fact]
        public void IsAvailable_reflects_individual_flags()
        {
            var availability = new GoldbergLaunchModeAvailability
            {
                SteamClientAvailable = true,
                StandardSteamApiAvailable = false,
                SteamDllBesideExeAvailable = true,
            };

            Assert.True(availability.IsAvailable(GoldbergLaunchMode.SteamClient));
            Assert.False(availability.IsAvailable(GoldbergLaunchMode.StandardSteamApi));
            Assert.True(availability.IsAvailable(GoldbergLaunchMode.SteamDllBesideExe));
        }

        [Fact]
        public void GetFirstAvailable_prefers_steam_client_then_regular_then_steam_dll()
        {
            Assert.Equal(
                GoldbergLaunchMode.SteamClient,
                new GoldbergLaunchModeAvailability
                {
                    SteamClientAvailable = true,
                    StandardSteamApiAvailable = true,
                    SteamDllBesideExeAvailable = true,
                }.GetFirstAvailable());

            Assert.Equal(
                GoldbergLaunchMode.StandardSteamApi,
                new GoldbergLaunchModeAvailability
                {
                    SteamClientAvailable = false,
                    StandardSteamApiAvailable = true,
                    SteamDllBesideExeAvailable = true,
                }.GetFirstAvailable());

            Assert.Equal(
                GoldbergLaunchMode.SteamDllBesideExe,
                new GoldbergLaunchModeAvailability
                {
                    SteamClientAvailable = false,
                    StandardSteamApiAvailable = false,
                    SteamDllBesideExeAvailable = true,
                }.GetFirstAvailable());
        }

        [Fact]
        public void ResolveAvailable_returns_preferred_when_available_otherwise_first_available()
        {
            var availability = new GoldbergLaunchModeAvailability
            {
                SteamClientAvailable = true,
                StandardSteamApiAvailable = false,
                SteamDllBesideExeAvailable = true,
            };

            Assert.Equal(
                GoldbergLaunchMode.SteamDllBesideExe,
                availability.ResolveAvailable(GoldbergLaunchMode.SteamDllBesideExe));
            Assert.Equal(
                GoldbergLaunchMode.SteamClient,
                availability.ResolveAvailable(GoldbergLaunchMode.StandardSteamApi));
        }
    }
}
