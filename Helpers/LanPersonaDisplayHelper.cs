using System;
using System.Drawing;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Helpers
{
    // Shared LAN list + chat persona colors/status. Graphite OfflineColor is local-only (self Hidden/Offline).
    public static class LanPersonaDisplayHelper
    {
        public static void ResolvePersonaColors(
            LanPresencePeer peer,
            ThemeColors colors,
            bool isSelf,
            bool channelOnline,
            out Color nameColor,
            out Color statusColor)
        {
            if (colors == null)
            {
                nameColor = SystemColors.ControlText;
                statusColor = SystemColors.GrayText;
                return;
            }

            // Graphite is only for your own Offline / Hidden row — peers never see those as distinct wire states.
            if (isSelf && (!channelOnline || (peer != null && peer.PersonaState == LanPersonaState.Hidden)))
            {
                nameColor = colors.OfflineColor;
                statusColor = colors.OfflineColor;
                return;
            }

            // Absent peers / last seen stay muted secondary (not graphite).
            if (peer == null || !peer.IsPresent)
            {
                nameColor = colors.StatusTextSecondary;
                statusColor = colors.StatusTextSecondary;
                return;
            }

            if (peer.PersonaState == LanPersonaState.Busy)
            {
                nameColor = colors.ErrorColor;
                statusColor = colors.ErrorColor;
                return;
            }

            if (peer.PersonaState == LanPersonaState.Away)
            {
                nameColor = colors.WarningColor;
                statusColor = colors.WarningColor;
                return;
            }

            if (peer.IsPlaying)
            {
                nameColor = colors.SuccessColor;
                statusColor = colors.SuccessColor;
                return;
            }

            nameColor = colors.StatusTextAccent;
            statusColor = colors.StatusTextAccent;
        }

        public static string FormatPersonaStatusLine(LanPresencePeer peer, bool isSelf, bool channelOnline)
        {
            // Offline label is local-only; peers who stop advertising just age into Last seen.
            if (isSelf && !channelOnline)
                return "Offline";

            if (peer == null)
                return isSelf ? "Offline" : "Last seen unknown";

            if (!isSelf && !peer.IsPresent)
                return FormatLastSeen(peer.LastSeenUtc);

            // In-game replaces Online text; Away/Busy append " - (Away)" / " - (Busy)".
            if (peer.IsPlaying)
            {
                string game = !string.IsNullOrWhiteSpace(peer.GameName)
                    ? peer.GameName.Trim()
                    : ("App " + peer.AppId);
                return AppendAwayBusyTag(game, peer.PersonaState);
            }

            string state;
            if (isSelf && peer.PersonaState == LanPersonaState.Hidden)
                state = "Hidden";
            else if (peer.PersonaState == LanPersonaState.Busy)
                state = "Busy";
            else if (peer.PersonaState == LanPersonaState.Away)
                state = "Away";
            else
                state = "Online";

            if (peer.IsLookingForPlay)
            {
                string game = !string.IsNullOrWhiteSpace(peer.LookingForPlayGameName)
                    ? peer.LookingForPlayGameName.Trim()
                    : ("App " + peer.LookingForPlayAppId);
                state = state + " · Looking to play " + game;
            }

            if (!string.IsNullOrWhiteSpace(peer.CustomStatus))
                state = state + " — " + peer.CustomStatus.Trim();

            return AppendAwayBusyTag(state, peer.PersonaState);
        }

        // Playing / long status lines get " - (Away)" / " - (Busy)"; bare Away/Busy stay as-is.
        private static string AppendAwayBusyTag(string line, LanPersonaState state)
        {
            if (string.IsNullOrEmpty(line))
                return line ?? string.Empty;

            if (state == LanPersonaState.Away)
            {
                if (string.Equals(line, "Away", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("Away ", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("Away·", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("Away —", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("Away ·", StringComparison.OrdinalIgnoreCase))
                    return line;
                return line + " - (Away)";
            }

            if (state == LanPersonaState.Busy)
            {
                if (string.Equals(line, "Busy", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("Busy ", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("Busy·", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("Busy —", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("Busy ·", StringComparison.OrdinalIgnoreCase))
                    return line;
                return line + " - (Busy)";
            }

            return line;
        }

        public static string FormatLastSeen(DateTime lastSeenUtc)
        {
            TimeSpan age = DateTime.UtcNow - lastSeenUtc;
            if (age.TotalSeconds < 45)
                return "Last seen just now";
            if (age.TotalMinutes < 60)
                return "Last seen " + Math.Max(1, (int)age.TotalMinutes) + "m ago";
            if (age.TotalHours < 48)
                return "Last seen " + Math.Max(1, (int)age.TotalHours) + "h ago";
            return "Last seen " + Math.Max(1, (int)age.TotalDays) + "d ago";
        }
    }
}
