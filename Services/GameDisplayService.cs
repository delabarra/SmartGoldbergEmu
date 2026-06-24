using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    public class GameDisplayService
    {
        private readonly IconService _iconService;

        public GameDisplayService() : this(ServiceLocator.IconService)
        {
        }

        public GameDisplayService(IconService iconService)
        {
            _iconService = iconService ?? ServiceLocator.IconService;
        }

        public void PopulateListView(
            ListView listView,
            List<GameConfig> games,
            string viewMode = null,
            ImageList largeImageList = null,
            ImageList smallImageList = null,
            Func<ulong, bool> isImportPending = null,
            Func<GameConfig, bool> isAddPending = null)
        {
            if (listView == null)
                return;

            var loadIcons = ShouldLoadIcons(viewMode);

            listView.BeginUpdate();
            try
            {
                listView.Items.Clear();

                if (loadIcons)
                {
                    if (largeImageList != null && IsIconView(viewMode))
                        largeImageList.Images.Clear();
                    if (smallImageList != null && IsDetailsView(viewMode))
                        smallImageList.Images.Clear();
                }

                foreach (var game in games)
                {
                    var imageIndex = -1;
                    string iconPath = null;
                    if (loadIcons)
                        GameFolderPathHelper.TryResolveIconSourcePath(game, out iconPath);

                    if (loadIcons && !string.IsNullOrEmpty(iconPath))
                    {
                        if (IsIconView(viewMode))
                            imageIndex = LoadIconIntoImageList(iconPath, largeImageList, true);
                        else if (IsDetailsView(viewMode))
                            imageIndex = LoadIconIntoImageList(iconPath, smallImageList, false);
                    }

                    listView.Items.Add(CreateListViewItem(game, imageIndex, viewMode, isImportPending, isAddPending));
                }
            }
            finally
            {
                listView.EndUpdate();
            }
        }

        public ListViewItem CreateListViewItem(
            GameConfig game,
            int imageIndex = -1,
            string viewMode = null,
            Func<ulong, bool> isImportPending = null,
            Func<GameConfig, bool> isAddPending = null)
        {
            if (game == null)
                return null;

            var mosaic = IsMosaicView(viewMode);
            var itemText = mosaic ? string.Empty : FormatGameListDisplayName(game, isImportPending, isAddPending);
            var item = new ListViewItem(itemText) { Tag = game };

            if (!mosaic)
            {
                item.SubItems.Add(game.AppId > 0 ? game.AppId.ToString() : "—");
                item.SubItems.Add(game.Path ?? string.Empty);
            }

            if (mosaic)
                item.ImageKey = GetMosaicImageKey(game);
            else if (imageIndex >= 0)
                item.ImageIndex = imageIndex;

            item.ToolTipText = BuildGameListItemToolTip(game, isImportPending, isAddPending);
            return item;
        }

        public static string GetMosaicImageKey(GameConfig game)
        {
            if (game == null)
                return string.Empty;
            if (game.AppId > 0)
                return game.AppId.ToString();
            if (game.GameGuid != Guid.Empty)
                return "pending-" + game.GameGuid.ToString("N");
            return "0";
        }

        private static string FormatGameListDisplayName(
            GameConfig game,
            Func<ulong, bool> isImportPending,
            Func<GameConfig, bool> isAddPending)
        {
            if (game == null)
                return string.Empty;

            string name = game.AppName ?? string.Empty;
            if (isImportPending != null && isImportPending(game.AppId))
                return string.IsNullOrWhiteSpace(name)
                    ? "Importing…"
                    : name.Trim() + " (importing…)";

            if (isAddPending != null && isAddPending(game))
                return string.IsNullOrWhiteSpace(name)
                    ? "Adding…"
                    : name.Trim() + " (adding…)";

            return name;
        }

        private static string BuildGameListItemToolTip(
            GameConfig game,
            Func<ulong, bool> isImportPending,
            Func<GameConfig, bool> isAddPending)
        {
            if (game == null)
                return string.Empty;

            string status = string.Empty;
            if (isImportPending != null && isImportPending(game.AppId))
                status = Environment.NewLine + "Status: Setting up emulator files…";
            else if (isAddPending != null && isAddPending(game))
                status = Environment.NewLine + "Status: Finish preview and save to add to the library.";

            string appIdText = game.AppId > 0 ? game.AppId.ToString() : "—";

            return "Name: " + (string.IsNullOrEmpty(game.AppName) ? "—" : game.AppName)
                + Environment.NewLine
                + "App ID: " + appIdText
                + status;
        }

        private static bool ShouldLoadIcons(string viewMode)
        {
            return IsIconView(viewMode) || IsDetailsView(viewMode);
        }

        private static bool IsIconView(string viewMode)
        {
            return ViewEquals(viewMode, ApplicationConstants.ViewModeIcons)
                || ViewEquals(viewMode, "Icon view");
        }

        private static bool IsDetailsView(string viewMode)
        {
            return ViewEquals(viewMode, ApplicationConstants.ViewModeDetails);
        }

        private static bool IsMosaicView(string viewMode)
        {
            return ViewEquals(viewMode, ApplicationConstants.ViewModeTile)
                || ViewEquals(viewMode, ApplicationConstants.ViewModeCompactTiles)
                || ViewEquals(viewMode, ApplicationConstants.ViewModeLogos);
        }

        private static bool ViewEquals(string viewMode, string expected)
        {
            return !string.IsNullOrEmpty(viewMode)
                && viewMode.Equals(expected, StringComparison.OrdinalIgnoreCase);
        }

        private int LoadIconIntoImageList(string filePath, ImageList imageList, bool largeIcon)
        {
            if (imageList == null || string.IsNullOrEmpty(filePath))
                return -1;

            Icon icon = null;
            try
            {
                icon = largeIcon ? _iconService.ExtractLargeIcon(filePath) : _iconService.ExtractSmallIcon(filePath);
                if (icon == null)
                    return -1;

                var size = largeIcon ? new Size(32, 32) : new Size(16, 16);
                var bitmap = _iconService.IconToBitmap(icon, size);
                if (bitmap == null)
                    return -1;

                imageList.Images.Add(bitmap);
                return imageList.Images.Count - 1;
            }
            catch
            {
                return -1;
            }
            finally
            {
                if (icon != null)
                    icon.Dispose();
            }
        }

        public void SetupListViewColumns(ListView listView, string columnOrder = null, string columnWidthsCsv = null)
        {
            if (listView == null)
                return;

            listView.Columns.Clear();

            var widthCsv = ApplicationConstants.NormalizeDetailsColumnWidths(columnWidthsCsv);
            var parts = widthCsv.Split(new[] { ',' }, StringSplitOptions.None);
            int wName = int.Parse(parts[0], CultureInfo.InvariantCulture);
            int wAppId = int.Parse(parts[1], CultureInfo.InvariantCulture);
            int wPath = int.Parse(parts[2], CultureInfo.InvariantCulture);

            listView.Columns.Add(ApplicationConstants.ColumnName, wName);
            listView.Columns.Add(ApplicationConstants.ColumnAppId, wAppId);
            listView.Columns.Add(ApplicationConstants.ColumnPath, wPath);

            if (!string.IsNullOrEmpty(columnOrder))
                RestoreColumnOrder(listView, columnOrder);
        }

        private void RestoreColumnOrder(ListView listView, string columnOrder)
        {
            if (listView == null || string.IsNullOrEmpty(columnOrder))
                return;

            var names = columnOrder.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (names.Length == 0)
                return;

            var columnMap = new Dictionary<string, ColumnHeader>();
            foreach (ColumnHeader column in listView.Columns)
            {
                if (!string.IsNullOrEmpty(column.Text))
                    columnMap[column.Text] = column;
            }

            var displayIndex = 0;
            foreach (var raw in names)
            {
                var trimmed = raw.Trim();
                if (columnMap.TryGetValue(trimmed, out var col))
                    col.DisplayIndex = displayIndex++;
            }

        }

        public void SetViewMode(
            ListView listView,
            string viewMode,
            ImageList largeImageList = null,
            ImageList smallImageList = null,
            string detailsColumnOrder = null,
            string detailsColumnWidths = null)
        {
            if (listView == null || string.IsNullOrEmpty(viewMode))
                return;

            if (IsDetailsView(viewMode))
            {
                if (listView.Columns.Count == 0 ||
                    listView.Columns.Cast<ColumnHeader>().Any(c => string.IsNullOrEmpty(c.Text)))
                {
                    listView.Columns.Clear();
                    SetupListViewColumns(listView, detailsColumnOrder, detailsColumnWidths);
                }
                listView.View = View.Details;
                listView.AllowColumnReorder = true;
                if (smallImageList != null)
                    listView.SmallImageList = smallImageList;
                listView.LargeImageList = null;
            }
            else if (IsIconView(viewMode))
            {
                listView.View = View.LargeIcon;
                if (largeImageList != null)
                    listView.LargeImageList = largeImageList;
                listView.SmallImageList = null;
            }
            else if (ViewEquals(viewMode, ApplicationConstants.ViewModeTile))
            {
                ApplyNativeTileView(listView, new Size(MosaicViewHelper.TileViewWidth, MosaicViewHelper.TileViewHeight), largeImageList);
            }
            else if (ViewEquals(viewMode, ApplicationConstants.ViewModeCompactTiles))
            {
                ApplyNativeTileView(listView, new Size(MosaicViewHelper.CompactTilesViewWidth, MosaicViewHelper.CompactTilesViewHeight), largeImageList);
            }
            else if (ViewEquals(viewMode, ApplicationConstants.ViewModeLogos))
            {
                ApplyNativeTileView(listView, new Size(MosaicViewHelper.LogoViewWidth, MosaicViewHelper.LogoViewHeight), largeImageList);
            }
            else
            {
                listView.View = View.Tile;
                listView.LargeImageList = null;
                listView.SmallImageList = null;
            }
        }

        private static void ApplyNativeTileView(ListView listView, Size tileSize, ImageList largeImageList)
        {
            listView.View = View.Tile;
            listView.TileSize = tileSize;
            if (largeImageList != null)
            {
                listView.LargeImageList = largeImageList;
                listView.SmallImageList = largeImageList;
            }
            listView.OwnerDraw = false;
        }

        public void ApplySort(ListView listView, string sortBy, string sortDirection)
        {
            if (listView == null || string.IsNullOrEmpty(sortBy))
                return;

            if (sortBy.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                listView.Sorting = SortOrder.None;
                return;
            }

            listView.BeginUpdate();
            try
            {
                var items = listView.Items.Cast<ListViewItem>().ToList();
                var ascending = sortDirection.Equals("Asc", StringComparison.OrdinalIgnoreCase);

                if (sortBy.Equals("Name", StringComparison.OrdinalIgnoreCase))
                    items = (ascending ? items.OrderBy(GetSortKeyName) : items.OrderByDescending(GetSortKeyName)).ToList();
                else if (sortBy.Equals("AppId", StringComparison.OrdinalIgnoreCase))
                    items = (ascending ? items.OrderBy(GetSortKeyAppId) : items.OrderByDescending(GetSortKeyAppId)).ToList();

                listView.Items.Clear();
                foreach (var item in items)
                    listView.Items.Add(item);
            }
            finally
            {
                listView.EndUpdate();
            }
        }

        private static string GetSortKeyName(ListViewItem item)
        {
            if (item?.Tag is GameConfig game)
                return game.AppName ?? string.Empty;
            return item?.Text ?? string.Empty;
        }

        private static ulong GetSortKeyAppId(ListViewItem item)
        {
            if (item?.Tag is GameConfig game)
                return game.AppId;
            if (item?.SubItems.Count > 1 && ulong.TryParse(item.SubItems[1].Text, out ulong appId))
                return appId;
            return 0UL;
        }

        public void RefreshListView(
            ListView listView,
            GameDataService gameDataService,
            AppDataService appDataService,
            ImageList largeImageList = null,
            ImageList smallImageList = null,
            Func<ulong, bool> isImportPending = null,
            Func<GameConfig, bool> isAddPending = null,
            IReadOnlyList<GameConfig> gamesOverride = null,
            bool applySort = true)
        {
            if (listView == null || gameDataService == null || appDataService == null)
                return;

            var viewMode = appDataService.GetViewMode();
            var games = gamesOverride != null
                ? new List<GameConfig>(gamesOverride)
                : gameDataService.GetAllGames();

            PopulateListView(listView, games, viewMode, largeImageList, smallImageList, isImportPending, isAddPending);
            SetViewMode(listView, viewMode, largeImageList, smallImageList, appDataService.GetDetailsColumnOrder(), appDataService.GetDetailsColumnWidths());
            if (applySort)
                ApplySort(listView, appDataService.GetSortBy(), appDataService.GetSortDirection());
        }

        public static ListViewItem FindListItemByGameGuid(ListView listView, Guid gameGuid)
        {
            if (listView == null || gameGuid == Guid.Empty)
                return null;

            foreach (ListViewItem item in listView.Items)
            {
                if (item?.Tag is GameConfig game && game.GameGuid == gameGuid)
                    return item;
            }

            return null;
        }

        public void UpdateListViewItem(
            ListViewItem item,
            GameConfig game,
            string viewMode,
            ImageList largeImageList,
            ImageList smallImageList,
            Func<ulong, bool> isImportPending,
            Func<GameConfig, bool> isAddPending)
        {
            if (item == null || game == null)
                return;

            var mosaic = IsMosaicView(viewMode);
            item.Tag = game;
            if (!mosaic)
                item.Text = FormatGameListDisplayName(game, isImportPending, isAddPending);

            if (!mosaic && item.SubItems.Count >= 3)
            {
                item.SubItems[1].Text = game.AppId > 0 ? game.AppId.ToString() : "—";
                item.SubItems[2].Text = game.Path ?? string.Empty;
            }

            if (mosaic)
                item.ImageKey = GetMosaicImageKey(game);
            else
            {
                int imageIndex = -1;
                if (ShouldLoadIcons(viewMode) && GameFolderPathHelper.TryResolveIconSourcePath(game, out string iconPath)
                    && !string.IsNullOrEmpty(iconPath))
                {
                    if (IsIconView(viewMode))
                        imageIndex = LoadIconIntoImageList(iconPath, largeImageList, true);
                    else if (IsDetailsView(viewMode))
                        imageIndex = LoadIconIntoImageList(iconPath, smallImageList, false);
                }

                if (imageIndex >= 0)
                    item.ImageIndex = imageIndex;
            }

            item.ToolTipText = BuildGameListItemToolTip(game, isImportPending, isAddPending);
        }

        public PendingListSyncResult SyncPendingAddListItem(
            ListView listView,
            GameConfig draft,
            string viewMode,
            ImageList largeImageList,
            ImageList smallImageList,
            Func<ulong, bool> isImportPending,
            Func<GameConfig, bool> isAddPending)
        {
            if (listView == null || draft == null || draft.GameGuid == Guid.Empty)
                return PendingListSyncResult.NoOp;

            var existing = FindListItemByGameGuid(listView, draft.GameGuid);
            listView.BeginUpdate();
            try
            {
                if (existing != null)
                {
                    UpdateListViewItem(existing, draft, viewMode, largeImageList, smallImageList, isImportPending, isAddPending);
                    return PendingListSyncResult.Updated;
                }

                int imageIndex = -1;
                if (ShouldLoadIcons(viewMode) && GameFolderPathHelper.TryResolveIconSourcePath(draft, out string iconPath)
                    && !string.IsNullOrEmpty(iconPath))
                {
                    if (IsIconView(viewMode))
                        imageIndex = LoadIconIntoImageList(iconPath, largeImageList, true);
                    else if (IsDetailsView(viewMode))
                        imageIndex = LoadIconIntoImageList(iconPath, smallImageList, false);
                }

                listView.Items.Add(CreateListViewItem(draft, imageIndex, viewMode, isImportPending, isAddPending));
                return PendingListSyncResult.Added;
            }
            finally
            {
                listView.EndUpdate();
            }
        }

        public PendingListSyncResult RemoveListItemByGameGuid(ListView listView, Guid gameGuid)
        {
            var item = FindListItemByGameGuid(listView, gameGuid);
            if (item == null)
                return PendingListSyncResult.NoOp;

            listView.BeginUpdate();
            try
            {
                listView.Items.Remove(item);
            }
            finally
            {
                listView.EndUpdate();
            }

            return PendingListSyncResult.Removed;
        }
    }
}
