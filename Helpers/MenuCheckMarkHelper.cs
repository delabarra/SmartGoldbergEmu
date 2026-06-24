using System.Windows.Forms;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Helpers
{
    /// <summary>
    /// Helper class for updating menu check marks based on current application state.
    /// </summary>
    public static class MenuCheckMarkHelper
    {
        /// <summary>
        /// Updates the check marks for view menu items based on the current view mode.
        /// </summary>
        /// <param name="appDataService">The app data service to get view mode from.</param>
        /// <param name="tilesViewMenuItem">Main menu tiles view item.</param>
        /// <param name="compactTilesViewMenuItem">Main menu compact tiles view item.</param>
        /// <param name="logosViewMenuItem">Main menu logos view item.</param>
        /// <param name="iconViewMenuItem">Main menu icon view item.</param>
        /// <param name="detailsMenuItem">Main menu details view item.</param>
        /// <param name="tileContextMenuItem">Context menu tile view item.</param>
        /// <param name="capsuleTilesContextMenuItem">Context menu capsule tiles view item.</param>
        /// <param name="logosContextMenuItem">Context menu logos view item.</param>
        /// <param name="largeIconsContextMenuItem">Context menu large icons view item.</param>
        /// <param name="detailsContextMenuItem">Context menu details view item.</param>
        public static void UpdateViewMenuCheckMarks(
            AppDataService appDataService,
            ToolStripMenuItem tilesViewMenuItem,
            ToolStripMenuItem compactTilesViewMenuItem,
            ToolStripMenuItem logosViewMenuItem,
            ToolStripMenuItem iconViewMenuItem,
            ToolStripMenuItem detailsMenuItem,
            ToolStripMenuItem tileContextMenuItem,
            ToolStripMenuItem capsuleTilesContextMenuItem,
            ToolStripMenuItem logosContextMenuItem,
            ToolStripMenuItem largeIconsContextMenuItem,
            ToolStripMenuItem detailsContextMenuItem)
        {
            if (appDataService == null)
                return;

            var currentViewMode = appDataService.GetViewMode();

            // Update main menu view items
            if (tilesViewMenuItem != null)
                tilesViewMenuItem.Checked = (currentViewMode == ApplicationConstants.ViewModeTile);
            if (compactTilesViewMenuItem != null)
                compactTilesViewMenuItem.Checked = (currentViewMode == ApplicationConstants.ViewModeCompactTiles);
            if (logosViewMenuItem != null)
                logosViewMenuItem.Checked = (currentViewMode == ApplicationConstants.ViewModeLogos);
            if (iconViewMenuItem != null)
                iconViewMenuItem.Checked = (currentViewMode == ApplicationConstants.ViewModeIcons);
            if (detailsMenuItem != null)
                detailsMenuItem.Checked = (currentViewMode == ApplicationConstants.ViewModeDetails);

            // Update list background context menu view items
            if (tileContextMenuItem != null)
                tileContextMenuItem.Checked = (currentViewMode == ApplicationConstants.ViewModeTile);
            if (capsuleTilesContextMenuItem != null)
                capsuleTilesContextMenuItem.Checked = (currentViewMode == ApplicationConstants.ViewModeCompactTiles);
            if (logosContextMenuItem != null)
                logosContextMenuItem.Checked = (currentViewMode == ApplicationConstants.ViewModeLogos);
            if (largeIconsContextMenuItem != null)
                largeIconsContextMenuItem.Checked = (currentViewMode == ApplicationConstants.ViewModeIcons);
            if (detailsContextMenuItem != null)
                detailsContextMenuItem.Checked = (currentViewMode == ApplicationConstants.ViewModeDetails);
        }

        /// <summary>
        /// Updates the check marks for sort menu items based on the current sort settings.
        /// </summary>
        /// <param name="appDataService">The app data service to get sort settings from.</param>
        /// <param name="ascNameContextMenuItem">Context menu ascending name sort item.</param>
        /// <param name="descNameContextMenuItem">Context menu descending name sort item.</param>
        /// <param name="ascAppIdContextMenuItem">Context menu ascending AppId sort item.</param>
        /// <param name="descAppIdContextMenuItem">Context menu descending AppId sort item.</param>
        /// <param name="noneContextMenuItem">Context menu no sort item.</param>
        /// <param name="ascNameMenuItem">Main menu ascending name sort item.</param>
        /// <param name="descNameMenuItem">Main menu descending name sort item.</param>
        /// <param name="ascAppIdMenuItem">Main menu ascending AppId sort item.</param>
        /// <param name="descAppIdMenuItem">Main menu descending AppId sort item.</param>
        /// <param name="noneMenuItem">Main menu no sort item.</param>
        public static void UpdateSortMenuCheckMarks(
            AppDataService appDataService,
            ToolStripMenuItem ascNameContextMenuItem,
            ToolStripMenuItem descNameContextMenuItem,
            ToolStripMenuItem ascAppIdContextMenuItem,
            ToolStripMenuItem descAppIdContextMenuItem,
            ToolStripMenuItem noneContextMenuItem,
            ToolStripMenuItem ascNameMenuItem,
            ToolStripMenuItem descNameMenuItem,
            ToolStripMenuItem ascAppIdMenuItem,
            ToolStripMenuItem descAppIdMenuItem,
            ToolStripMenuItem noneMenuItem)
        {
            if (appDataService == null)
                return;

            var sortBy = appDataService.GetSortBy();
            var sortDirection = appDataService.GetSortDirection();

            // Clear all check marks first
            if (ascNameContextMenuItem != null)
                ascNameContextMenuItem.Checked = false;
            if (descNameContextMenuItem != null)
                descNameContextMenuItem.Checked = false;
            if (ascAppIdContextMenuItem != null)
                ascAppIdContextMenuItem.Checked = false;
            if (descAppIdContextMenuItem != null)
                descAppIdContextMenuItem.Checked = false;
            if (noneContextMenuItem != null)
                noneContextMenuItem.Checked = false;
            if (ascNameMenuItem != null)
                ascNameMenuItem.Checked = false;
            if (descNameMenuItem != null)
                descNameMenuItem.Checked = false;
            if (ascAppIdMenuItem != null)
                ascAppIdMenuItem.Checked = false;
            if (descAppIdMenuItem != null)
                descAppIdMenuItem.Checked = false;
            if (noneMenuItem != null)
                noneMenuItem.Checked = false;

            // Set check marks based on current sort settings
            if (sortBy == ApplicationConstants.SortByNone)
            {
                if (noneContextMenuItem != null)
                    noneContextMenuItem.Checked = true;
                if (noneMenuItem != null)
                    noneMenuItem.Checked = true;
            }
            else if (sortBy == ApplicationConstants.SortByName)
            {
                if (sortDirection == ApplicationConstants.SortDirectionAsc)
                {
                    if (ascNameContextMenuItem != null)
                        ascNameContextMenuItem.Checked = true;
                    if (ascNameMenuItem != null)
                        ascNameMenuItem.Checked = true;
                }
                else
                {
                    if (descNameContextMenuItem != null)
                        descNameContextMenuItem.Checked = true;
                    if (descNameMenuItem != null)
                        descNameMenuItem.Checked = true;
                }
            }
            else if (sortBy == ApplicationConstants.SortByAppId)
            {
                if (sortDirection == ApplicationConstants.SortDirectionAsc)
                {
                    if (ascAppIdContextMenuItem != null)
                        ascAppIdContextMenuItem.Checked = true;
                    if (ascAppIdMenuItem != null)
                        ascAppIdMenuItem.Checked = true;
                }
                else
                {
                    if (descAppIdContextMenuItem != null)
                        descAppIdContextMenuItem.Checked = true;
                    if (descAppIdMenuItem != null)
                        descAppIdMenuItem.Checked = true;
                }
            }
        }

        /// <summary>
        /// Updates the check marks for theme menu items based on the current theme.
        /// </summary>
        /// <param name="appDataService">The app data service to get theme from.</param>
        /// <param name="lightThemeMenuItem">Light theme menu item.</param>
        /// <param name="darkThemeMenuItem">Dark theme menu item.</param>
        /// <param name="systemThemeMenuItem">System theme menu item.</param>
        public static void UpdateThemeMenuCheckMarks(
            AppDataService appDataService,
            ToolStripMenuItem lightThemeMenuItem,
            ToolStripMenuItem darkThemeMenuItem,
            ToolStripMenuItem systemThemeMenuItem)
        {
            if (appDataService == null)
                return;

            var currentTheme = appDataService.GetThemeMode();

            if (lightThemeMenuItem != null)
                lightThemeMenuItem.Checked = (currentTheme == ThemeMode.Light);
            if (darkThemeMenuItem != null)
                darkThemeMenuItem.Checked = (currentTheme == ThemeMode.Dark);
            if (systemThemeMenuItem != null)
                systemThemeMenuItem.Checked = (currentTheme == ThemeMode.System);
        }
    }
}

