using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    public static class GameCredentialPersistenceService
    {
        public static void ApplyRegistryFallbackForDisplay(ulong appId, GameSettingsSnapshot snapshot, IRegistryService registry)
        {
            if (snapshot?.User == null || appId == 0 || registry == null)
                return;

            if (string.IsNullOrEmpty(snapshot.User.Ticket))
            {
                string fromRegistry = registry.GetBase64Ticket(appId);
                if (!string.IsNullOrEmpty(fromRegistry))
                    snapshot.User.Ticket = fromRegistry;
            }

            if (string.IsNullOrEmpty(snapshot.User.AltSteamId))
            {
                string fromRegistry = registry.GetBase64AltSteamId(appId);
                if (!string.IsNullOrEmpty(fromRegistry))
                    snapshot.User.AltSteamId = fromRegistry;
            }
        }

        public static void PersistTicketAndAltSteamId(
            ulong appId,
            string ticket,
            string altSteamId,
            IRegistryService registry,
            EmulatorConfigService emulatorConfig)
        {
            if (appId == 0 || registry == null || emulatorConfig == null)
                return;

            string normalizedTicket = ticket?.Trim() ?? string.Empty;
            string normalizedAlt = altSteamId?.Trim() ?? string.Empty;

            emulatorConfig.SavePerGameTicketAndAltSteamId(appId, normalizedTicket, normalizedAlt);

            ValidationResult registryResult = registry.SetBase64Token(appId, normalizedTicket, normalizedAlt);
            if (!registryResult.IsValid)
                ServiceLocator.LogService?.LogWarning($"Failed to save base64 token to registry: {registryResult.ErrorMessage}");
        }

        public static void ClearTicketAndAltSteamId(ulong appId, IRegistryService registry, EmulatorConfigService emulatorConfig)
        {
            PersistTicketAndAltSteamId(appId, string.Empty, string.Empty, registry, emulatorConfig);
        }
    }
}
