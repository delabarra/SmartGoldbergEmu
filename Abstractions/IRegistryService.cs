using System.Collections.Generic;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Abstractions
{
    public interface IRegistryService
    {
        string GetSteamApiKey();
        ValidationResult SetSteamApiKey(string apiKey);
        bool HasSteamApiKey();
        ValidationResult RemoveSteamApiKey();
        ValidationResult SetBase64Token(ulong appId, string ticket, string altSteamId);
        string GetBase64Ticket(ulong appId);
        string GetBase64AltSteamId(ulong appId);
        Dictionary<string, string> LoadSteamIdProfiles();
        ValidationResult SaveSteamIdProfiles(Dictionary<string, string> profiles);
        ValidationResult UpsertSteamIdProfile(string steamId, string name);
        ValidationResult RemoveSteamIdProfile(string steamId);
    }
}
