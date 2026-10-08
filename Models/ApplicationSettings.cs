using System;
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

        // When true, removable SteamStub is unpacked automatically on add/launch without a confirm dialog.
        public bool AutoHandleSteamStubs { get; set; }

        public bool LogosViewDropShadow { get; set; }

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
            AutoHandleSteamStubs = false;
            LogosViewDropShadow = true;
        }
    }
}
