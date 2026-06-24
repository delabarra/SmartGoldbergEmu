using System.Collections.Generic;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Tests.Fakes
{
    public sealed class FakeRegistryService : IRegistryService
    {
        private string _apiKey = string.Empty;
        private readonly Dictionary<ulong, (string Ticket, string AltSteamId)> _base64ByAppId =
            new Dictionary<ulong, (string, string)>();
        private Dictionary<string, string> _steamIdProfiles = new Dictionary<string, string>();

        public string GetSteamApiKey() => _apiKey ?? string.Empty;

        public ValidationResult SetSteamApiKey(string apiKey)
        {
            _apiKey = apiKey ?? string.Empty;
            return ValidationResult.Success();
        }

        public bool HasSteamApiKey() => !string.IsNullOrEmpty(_apiKey);

        public ValidationResult RemoveSteamApiKey()
        {
            _apiKey = string.Empty;
            return ValidationResult.Success();
        }

        public ValidationResult SetBase64Token(ulong appId, string ticket, string altSteamId)
        {
            _base64ByAppId[appId] = (ticket ?? string.Empty, altSteamId ?? string.Empty);
            return ValidationResult.Success();
        }

        public string GetBase64Ticket(ulong appId)
        {
            return _base64ByAppId.TryGetValue(appId, out var pair) ? pair.Ticket : string.Empty;
        }

        public string GetBase64AltSteamId(ulong appId)
        {
            return _base64ByAppId.TryGetValue(appId, out var pair) ? pair.AltSteamId : string.Empty;
        }

        public Dictionary<string, string> LoadSteamIdProfiles() =>
            new Dictionary<string, string>(_steamIdProfiles);

        public ValidationResult SaveSteamIdProfiles(Dictionary<string, string> profiles)
        {
            _steamIdProfiles = profiles == null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(profiles);
            return ValidationResult.Success();
        }

        public ValidationResult UpsertSteamIdProfile(string steamId, string name)
        {
            if (string.IsNullOrWhiteSpace(steamId))
                return ValidationResult.Failure("Steam ID is required.");
            _steamIdProfiles[steamId] = name ?? string.Empty;
            return ValidationResult.Success();
        }

        public ValidationResult RemoveSteamIdProfile(string steamId)
        {
            if (string.IsNullOrWhiteSpace(steamId))
                return ValidationResult.Failure("Steam ID is required.");
            _steamIdProfiles.Remove(steamId);
            return ValidationResult.Success();
        }
    }
}
