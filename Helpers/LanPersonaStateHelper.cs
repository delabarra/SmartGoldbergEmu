using System;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Helpers
{
    // Settings INI + presence wire vocabulary for LanPersonaState.
    public static class LanPersonaStateHelper
    {
        public static LanPersonaState ParseSettings(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return LanPersonaState.Online;
            value = value.Trim();
            if (string.Equals(value, LanPresenceConstants.PersonaAway, StringComparison.OrdinalIgnoreCase))
                return LanPersonaState.Away;
            if (string.Equals(value, LanPresenceConstants.PersonaBusy, StringComparison.OrdinalIgnoreCase))
                return LanPersonaState.Busy;
            if (string.Equals(value, LanPresenceConstants.PersonaHidden, StringComparison.OrdinalIgnoreCase))
                return LanPersonaState.Hidden;
            return LanPersonaState.Online;
        }

        public static string FormatSettings(LanPersonaState state)
        {
            switch (state)
            {
                case LanPersonaState.Away:
                    return LanPresenceConstants.PersonaAway;
                case LanPersonaState.Busy:
                    return LanPresenceConstants.PersonaBusy;
                case LanPersonaState.Hidden:
                    return LanPresenceConstants.PersonaHidden;
                default:
                    return LanPresenceConstants.PersonaOnline;
            }
        }

        // Wire only advertises Online/Away/Busy; Hidden is local (do not advertise).
        public static string FormatWire(LanPersonaState state)
        {
            if (state == LanPersonaState.Busy)
                return LanPresenceConstants.PersonaBusy;
            if (state == LanPersonaState.Away)
                return LanPresenceConstants.PersonaAway;
            return LanPresenceConstants.PersonaOnline;
        }

        public static LanPersonaState ParseWire(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return LanPersonaState.Online;
            value = value.Trim();
            if (string.Equals(value, LanPresenceConstants.PersonaBusy, StringComparison.OrdinalIgnoreCase))
                return LanPersonaState.Busy;
            if (string.Equals(value, LanPresenceConstants.PersonaAway, StringComparison.OrdinalIgnoreCase))
                return LanPersonaState.Away;
            return LanPersonaState.Online;
        }
    }
}
