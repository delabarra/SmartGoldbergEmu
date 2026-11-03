using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using SmartGoldbergEmu.Constants;

namespace SmartGoldbergEmu.Models
{
    public class ApplicationSettings
    {
        public ThemeMode ThemeMode { get; set; }

        // Store Banner, Library Cover, Icons, Logos, or Details.
        public string ViewMode { get; set; }

        // Name, AppId, or None.
        public string SortBy { get; set; }

        // Asc or Desc.
        public string SortDirection { get; set; }

        public bool AutoUpdate { get; set; }

        public bool IsFirstRun { get; set; }

        public WindowState WindowState { get; set; }

        // Comma-separated column names.
        public string DetailsColumnOrder { get; set; }

        // Comma-separated pixel widths for Name, App ID, Path.
        public string DetailsColumnWidths { get; set; }

        // When false, beta branches (config/BetaKey), config tools, and dev/beta launch types are hidden.
        public bool FullLaunchOptions { get; set; }

        public bool LogosViewDropShadow { get; set; }

        // LAN presence: advertise/listen when true; stop all presence traffic when false.
        public bool LanPlayersOnline { get; set; }

        // Preferred Online / Away / Busy / Hidden while LanPlayersOnline (persists). Offline is LanPlayersOnline=false. Auto-Away overlays Online only at runtime.
        public LanPersonaState LanPersonaState { get; set; }

        public string LanCustomStatus { get; set; }

        // Local-only favorite Steam64 ids.
        public List<string> LanFavoriteSteamIds { get; set; }

        // Local-only muted Steam64 ids (suppress chat popups; still listed).
        public List<string> LanMutedSteamIds { get; set; }

        // Local-only Steam64 ids blocked from LAN chat ("Ignore user").
        public List<string> LanChatBlockedSteamIds { get; set; }

        // Local-only Steam64 ids already accepted for LAN chat (skip first-message gate).
        public List<string> LanChatTrustedSteamIds { get; set; }

        // LAN Players list window was open when the app last quit (ui_settings.ini).
        public bool LanListOpen { get; set; }

        // LAN Players list size/position (ui_settings.ini).
        public WindowState LanListWindowState { get; set; }

        // Shared LAN chat window size (ui_settings.ini). Location is not restored (each chat centers on parent).
        public WindowState LanChatWindowState { get; set; }

        public ApplicationSettings()
        {
            ThemeMode = ThemeMode.System;
            ViewMode = ApplicationConstants.ViewModeDefault;
            SortBy = ApplicationConstants.SortByDefault;
            SortDirection = ApplicationConstants.SortDirectionDefault;
            AutoUpdate = true;
            IsFirstRun = true;
            WindowState = new WindowState();
            DetailsColumnOrder = ApplicationConstants.DefaultColumnOrder;
            DetailsColumnWidths = ApplicationConstants.DefaultDetailsColumnWidths;
            FullLaunchOptions = false; // Falls back to the game executable when filtering leaves no options.
            LogosViewDropShadow = true;
            LanPlayersOnline = true;
            LanPersonaState = LanPersonaState.Online;
            LanCustomStatus = string.Empty;
            LanFavoriteSteamIds = new List<string>();
            LanMutedSteamIds = new List<string>();
            LanChatBlockedSteamIds = new List<string>();
            LanChatTrustedSteamIds = new List<string>();
            LanListOpen = false;
            // Null until read from ui_settings.ini or saved from LanPlayersForm (avoid writing defaults on unrelated UI saves).
            LanListWindowState = null;
            LanChatWindowState = null;
        }
    }
}
