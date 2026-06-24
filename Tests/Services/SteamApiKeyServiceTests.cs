using SmartGoldbergEmu.Services;
using SmartGoldbergEmu.Tests.Fakes;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    public sealed class SteamApiKeyServiceTests
    {
        private const string ValidKey = "0123456789ABCDEF0123456789ABCDEF";

        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("short", false)]
        [InlineData("0123456789abcdef0123456789abcdef", false)]
        [InlineData("0123456789ABCDEF0123456789ABCXY", false)]
        [InlineData(ValidKey, true)]
        public void IsValidApiKeyFormat_enforces_32_uppercase_alphanumeric(string key, bool expected)
        {
            Assert.Equal(expected, SteamApiKeyService.IsValidApiKeyFormat(key));
        }

        [Fact]
        public void TryGetValidFormatKey_returns_false_when_missing_or_invalid()
        {
            var registry = new FakeRegistryService();
            var service = new SteamApiKeyService(registry);
            Assert.False(service.TryGetValidFormatKey(out string key));
            Assert.Equal(string.Empty, key);

            registry.SetSteamApiKey("not-valid");
            Assert.False(service.TryGetValidFormatKey(out key));
            Assert.Equal("not-valid", key);
        }

        [Fact]
        public void TryGetValidFormatKey_returns_true_for_valid_stored_key()
        {
            var registry = new FakeRegistryService();
            registry.SetSteamApiKey(ValidKey);
            var service = new SteamApiKeyService(registry);
            Assert.True(service.TryGetValidFormatKey(out string key));
            Assert.Equal(ValidKey, key);
        }

        [Fact]
        public void GetApiKey_reads_from_injected_registry()
        {
            var registry = new FakeRegistryService();
            registry.SetSteamApiKey(ValidKey);
            var service = new SteamApiKeyService(registry);
            Assert.Equal(ValidKey, service.GetApiKey());
            Assert.True(service.HasApiKey());
            Assert.True(service.HasValidFormat());
        }

        [Fact]
        public void SetApiKey_rejects_invalid_format_without_persisting()
        {
            var registry = new FakeRegistryService();
            var service = new SteamApiKeyService(registry);
            var result = service.SetApiKey("not-a-valid-key");
            Assert.False(result.IsValid);
            Assert.Equal(string.Empty, registry.GetSteamApiKey());
        }

        [Fact]
        public void SetApiKey_strips_spaces_and_persists_valid_key()
        {
            var registry = new FakeRegistryService();
            var service = new SteamApiKeyService(registry);
            string spaced = ValidKey.Substring(0, 16) + " " + ValidKey.Substring(16);
            var result = service.SetApiKey(spaced);
            Assert.True(result.IsValid);
            Assert.Equal(ValidKey, registry.GetSteamApiKey());
        }
    }
}
