using System.Windows.Forms;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Helpers
{
    public static class MenuCheckMarkHelper
    {
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

