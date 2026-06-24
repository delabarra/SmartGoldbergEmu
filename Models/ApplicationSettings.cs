using System;
using System.Xml.Serialization;
using SmartGoldbergEmu.Constants;

namespace SmartGoldbergEmu.Models
{
    /// <summary>
    /// Represents SmartGoldbergEmu application settings and preferences.
    /// </summary>
    public class ApplicationSettings
    {

        /// <summary>
        /// Gets or sets the theme mode (Light, Dark, System).
        /// </summary>
        public ThemeMode ThemeMode { get; set; }

        /// <summary>
        /// Gets or sets the view mode (Store Banner, Library Cover, Icons, Logos, Details).
        /// </summary>
        public string ViewMode { get; set; }

        /// <summary>
        /// Gets or sets the sort field (Name, AppId, None).
        /// </summary>
        public string SortBy { get; set; }

        /// <summary>
        /// Gets or sets the sort direction (Asc, Desc).
        /// </summary>
        public string SortDirection { get; set; }

        /// <summary>
        /// Gets or sets whether to automatically check for updates.
        /// </summary>
        public bool AutoUpdate { get; set; }

        /// <summary>
        /// Gets or sets whether this is the first run of the application.
        /// </summary>
        public bool IsFirstRun { get; set; }

        /// <summary>
        /// Gets or sets the window state information.
        /// </summary>
        public WindowState WindowState { get; set; }

        /// <summary>
        /// Gets or sets the Details view column order as a comma-separated list of column names.
        /// </summary>
        public string DetailsColumnOrder { get; set; }

        /// <summary>
        /// Gets or sets Details data column widths (px) as comma-separated Name, App ID, Path.
        /// </summary>
        public string DetailsColumnWidths { get; set; }

        /// <summary>
        /// Gets or sets whether to show all Steam (PICS) launch options. When false, beta branches (config/BetaKey), config tools, and dev/beta types are hidden.
        /// </summary>
        public bool FullLaunchOptions { get; set; }

        /// <summary>
        /// Gets or sets whether Logos view tiles use a drop shadow in the ImageList (display only).
        /// </summary>
        public bool LogosViewDropShadow { get; set; }

        /// <summary>
        /// Full path to the user-selected Steamless.CLI.exe (optional).
        /// </summary>
        public string SteamlessCliPath { get; set; }

        /// <summary>
        /// Initializes a new instance of the ApplicationSettings class with default values.
        /// </summary>
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
            FullLaunchOptions = false; // Default: filter beta branches and restricted types; use game executable if none left
            LogosViewDropShadow = true;
        }
    }
}
