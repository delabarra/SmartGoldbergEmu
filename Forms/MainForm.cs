using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Extensions;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Properties;
using SmartGoldbergEmu.Services;
using SmartGoldbergEmu.Validation;
using SteamKit;
using System.Threading.Tasks;
using System.Diagnostics;

namespace SmartGoldbergEmu.Forms
{
    public partial class MainForm : Form
    {
        private readonly GameDataService _gameDataService;
        private readonly AppDataService _appDataService;
        private readonly GameDisplayService _gameDisplayService;
        private readonly ThemeService _themeService;
        private readonly GameLaunchService _gameLaunchService;
        private readonly GameSetupService _gameSetupService;
        private readonly TaskReportService _taskReportService;
        private readonly LaunchOptionService _launchOptionService;
        private readonly SteamApiKeyService _apiKeyService;
        private readonly PendingAddGameListService _pendingAddGameListService;
        private LegacyImportService _legacyImportService;
        private ImageList _largeImageList;
        private ImageList _smallImageList;
        private ImageList _tileImageList;
        private ImageList _compactTileImageList;
        private ImageList _logoImageList;
        private ApiKeyStatusIndicatorHelper _apiKeyStatusIndicatorHelper;
        private UriFileWatcherHelper _uriFileWatcherHelper;
        private string _persistedDetailsColumnWidths;
        private Timer _detailsColumnWidthsSaveTimer;
        private const int DetailsColumnWidthsSaveDebounceMs = 250;
        private Timer _gameListRefreshTimer;
        private const int GameListRefreshDebounceMs = 80;
        private bool _gameListRefreshFullTiles;
        private int _tileImageLoadGeneration;
        private string _pendingAddMosaicImageKey;

        public ulong? PendingAppIdLaunch { get; set; }

        public TaskReportService TaskReportService => _taskReportService;

        public MainForm() : this(
            ServiceLocator.GameDataService,
            ServiceLocator.AppDataService,
            ServiceLocator.GameDisplayService,
            ServiceLocator.ThemeService,
            ServiceLocator.GameLaunchService,
            ServiceLocator.GameSetupService,
            ServiceLocator.LaunchOptionService)
        {
        }

        public MainForm(
            GameDataService gameDataService,
            AppDataService appDataService,
            GameDisplayService gameDisplayService,
            ThemeService themeService,
            GameLaunchService gameLaunchService,
            GameSetupService gameSetupService) : this(gameDataService, appDataService, gameDisplayService, themeService, gameLaunchService, gameSetupService, ServiceLocator.LaunchOptionService)
        {
        }

        public MainForm(
            GameDataService gameDataService,
            AppDataService appDataService,
            GameDisplayService gameDisplayService,
            ThemeService themeService,
            GameLaunchService gameLaunchService,
            GameSetupService gameSetupService,
            LaunchOptionService launchOptionService)
        {
            InitializeComponent();

            _gameDataService = gameDataService ?? throw new ArgumentNullException(nameof(gameDataService));
            _appDataService = appDataService ?? throw new ArgumentNullException(nameof(appDataService));
            _gameDisplayService = gameDisplayService ?? throw new ArgumentNullException(nameof(gameDisplayService));
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            _gameLaunchService = gameLaunchService ?? throw new ArgumentNullException(nameof(gameLaunchService));
            _gameSetupService = gameSetupService ?? throw new ArgumentNullException(nameof(gameSetupService));
            _launchOptionService = launchOptionService ?? ServiceLocator.LaunchOptionService;
            _apiKeyService = ServiceLocator.SteamApiKeyService;
            _pendingAddGameListService = ServiceLocator.PendingAddGameListService;
            _taskReportService = new TaskReportService(
                prgFeedback,
                lblFeedback,
                this);

            if (DesignTimeHelper.IsDesignTime)
                return;

            this.Icon = Resources.steam_gold_x128;
            Text = ApplicationVersionHelper.GetWindowTitle();
            ApplyViewModeMenuTexts();

            ServiceLocator.SetTaskReportService(_taskReportService);

            InitializeImageLists();
            InitializeApiKeyStatusIndicator();
            InitializeGameDisplay();
            SetupContextMenus();
            InitializeTheme();
            SetupListViewOwnerDraw();

            _themeService.ThemeChanged += ThemeService_ThemeChanged;

            _uriFileWatcherHelper = new UriFileWatcherHelper(this, LaunchGameByAppId);
            _uriFileWatcherHelper.Setup();
        }

        private void InitializeApiKeyStatusIndicator()
        {
            _apiKeyStatusIndicatorHelper?.Dispose();

            _apiKeyStatusIndicatorHelper = new ApiKeyStatusIndicatorHelper(lblApiKeyStatus, _apiKeyService);
            _apiKeyStatusIndicatorHelper.IndicatorClicked += ApiKeyStatusIndicatorHelper_IndicatorClicked;
            _apiKeyStatusIndicatorHelper.Initialize();
        }

        private void UpdateApiKeyStatusIndicator()
        {
            _apiKeyStatusIndicatorHelper?.Update();
        }

        private void OpenSettingsDialog(int? userAccountTabIndex = null)
        {
            using (var settingsForm = new SettingsForm())
            {
                settingsForm.ApiKeyValidationStatusChanged += (s, args) => UpdateApiKeyStatusIndicator();
                if (userAccountTabIndex.HasValue)
                    settingsForm.SetSelectedTab(userAccountTabIndex.Value);
                settingsForm.ShowDialog(this);
                InitializeApiKeyStatusIndicator();
            }
        }

        private static TaskReportService GetLocatorTaskReportOrNull()
        {
            var feedback = ServiceLocator.TaskReportService;
            if (feedback == null)
                Program.LogService?.LogWarning("TaskReportService is null; progress will not be shown.");
            return feedback;
        }

        private void ApiKeyStatusIndicatorHelper_IndicatorClicked(object sender, EventArgs e)
        {
            OpenSettingsDialog(0);
        }

        private void OnThemeLight_Click(object sender, EventArgs e)
        {
            SetThemeAndUpdate(ThemeMode.Light);
        }

        private void OnThemeDark_Click(object sender, EventArgs e)
        {
            SetThemeAndUpdate(ThemeMode.Dark);
        }

        private void OnThemeSystem_Click(object sender, EventArgs e)
        {
            SetThemeAndUpdate(ThemeMode.System);
        }

        private void SetThemeAndUpdate(ThemeMode mode)
        {
            _themeService.SetTheme(mode, this);
            _appDataService.SetThemeMode(mode);
            UpdateThemeIcon();
            UpdateThemeMenuCheckMarks();
        }

        private void InitializeTheme()
        {
            try
            {
                var themeMode = _appDataService.GetThemeMode();
                _themeService.SetTheme(themeMode, this);
                UpdateThemeIcon();
                UpdateThemeMenuCheckMarks();
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Failed to initialize theme: {ex.Message}");
            }
        }

        private void ThemeService_ThemeChanged(object sender, ThemeChangedEventArgs e)
        {
            if (IsDisposed || Disposing)
                return;
            if (InvokeRequired)
            {
                Invoke(new Action(ApplyThemeFromService));
                return;
            }
            ApplyThemeFromService();
        }

        private void ApplyThemeFromService()
        {
            _themeService.ApplyTheme(this);
            UpdateThemeIcon();
            UpdateThemeMenuCheckMarks();
            ReloadMosaicTileImagesIfNeeded();
        }

        private void ReloadMosaicTileImagesIfNeeded()
        {
            var viewMode = _appDataService.GetViewMode();
            if (!IsMosaicViewMode(viewMode))
                return;
            StartLoadTileImages(viewMode);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _taskReportService.Clear();
            _themeService.ThemeChanged -= ThemeService_ThemeChanged;

            if (_detailsColumnWidthsSaveTimer != null)
            {
                _detailsColumnWidthsSaveTimer.Stop();
                _detailsColumnWidthsSaveTimer.Tick -= DetailsColumnWidthsSaveTimer_Tick;
                _detailsColumnWidthsSaveTimer.Dispose();
                _detailsColumnWidthsSaveTimer = null;
            }

            if (_gameListRefreshTimer != null)
            {
                _gameListRefreshTimer.Stop();
                _gameListRefreshTimer.Tick -= GameListRefreshTimer_Tick;
                _gameListRefreshTimer.Dispose();
                _gameListRefreshTimer = null;
            }

            _largeImageList?.Dispose();
            _largeImageList = null;
            _smallImageList?.Dispose();
            _smallImageList = null;
            _tileImageList?.Dispose();
            _tileImageList = null;
            _compactTileImageList?.Dispose();
            _compactTileImageList = null;
            _logoImageList?.Dispose();
            _logoImageList = null;

            _apiKeyStatusIndicatorHelper?.Dispose();
            _uriFileWatcherHelper?.Dispose();

            base.OnFormClosed(e);
        }

        private static ImageList CreateImageList(Size imageSize)
        {
            return new ImageList
            {
                ImageSize = imageSize,
                ColorDepth = ColorDepth.Depth32Bit
            };
        }

        private void InitializeImageLists()
        {
            _largeImageList = CreateImageList(new Size(32, 32));
            _smallImageList = CreateImageList(new Size(16, 16));
            _tileImageList = CreateImageList(new Size(MosaicViewHelper.TileViewImageWidth, MosaicViewHelper.TileViewImageHeight));
            _compactTileImageList = CreateImageList(new Size(MosaicViewHelper.CompactTilesViewImageWidth, MosaicViewHelper.CompactTilesViewImageHeight));
            _logoImageList = CreateImageList(MosaicViewHelper.LogoViewImageSize);
        }

        private void UpdateThemeIcon()
        {
            var currentTheme = _themeService.CurrentTheme;
            switch (currentTheme)
            {
                case ThemeMode.Light:
                    btnTheme.Text = "☀️";
                    btnTheme.ToolTipText = "Light";
                    break;
                case ThemeMode.Dark:
                    btnTheme.Text = "🌙";
                    btnTheme.ToolTipText = "Dark";
                    break;
                case ThemeMode.System:
                    btnTheme.Text = "🖥️";
                    btnTheme.ToolTipText = "System";
                    break;
            }
        }

        private void OnSettings_Click(object sender, EventArgs e)
        {
            OpenSettingsDialog();
        }

        private void InitializeGameDisplay()
        {
            try
            {
                var viewMode = _appDataService.GetViewMode();
                var detailsColumnOrder = _appDataService.GetDetailsColumnOrder();
                var detailsColumnWidths = _appDataService.GetDetailsColumnWidths();
                _persistedDetailsColumnWidths = detailsColumnWidths;

                LoadGames(viewMode);

                var tileImageList = GetTileImageListForViewMode(viewMode);
                _gameDisplayService.SetViewMode(lstGames, viewMode,
                    tileImageList ?? _largeImageList,
                    tileImageList ?? _smallImageList,
                    detailsColumnOrder,
                    detailsColumnWidths);

                lstGames.OwnerDraw = (viewMode == ApplicationConstants.ViewModeDetails);

                if (IsMosaicViewMode(viewMode))
                    StartLoadTileImages(viewMode);

                if (viewMode == ApplicationConstants.ViewModeDetails)
                {
                    UpdateDetailsGameListColumns();
                }
                UpdateViewMenuCheckMarks();

                _gameDisplayService.ApplySort(lstGames, _appDataService.GetSortBy(), _appDataService.GetSortDirection());
                UpdateSortMenuCheckMarks();
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Failed to initialize game display: {ex.Message}");
            }
        }

        private ImageList GetTileImageListForViewMode(string viewMode)
        {
            if (viewMode == ApplicationConstants.ViewModeTile)
                return _tileImageList;
            if (viewMode == ApplicationConstants.ViewModeCompactTiles)
                return _compactTileImageList;
            if (viewMode == ApplicationConstants.ViewModeLogos)
                return _logoImageList;
            return null;
        }

        private void LoadGames(string viewMode = null)
        {
            try
            {
                var games = GetGamesForListDisplay();
                var tileImageList = GetTileImageListForViewMode(viewMode);
                _gameDisplayService.PopulateListView(lstGames, games, viewMode,
                    tileImageList ?? _largeImageList,
                    tileImageList ?? _smallImageList,
                    GetImportPendingPredicate(),
                    GetAddPendingPredicate());
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Failed to load games: {ex.Message}");
            }
        }

        private void StartLoadTileImages(string viewMode)
        {
            int generation = ++_tileImageLoadGeneration;
            _ = LoadTileImagesAsync(viewMode, generation).ForgetFaults(Program.LogService, nameof(LoadTileImagesAsync));
        }

        private async Task LoadTileImagesAsync(string viewMode, int generation)
        {
            try
            {
                if (IsDisposed || Disposing)
                    return;

                var gameImageService = ServiceLocator.GameImageService;
                var imageNormalizationService = ServiceLocator.ImageNormalizationService;
                var targetImageList = GetTileImageListForViewMode(viewMode);
                if (targetImageList == null)
                    return;

                targetImageList.Images.Clear();

                var effectiveTheme = _themeService.EffectiveTheme;
                _themeService.GetFallbackMosaicArtColors(effectiveTheme, out var mosaicBackground, out var mosaicForeground);
                await gameImageService.EnsureMosaicFallbackForViewAsync(viewMode, effectiveTheme, mosaicBackground, mosaicForeground).ConfigureAwait(true);

                if (IsDisposed || Disposing || generation != _tileImageLoadGeneration)
                    return;

                var addedAppIds = new HashSet<string>();
                bool logosDropShadow = viewMode == ApplicationConstants.ViewModeLogos
                    && _appDataService.GetLogosViewDropShadow();

                foreach (ListViewItem item in lstGames.Items)
                {
                    if (IsDisposed || Disposing || generation != _tileImageLoadGeneration)
                        return;

                    var game = item.Tag as GameConfig;
                    if (game == null)
                        continue;

                    string imageKey = GameDisplayService.GetMosaicImageKey(game);

                    if (addedAppIds.Contains(imageKey))
                        continue;

                    Bitmap imageCopy = await LoadMosaicDisplayBitmapForGameAsync(
                        game,
                        viewMode,
                        gameImageService,
                        imageNormalizationService,
                        logosDropShadow).ConfigureAwait(true);

                    if (imageCopy == null)
                        continue;

                    if (generation != _tileImageLoadGeneration)
                    {
                        imageCopy.Dispose();
                        continue;
                    }

                    targetImageList.Images.Add(imageKey, imageCopy);
                    addedAppIds.Add(imageKey);
                }

                if (IsDisposed || Disposing || generation != _tileImageLoadGeneration)
                    return;

                lstGames.Invalidate();
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Failed to load tile images: {ex.Message}");
            }
        }

        private Task UpsertMosaicTileForGameAsync(GameConfig game, string viewMode)
        {
            if (game == null || !IsMosaicViewMode(viewMode))
                return Task.CompletedTask;

            return UpsertMosaicTileForGameCoreAsync(game, viewMode);
        }

        private async Task UpsertMosaicTileForGameCoreAsync(GameConfig game, string viewMode)
        {
            try
            {
                if (IsDisposed || Disposing)
                    return;

                var gameImageService = ServiceLocator.GameImageService;
                var imageNormalizationService = ServiceLocator.ImageNormalizationService;
                var targetImageList = GetTileImageListForViewMode(viewMode);
                if (targetImageList == null)
                    return;

                string imageKey = GameDisplayService.GetMosaicImageKey(game);
                if (string.IsNullOrEmpty(imageKey))
                    return;

                var effectiveTheme = _themeService.EffectiveTheme;
                _themeService.GetFallbackMosaicArtColors(effectiveTheme, out var mosaicBackground, out var mosaicForeground);
                await gameImageService.EnsureMosaicFallbackForViewAsync(viewMode, effectiveTheme, mosaicBackground, mosaicForeground).ConfigureAwait(true);

                if (IsDisposed || Disposing)
                    return;

                bool logosDropShadow = viewMode == ApplicationConstants.ViewModeLogos
                    && _appDataService.GetLogosViewDropShadow();

                Bitmap imageCopy = await LoadMosaicDisplayBitmapForGameAsync(
                    game,
                    viewMode,
                    gameImageService,
                    imageNormalizationService,
                    logosDropShadow).ConfigureAwait(true);

                if (imageCopy == null)
                    return;

                if (IsDisposed || Disposing)
                {
                    imageCopy.Dispose();
                    return;
                }

                if (targetImageList.Images.ContainsKey(imageKey))
                    targetImageList.Images.RemoveByKey(imageKey);
                targetImageList.Images.Add(imageKey, imageCopy);

                var item = GameDisplayService.FindListItemByGameGuid(lstGames, game.GameGuid);
                if (item != null)
                    item.ImageKey = imageKey;

                lstGames.Invalidate();
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Failed to update tile for {game?.AppName}: {ex.Message}");
            }
        }

        private async Task<Bitmap> LoadMosaicDisplayBitmapForGameAsync(
            GameConfig game,
            string viewMode,
            GameImageService gameImageService,
            ImageNormalizationService imageNormalizationService,
            bool logosDropShadow)
        {
            string imagePath;
            if (viewMode == ApplicationConstants.ViewModeTile)
            {
                imagePath = gameImageService.GetImagePath(game.AppId, PathConstants.SteamGameResourcesHeaderImageFileName);
            }
            else if (viewMode == ApplicationConstants.ViewModeLogos)
            {
                imagePath = gameImageService.GetLogoImagePathOrFallback(game.AppId);
                if (!string.IsNullOrEmpty(imagePath))
                    gameImageService.NormalizeResolvedLogoForLogosListIfNeeded(imagePath);
            }
            else
            {
                imagePath = gameImageService.GetCapsuleImagePathOrFallback(game.AppId);
                if (!string.IsNullOrEmpty(imagePath))
                {
                    var imageFileName = Path.GetFileName(imagePath);
                    if (string.Equals(imageFileName, PathConstants.SteamGameResourcesCapsuleCoverImageFileName, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(imageFileName, PathConstants.SteamGameResourcesCapsuleImageFileName, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(imageFileName, PathConstants.SteamGameResourcesLegacyLibraryCapsuleImageFileName, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(imageFileName, PathConstants.SteamGameResourcesSmallCapsuleImageFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        imageNormalizationService.EnsureCompactTileFileNormalizedForCompactTilesView(imagePath);
                    }
                }
            }

            if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
            {
                try
                {
                    using (var image = Image.FromFile(imagePath))
                    {
                        if (viewMode == ApplicationConstants.ViewModeTile)
                            return MosaicViewHelper.CreateTileViewDisplayBitmap(image);
                        if (viewMode == ApplicationConstants.ViewModeLogos)
                            return MosaicViewHelper.CreateLogoViewDisplayBitmap(image, logosDropShadow);
                        return new Bitmap(image);
                    }
                }
                catch
                {
                }
            }

            using (var rawFallback = gameImageService.TryCloneMosaicFallbackBitmap())
            {
                if (rawFallback == null)
                    return null;

                if (viewMode == ApplicationConstants.ViewModeTile)
                    return MosaicViewHelper.CreateTileViewDisplayBitmap(rawFallback);
                if (viewMode == ApplicationConstants.ViewModeLogos)
                {
                    using (var normalized = imageNormalizationService.CreateFallbackLogoDisplayBitmapForLogosView(rawFallback))
                    {
                        return MosaicViewHelper.CreateLogoViewDisplayBitmap(normalized, logosDropShadow);
                    }
                }

                return imageNormalizationService.CreateCompactTileDisplayBitmapFromImage(rawFallback);
            }
        }

        private void RemoveMosaicImageKey(string imageKey)
        {
            if (string.IsNullOrEmpty(imageKey))
                return;

            RemoveMosaicImageKeyFromList(_tileImageList, imageKey);
            RemoveMosaicImageKeyFromList(_compactTileImageList, imageKey);
            RemoveMosaicImageKeyFromList(_logoImageList, imageKey);
        }

        private static void RemoveMosaicImageKeyFromList(ImageList imageList, string imageKey)
        {
            if (imageList?.Images == null || string.IsNullOrEmpty(imageKey))
                return;

            if (imageList.Images.ContainsKey(imageKey))
                imageList.Images.RemoveByKey(imageKey);
        }

        private void SetupContextMenus()
        {
            lstGames.ContextMenuStrip = ctxGamesView;

            lstGames.MouseDown += lstGames_MouseDown;

            ctxGamesItem.Opening += ctxGamesItem_Opening;
            ctxGamesView.Opening += ctxGamesView_Opening;

            miCtxRowRun.Click += OnRunGame_Click;
            miCtxRowRunWithoutEmu.Click += OnRunWithoutEmu_Click;
            miCtxRowRemove.Click += OnRemoveGame_Click;
            miCtxRowProperties.Click += OnGameProperties_Click;
            miCtxRowGenAchievements.Click += OnGenerateAchievements_Click;
            miCtxRowGenItems.Click += OnGenerateItems_Click;
            miCtxRowOpenValveDataFile.Click += OnOpenValveDataFile_Click;

            miCtxRowSteamStore.Click += OnOpenSteamStore_Click;
            miCtxRowSteamCommunity.Click += OnOpenSteamCommunity_Click;
            miCtxRowSteamWorkshop.Click += OnOpenSteamWorkshop_Click;
            miCtxRowSteamDb.Click += OnOpenSteamDb_Click;

            miCtxRowGameDependencies.Click += OnOpenGameDependencies_Click;
            miCtxRowLauncherOptions.Click += OnOpenLauncherOptions_Click;

            miCtxRowOpenExecutableFolder.Click += OnOpenExecutableFolder_Click;
            miCtxRowOpenSettingsFolder.Click += OnOpenSettingsFolder_Click;
            miCtxRowOpenInventoryFile.Click += OnOpenInventoryFile_Click;

            miCtxRowCopyGuid.Click += OnCopyGuid_Click;
            miCtxRowCreateShortcut.Click += OnCreateShortcut_Click;
            miCtxRowCreateSteamAppIdFile.Click += OnCreateSteamAppIdFile_Click;
            miCtxRowApplySteamless.Click += OnApplySteamless_Click;

            lstGames.ItemActivate += lstGames_ItemActivate;

            lstGames.KeyDown += lstGames_KeyDown;

            lstGames.AllowDrop = true;
            lstGames.DragEnter += lstGames_DragEnter;
            lstGames.DragDrop += lstGames_DragDrop;
        }

        private void SetupListViewOwnerDraw()
        {
            lstGames.DrawColumnHeader += lstGames_DrawColumnHeader;
            lstGames.DrawItem += lstGames_DrawItem;
            lstGames.DrawSubItem += lstGames_DrawSubItem;
            lstGames.Resize += lstGames_Resize;
            lstGames.ColumnClick += lstGames_ColumnClick;
            lstGames.ColumnWidthChanged += lstGames_ColumnWidthChanged;
            lstGames.ColumnWidthChanging += lstGames_ColumnWidthChanging;
            lstGames.ColumnReordered += lstGames_ColumnReordered;

            ListViewColumnHelper.ReducePaintFlicker(lstGames);
        }

        private void lstGames_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            ListViewColumnHelper.DrawThemedColumnHeader(e, _themeService, _appDataService);
        }

        private void lstGames_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            e.DrawDefault = true;
        }

        private void lstGames_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            e.DrawDefault = true;
        }

        private void lstGames_Resize(object sender, EventArgs e)
        {
            var viewMode = _appDataService.GetViewMode();
            if (viewMode == ApplicationConstants.ViewModeDetails && lstGames.Columns.Count >= 3)
                UpdateDetailsGameListColumns();
            else if (IsMosaicViewMode(viewMode))
                lstGames.Invalidate();
        }

        private static string ToggleSortDirection(string currentDirection)
        {
            return currentDirection == ApplicationConstants.SortDirectionAsc
                ? ApplicationConstants.SortDirectionDesc
                : ApplicationConstants.SortDirectionAsc;
        }

        private void lstGames_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (_appDataService.GetViewMode() != ApplicationConstants.ViewModeDetails)
                return;

            var clickedColumn = lstGames.Columns[e.Column];
            if (clickedColumn == null)
                return;

            var currentSortBy = _appDataService.GetSortBy();
            var currentSortDirection = _appDataService.GetSortDirection();

            if (clickedColumn.Text == ApplicationConstants.ColumnName)
            {
                var sortDirection = currentSortBy == ApplicationConstants.SortByName
                    ? ToggleSortDirection(currentSortDirection)
                    : ApplicationConstants.SortDirectionAsc;
                ApplySort(ApplicationConstants.SortByName, sortDirection);
                lstGames.Invalidate();
                return;
            }

            if (clickedColumn.Text == ApplicationConstants.ColumnAppId)
            {
                var sortDirection = currentSortBy == ApplicationConstants.SortByAppId
                    ? ToggleSortDirection(currentSortDirection)
                    : ApplicationConstants.SortDirectionAsc;
                ApplySort(ApplicationConstants.SortByAppId, sortDirection);
                lstGames.Invalidate();
            }
        }

        private void lstGames_ColumnWidthChanged(object sender, ColumnWidthChangedEventArgs e)
        {
            var viewMode = _appDataService.GetViewMode();
            if (viewMode != ApplicationConstants.ViewModeDetails || lstGames.Columns.Count < 3)
                return;

            var changedColumn = lstGames.Columns[e.ColumnIndex];
            if (changedColumn != null && changedColumn.Text != ApplicationConstants.ColumnPath)
                UpdateDetailsGameListColumns();

            SchedulePersistDetailsColumnWidths();
        }

        private void lstGames_ColumnWidthChanging(object sender, ColumnWidthChangingEventArgs e)
        {
            var viewMode = _appDataService.GetViewMode();
            if (viewMode != ApplicationConstants.ViewModeDetails || lstGames.Columns.Count < 3)
                return;

            ListViewColumnHelper.ClampDetailsDataColumnWidthChanging(e);
        }

        private void lstGames_ColumnReordered(object sender, ColumnReorderedEventArgs e)
        {
            var viewMode = _appDataService.GetViewMode();
            ListViewColumnHelper.HandleColumnReordered(lstGames, _appDataService, viewMode);
            if (viewMode == ApplicationConstants.ViewModeDetails && lstGames.Columns.Count >= 3)
                UpdateDetailsGameListColumns();
        }

        private void UpdateDetailsGameListColumns()
        {
            ListViewColumnHelper.UpdateDetailsGameListColumnLayout(lstGames);
        }

        private void SetListViewContextMenu(ContextMenuStrip menu)
        {
            if (lstGames.ContextMenuStrip != menu)
                lstGames.ContextMenuStrip = menu;
        }

        private void SyncListViewContextMenuFromSelection()
        {
            SetListViewContextMenu(lstGames.SelectedItems.Count > 0
                ? ctxGamesItem
                : ctxGamesView);
        }

        private void UpdateListViewContextMenuForPoint(Point location)
        {
            var hitTest = lstGames.HitTest(location);
            SetListViewContextMenu(hitTest.Item != null ? ctxGamesItem : ctxGamesView);
        }

        private void lstGames_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            UpdateListViewContextMenuForPoint(e.Location);
        }

        private void OnViewModeTile_Click(object sender, EventArgs e) => SwitchToTilesView();

        private void OnViewModeCompactTiles_Click(object sender, EventArgs e) => SwitchToCompactTilesView();

        private void OnViewModeLogos_Click(object sender, EventArgs e) => SwitchToLogosView();

        private void OnViewModeIcons_Click(object sender, EventArgs e) => SwitchToIconView();

        private void OnViewModeDetails_Click(object sender, EventArgs e) => SwitchToDetailsView();

        private void SwitchToTilesView()
        {
            SwitchViewMode(ApplicationConstants.ViewModeTile, _tileImageList, _tileImageList, ownerDraw: false, loadTileImages: true);
        }

        private void SwitchToCompactTilesView()
        {
            SwitchViewMode(ApplicationConstants.ViewModeCompactTiles, _compactTileImageList, _compactTileImageList, ownerDraw: false, loadTileImages: true);
        }

        private void SwitchToLogosView()
        {
            SwitchViewMode(ApplicationConstants.ViewModeLogos, _logoImageList, _logoImageList, ownerDraw: false, loadTileImages: true);
        }

        private void SwitchToIconView()
        {
            SwitchViewMode(ApplicationConstants.ViewModeIcons, _largeImageList, _smallImageList, ownerDraw: false, loadTileImages: false);
        }

        private void SwitchToDetailsView()
        {
            var columnOrder = _appDataService.GetDetailsColumnOrder();
            SwitchViewMode(
                ApplicationConstants.ViewModeDetails,
                _largeImageList,
                _smallImageList,
                ownerDraw: true,
                loadTileImages: false,
                detailsColumnOrder: columnOrder);
        }

        private void SwitchViewMode(
            string viewMode,
            ImageList largeList,
            ImageList smallList,
            bool ownerDraw,
            bool loadTileImages,
            Action extraSetup = null,
            string detailsColumnOrder = null,
            string detailsColumnWidths = null)
        {
            _appDataService.SetViewMode(viewMode);
            lstGames.BeginUpdate();
            try
            {
                LoadGames(viewMode);
                string detailsOrderArg = null;
                string detailsWidthsArg = null;
                if (viewMode == ApplicationConstants.ViewModeDetails)
                {
                    detailsOrderArg = detailsColumnOrder ?? _appDataService.GetDetailsColumnOrder();
                    detailsWidthsArg = detailsColumnWidths ?? _appDataService.GetDetailsColumnWidths();
                }

                _gameDisplayService.SetViewMode(lstGames, viewMode, largeList, smallList, detailsOrderArg, detailsWidthsArg);
                lstGames.OwnerDraw = ownerDraw;
                if (loadTileImages)
                    StartLoadTileImages(viewMode);
                extraSetup?.Invoke();
                if (viewMode == ApplicationConstants.ViewModeDetails)
                {
                    UpdateDetailsGameListColumns();
                    _persistedDetailsColumnWidths = detailsWidthsArg;
                }

                ApplySort(_appDataService.GetSortBy(), _appDataService.GetSortDirection());
                UpdateViewMenuCheckMarks();
            }
            finally
            {
                lstGames.EndUpdate();
            }
        }

        private void SortByNameAsc() => ApplySort(ApplicationConstants.SortByName, ApplicationConstants.SortDirectionAsc);

        private void SortByNameDesc() => ApplySort(ApplicationConstants.SortByName, ApplicationConstants.SortDirectionDesc);

        private void SortByAppIdAsc() => ApplySort(ApplicationConstants.SortByAppId, ApplicationConstants.SortDirectionAsc);

        private void SortByAppIdDesc() => ApplySort(ApplicationConstants.SortByAppId, ApplicationConstants.SortDirectionDesc);

        private void SortByNone() => ApplySort(ApplicationConstants.SortByNone, ApplicationConstants.SortDirectionAsc);

        private void OnSortNameAsc_Click(object sender, EventArgs e) => SortByNameAsc();

        private void OnSortNameDesc_Click(object sender, EventArgs e) => SortByNameDesc();

        private void OnSortAppIdAsc_Click(object sender, EventArgs e) => SortByAppIdAsc();

        private void OnSortAppIdDesc_Click(object sender, EventArgs e) => SortByAppIdDesc();

        private void OnSortNone_Click(object sender, EventArgs e) => SortByNone();

        private void OnBarViewRefresh_Click(object sender, EventArgs e) => RefreshGamesImmediate();

        private void OnCtxViewRefresh_Click(object sender, EventArgs e) => RefreshGamesImmediate();

        private void OnForkSelect_Click(object sender, EventArgs e)
        {
            using (var f = new ForkSelectForm())
            {
                if (f.ShowDialog(this) == DialogResult.OK)
                    Program.LogService?.LogMessage("Fork selection saved");
            }
        }

        private void OnCheckUpdates_Click(object sender, EventArgs e)
        {
            _ = OnCheckUpdatesAsync().ForgetFaults(Program.LogService, nameof(OnCheckUpdatesAsync));
        }

        private async Task OnCheckUpdatesAsync()
        {
            if (IsDisposed || Disposing)
                return;
            Program.LogService?.LogMessage("Manual update check triggered by user");
            _taskReportService.SetMessage("Checking for updates...");
            try
            {
                await EmulatorUpdateService.CheckForUpdatesWithUIAsync(
                    Program.LogService,
                    this,
                    isStartup: false,
                    onCheckStart: null,
                    onCheckComplete: null).ConfigureAwait(true);
                if (IsDisposed || Disposing)
                    return;
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Update check UI flow failed: {ex.Message}", ex);
                _taskReportService.SetMessage(ErrorDisplayHelper.SanitizeForUser("Update check", ex), TaskReportKind.Error);
            }
            finally
            {
                _taskReportService.SetMessage(string.Empty);
            }
        }

        private void OnReinstall_Click(object sender, EventArgs e)
        {
            _ = OnReinstallAsync().ForgetFaults(Program.LogService, nameof(OnReinstallAsync));
        }

        private async Task OnReinstallAsync()
        {
            if (IsDisposed || Disposing)
                return;

            var dialogResult = FormMessageBoxHelper.ShowDialogIfAlive(this,
                "This will download and reinstall Goldberg Emulator files.\n\nProceed?",
                "Reinstall",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question);

            if (dialogResult != DialogResult.OK)
                return;

            Program.LogService?.LogMessage("Manual reinstall triggered by user");
            _taskReportService.SetMessage("Reinstalling...");

            try
            {
                await EmulatorUpdateService.DownloadAndInstallWithUIAsync(Program.LogService, this).ConfigureAwait(true);
                if (IsDisposed || Disposing)
                    return;
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Reinstall failed: {ex.Message}", ex);
                _taskReportService.SetMessage(ErrorDisplayHelper.SanitizeForUser("Reinstall", ex), TaskReportKind.Error);
            }
            finally
            {
                _taskReportService.SetMessage(string.Empty);
            }
        }

        private async void OnAddGame_Click(object sender, EventArgs e)
        {
            var warmupCts = new System.Threading.CancellationTokenSource();
            // Warm the anonymous Steam session while the user picks a file, so the later 5s
            // metadata fetch reuses a live connection instead of timing out on a cold connect.
            Task warmupTask = ServiceLocator.SteamProductInfoService.PreWarmSessionAsync(warmupCts.Token);
            bool proceeded = false;
            try
            {
                string executablePath = SelectGameExecutable();
                if (string.IsNullOrEmpty(executablePath))
                    return;

                proceeded = true;
                await AddGameFromExecutable(executablePath);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Error adding game: {ex.Message}", ex);
                if (!IsDisposed && !Disposing)
                    _taskReportService.SetMessage(ErrorDisplayHelper.SanitizeForUser("Adding game", ex), TaskReportKind.Error);
            }
            finally
            {
                if (!proceeded)
                    warmupCts.Cancel();

                try
                {
                    await warmupTask.ConfigureAwait(true);
                }
                catch
                {
                }

                // User backed out of file selection: drop the connection we warmed for nothing.
                if (!proceeded)
                    await ServiceLocator.SteamProductInfoService.CloseSessionAsync().ConfigureAwait(true);

                warmupCts.Dispose();
            }
        }

        private async Task AddGameFromExecutable(string executablePath)
        {
            try
            {
                if (IsDisposed || Disposing || string.IsNullOrWhiteSpace(executablePath))
                    return;

                _pendingAddGameListService.SetDraft(PendingAddGameListService.CreateDraftFromExecutable(executablePath));
                ShowPendingAddInList();

                _taskReportService.SetProgress(0, 0);

                GameAddCollectResult collectResult = await ServiceLocator.GameAddCollector
                    .CollectFromExecutableAsync(executablePath, this, _taskReportService)
                    .ConfigureAwait(false);

                if (IsDisposed || Disposing)
                    return;
                if (collectResult.Cancelled)
                {
                    ClearPendingAddListEntry();
                    if (collectResult.MetadataFetchFailed)
                        _taskReportService.SetMessage("Could not fetch app data from Steam.", TaskReportKind.Error);
                    else
                        _taskReportService.SetMessageWithAutoClear("Adding game cancelled.", delayMs: AddGameStatusMessages.StatusAutoClearDelayMs);
                    return;
                }

                if (collectResult.Bundle?.Game == null)
                {
                    ClearPendingAddListEntry();
                    _taskReportService.SetMessage("Could not collect game data.", TaskReportKind.Error);
                    return;
                }

                _pendingAddGameListService.ApplyCollectedGame(collectResult.Bundle.Game);
                UpdatePendingAddInList();

                GameConfig gameConfig = collectResult.Bundle.Game;
                OnlineAppData metadata = collectResult.Bundle.Metadata;

                if (IsDisposed || Disposing)
                    return;

                _taskReportService.SetProgress(0, 0);
                if (!await OpenGameSettingsFormAsync(gameConfig, metadata, collectResult.Bundle).ConfigureAwait(true))
                    _taskReportService.SetMessageWithAutoClear("Adding game cancelled.", delayMs: AddGameStatusMessages.StatusAutoClearDelayMs);
            }
            catch (Exception ex)
            {
                ClearPendingAddListEntry();
                Program.LogService?.LogError($"Error adding game: {ex.Message}", ex);
                _taskReportService.SetMessage(ErrorDisplayHelper.SanitizeForUser("Adding game", ex), TaskReportKind.Error);
            }
        }

        private async Task<bool> OpenGameSettingsFormAsync(GameConfig gameConfig, OnlineAppData metadata, GameAddBundle addBundle = null)
        {
            bool existedBeforeDialog = _gameDataService.GetGame(gameConfig.GameGuid) != null;
            PendingAddGameSave pendingAddSave = null;
            using (var gameSettingsForm = new GameSettingsForm(
                gameConfig,
                isEditMode: false,
                metadata: metadata,
                feedbackService: _taskReportService,
                onSaveCompleted: null,
                addBundle: addBundle))
            {
                DialogResult dialogResult = gameSettingsForm.ShowDialog(this);
                pendingAddSave = gameSettingsForm.PendingAddSave;
                bool existsAfterDialog = _gameDataService.GetGame(gameConfig.GameGuid) != null;
                bool gameWasAdded = !existedBeforeDialog && existsAfterDialog;

                if (dialogResult == DialogResult.Retry && gameSettingsForm.EditExistingGameGuid != Guid.Empty)
                {
                    ClearPendingAddListEntry();
                    _taskReportService.SetMessageWithAutoClear("Opening the existing game for edit.", delayMs: AddGameStatusMessages.StatusAutoClearDelayMs);
                    EditGame(gameSettingsForm.EditExistingGameGuid);
                    return true;
                }

                if (dialogResult == DialogResult.OK || gameWasAdded)
                {
                    if (pendingAddSave != null)
                        return await CompletePendingAddSaveAsync(pendingAddSave).ConfigureAwait(true);
                    return true;
                }

                ClearPendingAddListEntry();
                return false;
            }
        }

        private async Task<bool> CompletePendingAddSaveAsync(PendingAddGameSave pending)
        {
            if (pending?.GameConfig == null)
                return false;

            GameConfig draftToRestore = pending.GameConfig;
            Guid savedGameGuid = draftToRestore.GameGuid;
            _pendingAddGameListService.Clear();

            GameSettingsSnapshot snapshot = pending.SettingsSnapshot ?? new GameSettingsSnapshot { AppId = pending.GameConfig.AppId };

            var formSaveRequest = new GameSettingsSaveRequest
            {
                GameConfig = pending.GameConfig,
                IsEditMode = false,
                Metadata = pending.Metadata,
                CustomStatsRawJson = pending.CustomStatsRawJson,
                TaskReportService = _taskReportService,
                SuppressStatusMessages = true,
                BuildSnapshot = () => snapshot,
                ResolveAchievementLanguage = s =>
                {
                    if (!string.IsNullOrEmpty(s?.User?.Language))
                        return s.User.Language;
                    return ServiceLocator.EmulatorConfigService.GetLanguageForAchievements(pending.GameConfig.AppId);
                },
                SaveDlcAndPaths = pending.SaveDlcAndPaths,
                SaveAdditionalGoldbergFiles = () => SaveAdditionalFilesFromPending(pending),
                OnAssetsDownloaded = () => NotifyAddSaveListChanged(savedGameGuid, reloadMosaic: true),
                OnSuccessfulSaveCompleted = () => NotifyAddSaveListChanged(savedGameGuid, reloadMosaic: false)
            };

            try
            {
                GameSettingsSaveResult saveResult = await ServiceLocator.GameSaveWriter.SaveAddAsync(new GameSaveAddRequest
                {
                    GameConfig = pending.GameConfig,
                    Metadata = pending.Metadata,
                    AchievementPreview = pending.AchievementPreview,
                    FormSaveRequest = formSaveRequest,
                    TaskReportService = _taskReportService,
                    OnAssetsDownloaded = formSaveRequest.OnAssetsDownloaded,
                    OnSuccessfulSaveCompleted = formSaveRequest.OnSuccessfulSaveCompleted,
                    CredentialsTouched = pending.CredentialsTouched
                }).ConfigureAwait(true);

                if (!saveResult.IsSuccess)
                {
                    if (_gameDataService.GetGame(draftToRestore.GameGuid) == null)
                    {
                        _pendingAddGameListService.SetDraft(draftToRestore);
                        ShowPendingAddInList();
                    }

                    if (saveResult.HasCustomStatsJsonError)
                    {
                        FormMessageBoxHelper.ShowIfAlive(this,
                            "Custom stats contain invalid JSON. Please fix the format before saving.",
                            "Invalid JSON",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                    else if (!string.IsNullOrWhiteSpace(saveResult.ErrorMessage))
                    {
                        _taskReportService.SetMessage(saveResult.ErrorMessage, TaskReportKind.Error);
                    }
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                if (_gameDataService.GetGame(draftToRestore.GameGuid) == null)
                {
                    _pendingAddGameListService.SetDraft(draftToRestore);
                    ShowPendingAddInList();
                }

                Program.LogService?.LogError($"Error saving game: {ex.Message}", ex);
                _taskReportService.SetMessage(ErrorDisplayHelper.SanitizeForUser("Saving game", ex), TaskReportKind.Error);
                return false;
            }
        }

        private void ClearPendingAddListEntry()
        {
            if (!_pendingAddGameListService.HasDraft)
                return;

            Guid gameGuid = _pendingAddGameListService.GetDraft().GameGuid;
            string mosaicKey = _pendingAddMosaicImageKey;
            _pendingAddGameListService.Clear();
            _pendingAddMosaicImageKey = null;
            RemovePendingAddFromListView(gameGuid, mosaicKey);
        }

        private void ShowPendingAddInList()
        {
            var draft = _pendingAddGameListService.GetDraft();
            if (draft == null)
                return;

            var viewMode = _appDataService.GetViewMode();
            var tileImageList = GetTileImageListForViewMode(viewMode);
            _gameDisplayService.SyncPendingAddListItem(
                lstGames,
                draft,
                viewMode,
                tileImageList ?? _largeImageList,
                tileImageList ?? _smallImageList,
                GetImportPendingPredicate(),
                GetAddPendingPredicate());

            _pendingAddMosaicImageKey = GameDisplayService.GetMosaicImageKey(draft);
            EnsurePendingAddItemVisible();

            if (IsMosaicViewMode(viewMode))
                _ = UpsertMosaicTileForGameAsync(draft, viewMode).ForgetFaults(Program.LogService, nameof(UpsertMosaicTileForGameAsync));
        }

        private void UpdatePendingAddInList()
        {
            var draft = _pendingAddGameListService.GetDraft();
            if (draft == null)
                return;

            string priorMosaicKey = _pendingAddMosaicImageKey;
            string newMosaicKey = GameDisplayService.GetMosaicImageKey(draft);
            var viewMode = _appDataService.GetViewMode();
            var tileImageList = GetTileImageListForViewMode(viewMode);
            var item = GameDisplayService.FindListItemByGameGuid(lstGames, draft.GameGuid);
            if (item != null)
            {
                _gameDisplayService.UpdateListViewItem(
                    item,
                    draft,
                    viewMode,
                    tileImageList ?? _largeImageList,
                    tileImageList ?? _smallImageList,
                    GetImportPendingPredicate(),
                    GetAddPendingPredicate());
            }
            else
            {
                ShowPendingAddInList();
                return;
            }

            _pendingAddMosaicImageKey = newMosaicKey;
            if (IsMosaicViewMode(viewMode)
                && !string.Equals(priorMosaicKey, newMosaicKey, StringComparison.Ordinal))
            {
                if (!string.IsNullOrEmpty(priorMosaicKey))
                    RemoveMosaicImageKey(priorMosaicKey);
                _ = UpsertMosaicTileForGameAsync(draft, viewMode).ForgetFaults(Program.LogService, nameof(UpsertMosaicTileForGameAsync));
            }
        }

        private void RemovePendingAddFromListView(Guid gameGuid, string mosaicImageKey)
        {
            if (gameGuid == Guid.Empty)
                return;

            _gameDisplayService.RemoveListItemByGameGuid(lstGames, gameGuid);
            if (!string.IsNullOrEmpty(mosaicImageKey))
                RemoveMosaicImageKey(mosaicImageKey);
            lstGames.Invalidate();
        }

        private void EnsurePendingAddItemVisible()
        {
            var draft = _pendingAddGameListService.GetDraft();
            if (draft == null)
                return;

            var item = GameDisplayService.FindListItemByGameGuid(lstGames, draft.GameGuid);
            item?.EnsureVisible();
        }

        private List<GameConfig> GetGamesForListDisplay()
        {
            return _pendingAddGameListService.MergeInto(_gameDataService.GetAllGames());
        }

        private Func<GameConfig, bool> GetAddPendingPredicate()
        {
            return _pendingAddGameListService.IsPendingGame;
        }

        private static void SaveAdditionalFilesFromPending(PendingAddGameSave pending)
        {
            GameSettingsForm.SaveAdditionalFilesFromRequest(pending?.AdditionalFilesSaveRequest, null);
        }

        private string SelectGameExecutable()
        {
            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = ApplicationConstants.ExecutableFileFilter;
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;
                openFileDialog.Title = "Select Game Executable";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    return openFileDialog.FileName;
                }
            }
            return null;
        }


        private void OnAbout_Click(object sender, EventArgs e)
        {
            using (var aboutForm = new AboutForm())
            {
                aboutForm.ShowDialog();
            }
        }

        private void OnCheckLauncherUpdates_Click(object sender, EventArgs e)
        {
            _ = OnCheckLauncherUpdatesAsync().ForgetFaults(Program.LogService, nameof(OnCheckLauncherUpdatesAsync));
        }

        private async Task OnCheckLauncherUpdatesAsync()
        {
            if (IsDisposed || Disposing)
                return;
            Program.LogService?.LogMessage("Manual launcher update check triggered by user");
            _taskReportService.SetMessage("Checking for launcher updates...");
            try
            {
                await LauncherUpdateService.CheckForUpdatesWithUIAsync(Program.LogService, this, isStartup: false)
                    .ConfigureAwait(true);
                if (IsDisposed || Disposing)
                    return;
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Launcher update check UI flow failed: {ex.Message}", ex);
                _taskReportService.SetMessage(ErrorDisplayHelper.SanitizeForUser("Launcher update check", ex), TaskReportKind.Error);
            }
            finally
            {
                _taskReportService.SetMessage(string.Empty);
            }
        }

        private void OnExit_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void OnRunGame_Click(object sender, EventArgs e) => LaunchSelectedGame(useEmulator: true);

        private void OnRunWithoutEmu_Click(object sender, EventArgs e) => LaunchSelectedGame(useEmulator: false);

        private void OnEditGame_Click(object sender, EventArgs e)
        {
            if (GetSelectedGame() is GameConfig game)
            {
                if (_pendingAddGameListService.IsPendingGame(game))
                    return;
                EditGame(game.GameGuid);
            }
        }

        private void OnRemoveGame_Click(object sender, EventArgs e)
        {
            var games = GetSelectedGames();
            if (games.Count == 0)
                return;

            if (games.All(g => _pendingAddGameListService.IsPendingGame(g)))
            {
                ClearPendingAddListEntry();
                _taskReportService.SetMessageWithAutoClear("Adding game cancelled.", delayMs: AddGameStatusMessages.StatusAutoClearDelayMs);
                return;
            }

            games = games.Where(g => !_pendingAddGameListService.IsPendingGame(g)).ToList();
            if (games.Count == 0)
                return;

            var (confirmed, deleteFiles) = RemovesGameForm.Show(games, this);
            if (!confirmed)
                return;

            int removed = 0;
            string lastError = null;
            foreach (var game in games)
            {
                var removeResult = _gameDataService.RemoveGame(game.GameGuid, deleteFiles);
                if (removeResult.IsValid)
                {
                    removed++;
                }
                else
                {
                    lastError = removeResult.ErrorMessage;
                    Program.LogService?.LogError($"Failed to remove game {game.AppName}: {removeResult.ErrorMessage}");
                }
            }

            if (removed > 0)
            {
                RefreshGames();
                _taskReportService.SetMessageWithAutoClear(
                    removed == 1 ? $"{games[0].AppName} removed." : $"{removed} games removed.",
                    TaskReportKind.Info);
            }

            if (removed < games.Count && !string.IsNullOrWhiteSpace(lastError))
            {
                string userMessage = "Failed to remove some games from library.";
                if (!lastError.Contains("\\") && !lastError.Contains("/"))
                    userMessage = $"Failed to remove game: {lastError}";
                FormMessageBoxHelper.ShowIfAlive(this, userMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void OnGenerateAchievements_Click(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing)
                return;
            if (!TryGetSelectedGameWithAppId(out GameConfig selectedGame))
                return;

            Program.LogService?.LogMessage($"Starting achievement generation for game: {selectedGame.AppName} (App ID: {selectedGame.AppId})");

            try
            {
                var feedbackService = GetLocatorTaskReportOrNull();
                await ServiceLocator.GoldbergArtifactService
                    .GenerateAchievementsFromMenuAsync(selectedGame, feedbackService)
                    .ConfigureAwait(true);

                if (IsDisposed || Disposing)
                    return;
                Program.LogService?.LogMessage("Achievement generation completed successfully");
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Failed to generate achievements: {ex.Message}", ex);
                FormMessageBoxHelper.ShowIfAlive(this, "Failed to generate achievements. Please check the SmartGoldbergEmu log for details.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnGameProperties_Click(object sender, EventArgs e)
        {
            if (GetSelectedGame() is GameConfig game)
                EditGame(game.GameGuid);
        }

        private async void OnApplySteamless_Click(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing)
                return;

            var game = GetSelectedGame();
            if (game == null)
                return;

            if (!TryResolveExecutableForSteamless(game, out string executablePath))
            {
                ShowSteamlessApplyFeedback(game.AppName, new SteamlessApplyResult { Outcome = SteamlessApplyOutcome.ExecutablePathInvalid });
                return;
            }

            if (!TryEnsureSteamlessCliPath())
                return;

            if (!SteamlessOptionsForm.TryShow(game.AppName, executablePath, this, out SteamlessCliOptions cliOptions))
                return;

            Program.LogService?.LogMessage($"Running Steamless on {game.AppName}: {executablePath}");
            _taskReportService.StartProgress(SteamlessFeedback.Progress(game.AppName));
            prgFeedback.Style = ProgressBarStyle.Marquee;

            try
            {
                var result = await ServiceLocator.SteamlessService.ApplySteamlessAsync(executablePath, cliOptions, Program.LogService).ConfigureAwait(true);
                if (IsDisposed || Disposing)
                    return;

                ShowSteamlessApplyFeedback(game.AppName, result);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Running Steamless on {game.AppName} failed.", ex);
                ShowSteamlessApplyFeedback(game.AppName, new SteamlessApplyResult
                {
                    Outcome = SteamlessApplyOutcome.Unexpected,
                    LogDetail = ex.Message
                });
            }
            finally
            {
                if (!IsDisposed && !Disposing)
                    prgFeedback.Style = ProgressBarStyle.Blocks;
            }
        }

        private void ShowSteamlessApplyFeedback(string gameName, SteamlessApplyResult result)
        {
            if (result == null)
                return;

            if (!string.IsNullOrWhiteSpace(result.LogDetail))
                Program.LogService?.LogMessage("Steamless detail: " + result.LogDetail);

            if (SteamlessFeedback.UsePopupForOutcome(result.Outcome))
            {
                FormMessageBoxHelper.ShowIfAlive(
                    this,
                    SteamlessFeedback.PopupMessage(result.Outcome, gameName, result.LogDetail),
                    SteamlessFeedback.DialogTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            string status = SteamlessFeedback.StatusMessage(result.Outcome, gameName);
            if (string.IsNullOrEmpty(status))
                return;

            int delayMs = result.Outcome == SteamlessApplyOutcome.Success ? 6000 : 8000;
            _taskReportService.SetMessageWithAutoClear(status, SteamlessFeedback.StatusKindForOutcome(result.Outcome), delayMs);
        }

        private async void OnGenerateItems_Click(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing)
                return;
            if (!TryGetSelectedGameWithAppId(out GameConfig selectedGame))
                return;

            Program.LogService?.LogMessage($"Starting {PathConstants.GoldbergItemsJsonFileName} generation for game: {selectedGame.AppName} (App ID: {selectedGame.AppId})");

            try
            {
                var feedbackService = GetLocatorTaskReportOrNull();
                var result = await ServiceLocator.GoldbergArtifactService
                    .GenerateItemsFromMenuAsync(selectedGame, feedbackService)
                    .ConfigureAwait(true);
                if (IsDisposed || Disposing)
                    return;
                if (!result.Success)
                {
                    FormMessageBoxHelper.ShowIfAlive(this, result.ErrorMessage, "Generate Items", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Program.LogService?.LogMessage($"{PathConstants.GoldbergItemsJsonFileName} generation completed successfully");
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Failed to generate {PathConstants.GoldbergItemsJsonFileName}: {ex.Message}", ex);
                FormMessageBoxHelper.ShowIfAlive(this, $"Failed to generate {PathConstants.GoldbergItemsJsonFileName}. Please check the SmartGoldbergEmu log for details.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnOpenValveDataFile_Click(object sender, EventArgs e)
        {
            if (!TryGetSelectedGameWithAppId(out GameConfig game))
                return;

            try
            {
                string valveDataPath = PathConstants.CombineGamesPerAppValveDataFilePath(
                    PathConstants.GamesDirectory,
                    game.AppId.ToString());

                if (!PathValidationHelper.IsSafeFilePath(valveDataPath))
                {
                    FormMessageBoxHelper.ShowIfAlive(this, "Invalid Valve data file path detected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (!ShellFolderHelper.TryOpenFile(valveDataPath, out string errorMessage))
                {
                    string body = errorMessage != null && errorMessage.StartsWith("File does not exist", StringComparison.Ordinal)
                        ? errorMessage + "\n\nSave the game to export it from Steam."
                        : errorMessage ?? "Failed to open Valve data file.";
                    FormMessageBoxHelper.ShowIfAlive(this, body, "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Failed to open Valve data file: {ex.Message}", ex);
                FormMessageBoxHelper.ShowIfAlive(this, "Failed to open Valve data file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnOpenSteamStore_Click(object sender, EventArgs e) =>
            OpenExternalUrl(ApplicationConstants.SteamStoreAppUrlFormat, "store page");

        private void OnOpenSteamCommunity_Click(object sender, EventArgs e) =>
            OpenExternalUrl(ApplicationConstants.SteamCommunityAppUrlFormat, "community page");

        private void OnOpenSteamWorkshop_Click(object sender, EventArgs e) =>
            OpenExternalUrl(ApplicationConstants.SteamCommunityWorkshopUrlFormat, "workshop page");

        private void OnOpenSteamDb_Click(object sender, EventArgs e) =>
            OpenExternalUrl(ApplicationConstants.SteamDbAppUrlFormat, "SteamDB page");

        private void OpenExternalUrl(string urlFormat, string pageName)
        {
            if (!TryGetSelectedGameWithAppId(out GameConfig game))
                return;

            string url = string.Format(urlFormat, game.AppId);
            if (!PathValidationHelper.IsSafeUrl(url))
            {
                FormMessageBoxHelper.ShowIfAlive(this, "Invalid URL format detected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                Process.Start(url);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Failed to open {pageName}: {ex.Message}", ex);
                FormMessageBoxHelper.ShowIfAlive(this, $"Failed to open {pageName}.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnOpenGameDependencies_Click(object sender, EventArgs e) =>
            OpenExternalUrl(ApplicationConstants.SteamDbDepotsUrlFormat, "SteamDB depots page");

        private void OnOpenLauncherOptions_Click(object sender, EventArgs e) =>
            OpenExternalUrl(ApplicationConstants.SteamDbConfigUrlFormat, "SteamDB config page");

        private void OnOpenExecutableFolder_Click(object sender, EventArgs e)
        {
            var game = GetSelectedGame();
            if (game == null || string.IsNullOrEmpty(game.Path))
            {
                FormMessageBoxHelper.ShowIfAlive(this, "Please select a game with a valid executable path.", "No Game Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!GameFolderPathHelper.TryGetExecutableDirectory(game, out string folderPath))
            {
                FormMessageBoxHelper.ShowIfAlive(this, "Invalid folder path detected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ShellFolderHelper.OpenFolderForOwner(
                this,
                folderPath,
                createIfMissing: false,
                "Folder Not Found",
                "Failed to open folder",
                restrictToAppInstallTree: false);
        }

        private void OnOpenSettingsFolder_Click(object sender, EventArgs e)
        {
            if (!TryGetSelectedGameWithAppId(out GameConfig game))
                return;

            string settingsPath = ServiceLocator.EmulatorConfigService.GetGameSteamSettingsPath(game.AppId);
            ShellFolderHelper.OpenFolderForOwner(this, settingsPath, createIfMissing: true, "Error", "Failed to open settings folder");
        }

        private void OnOpenGoldbergFolder_Click(object sender, EventArgs e) =>
            ShellFolderHelper.OpenFolderForOwner(this, PathConstants.GoldbergDirectory, createIfMissing: true, "Error", "Failed to open Goldberg folder");

        private void OnOpenExtraDllsFolder_Click(object sender, EventArgs e)
        {
            string extraDllsDir = ServiceLocator.GoldbergFilesService.EnsureSteamClientExtraDllsDirectory();
            ShellFolderHelper.OpenFolderForOwner(this, extraDllsDir, createIfMissing: true, "Error", "Failed to open extra DLLs folder");
        }

        private void OnOpenInventoryFile_Click(object sender, EventArgs e)
        {
            if (!TryGetSelectedGameWithAppId(out GameConfig game))
                return;

            string settingsPath = ServiceLocator.EmulatorConfigService.GetGameSteamSettingsPath(game.AppId);
            string inventoryPath = Path.Combine(settingsPath, PathConstants.GoldbergItemsJsonFileName);

            if (!ShellFolderHelper.TryOpenFile(inventoryPath, out string errorMessage))
            {
                var icon = errorMessage != null && errorMessage.StartsWith("File does not exist", StringComparison.Ordinal)
                    ? MessageBoxIcon.Information
                    : MessageBoxIcon.Error;
                string title = icon == MessageBoxIcon.Information ? "File Not Found" : "Error";
                string body = icon == MessageBoxIcon.Information
                    ? errorMessage + "\n\nYou may need to generate items first."
                    : errorMessage ?? "Failed to open inventory file.";
                FormMessageBoxHelper.ShowIfAlive(this, body, title, MessageBoxButtons.OK, icon);
            }
        }

        private void OnCreateSteamAppIdFile_Click(object sender, EventArgs e)
        {
            var game = GetSelectedGame();
            if (game == null || game.AppId == 0 || string.IsNullOrEmpty(game.Path))
            {
                FormMessageBoxHelper.ShowIfAlive(this, "Please select a game with a valid App ID and executable path.", "No Game Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ValidationResult result = ServiceLocator.EmulatorConfigService.TryEnsureSteamAppIdBesideExecutable(game);
            if (result.IsValid)
            {
                FormMessageBoxHelper.ShowIfAlive(this,
                    $"{PathConstants.SteamAppIdFileName} for appid {game.AppId} created successfully.",
                    "File Created",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            string message = result.ErrorMessage ?? "Failed to create file.";
            MessageBoxIcon icon = message.IndexOf("does not exist", StringComparison.OrdinalIgnoreCase) >= 0
                ? MessageBoxIcon.Warning
                : MessageBoxIcon.Error;
            string title = icon == MessageBoxIcon.Warning ? "Folder Not Found" : "Error";
            FormMessageBoxHelper.ShowIfAlive(this, message, title, MessageBoxButtons.OK, icon);
        }

        private void OnCopyGuid_Click(object sender, EventArgs e)
        {
            var game = GetSelectedGame();
            if (game != null)
            {
                try
                {
                    Clipboard.SetText(game.GameGuid.ToString());
                    FormMessageBoxHelper.ShowIfAlive(this, $"Entry GUID copied to clipboard:\n{game.GameGuid}", "GUID copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    FormMessageBoxHelper.ShowIfAlive(this, $"Failed to copy entry GUID: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                FormMessageBoxHelper.ShowIfAlive(this, "Please select a game.", "No Game Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void OnCreateShortcut_Click(object sender, EventArgs e)
        {
            var game = GetSelectedGame();
            if (game == null)
            {
                FormMessageBoxHelper.ShowIfAlive(this, "Please select a game.", "No Game Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!UriProtocolRegistryService.IsProtocolRegistered())
            {
                FormMessageBoxHelper.ShowIfAlive(this,
                    $"The {ApplicationConstants.UriProtocolAuthorityPrefix} protocol is not registered. Please restart the application to register it automatically.\n\n" +
                    "If the problem persists, try running the application as administrator.",
                    "Protocol Not Registered",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            try
            {
                using (var saveFileDialog = new SaveFileDialog())
                {
                    saveFileDialog.Filter = ApplicationConstants.ShortcutFileFilter;
                    string sanitizedName = ShortcutService.SanitizeFileName(game.AppName);
                    saveFileDialog.FileName = $"{sanitizedName}.url";
                    saveFileDialog.Title = "Create Shortcut";
                    
                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        GameFolderPathHelper.TryResolveIconSourcePath(game, out string iconPath);
                        if (string.IsNullOrEmpty(iconPath))
                            iconPath = null;

                        if (ShortcutService.Create(saveFileDialog.FileName, game.AppId, game.AppName, iconPath))
                        {
                            FormMessageBoxHelper.ShowIfAlive(this,
                                $"Shortcut created successfully:\n{saveFileDialog.FileName}",
                                "Shortcut Created",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        }
                        else
                        {
                            FormMessageBoxHelper.ShowIfAlive(this,
                                "Failed to create shortcut. Please check the file path and permissions.",
                                "Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Failed to create shortcut: {ex.Message}", ex);
                FormMessageBoxHelper.ShowIfAlive(this,
                    "Failed to create shortcut. Please check the file path and permissions.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void ctxGamesItem_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            miCtxRowCreateShortcut.Enabled = true;
            var game = GetSelectedGame();
            miCtxRowOpenValveDataFile.Enabled = game != null && game.AppId > 0;

            miCtxRowApplySteamless.Visible = true;
            bool canApplySteamless = game != null && TryResolveExecutableForSteamless(game, out _);
            miCtxRowApplySteamless.Enabled = canApplySteamless;
            if (game != null && !canApplySteamless)
                Program.LogService?.LogDebug($"Steamless disabled: could not resolve executable (StartFolder={game.StartFolder}, Path={game.Path}).");
        }

        private bool TryResolveExecutableForSteamless(GameConfig game, out string fullExecutablePath)
        {
            return GameFolderPathHelper.TryResolveExecutableForSteamless(game, out fullExecutablePath);
        }

        private bool TryEnsureSteamlessCliPath()
        {
            if (ServiceLocator.SteamlessService.TryGetConfiguredCli(out _, out _))
                return true;

            string setupPrompt = ServiceLocator.SteamlessService.HasInvalidSavedCliPath()
                ? SteamlessFeedback.NotInstalledPopupBody()
                : SteamlessFeedback.NotConfiguredDisclaimerBody();

            var disclaimer = FormMessageBoxHelper.ShowDialogIfAlive(
                this,
                setupPrompt,
                SteamlessFeedback.DialogTitle,
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Information);
            if (disclaimer != DialogResult.OK)
                return false;

            string selectedCliPath = PromptSelectSteamlessCli();
            if (string.IsNullOrEmpty(selectedCliPath))
                return false;

            var saveResult = ServiceLocator.SteamlessService.TryPersistCliPath(selectedCliPath, out _);
            if (!saveResult.IsValid)
            {
                FormMessageBoxHelper.ShowIfAlive(
                    this,
                    saveResult.ErrorMessage,
                    SteamlessFeedback.DialogTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }

            Program.LogService?.LogMessage("Steamless CLI path saved.");
            return true;
        }

        private string PromptSelectSteamlessCli()
        {
            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = ApplicationConstants.SteamlessCliFileDialogFilter;
                openFileDialog.FilterIndex = 1;
                openFileDialog.FileName = PathConstants.SteamlessCliExecutableName;
                openFileDialog.RestoreDirectory = false;
                string browseRoot = ServiceLocator.SteamlessService.GetCliBrowseInitialDirectory();
                if (!string.IsNullOrEmpty(browseRoot))
                    openFileDialog.InitialDirectory = browseRoot;
                openFileDialog.Title = "Select Steamless.CLI.exe";
                openFileDialog.CheckFileExists = true;

                if (openFileDialog.ShowDialog(this) == DialogResult.OK)
                    return openFileDialog.FileName;
            }

            return null;
        }

        private void ctxGamesView_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            UpdateViewMenuCheckMarks();
            UpdateSortMenuCheckMarks();
        }

        private void lstGames_ItemActivate(object sender, EventArgs e) => LaunchSelectedGame(useEmulator: true);

        private void lstGames_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                LaunchSelectedGame(useEmulator: true);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete)
            {
                OnRemoveGame_Click(sender, e);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F2)
            {
                OnEditGame_Click(sender, e);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F5)
            {
                RefreshGamesImmediate();
                e.Handled = true;
            }
            else if (e.KeyCode == (Keys)0x5D || (e.KeyCode == Keys.F10 && e.Shift))
            {
                SyncListViewContextMenuFromSelection();
            }
        }

        private void LaunchSelectedGame(bool useEmulator)
        {
            if (GetSelectedGame() is GameConfig game)
            {
                if (_pendingAddGameListService.IsPendingGame(game))
                {
                    _taskReportService.SetMessageWithAutoClear("Save the game before launching it.", delayMs: 6000);
                    return;
                }

                bool effectiveUseEmulator = ResolveUseEmulatorForLaunch(game, useEmulator);
                _ = LaunchGameInternalAsync(game, effectiveUseEmulator).ForgetFaults(Program.LogService, nameof(LaunchGameInternalAsync));
            }
        }

        private static bool ResolveUseEmulatorForLaunch(GameConfig game, bool useEmulator)
        {
            if (game != null && game.LaunchMode == GoldbergLaunchMode.NoEmulation)
                return false;
            return useEmulator;
        }

        public void LaunchGameByAppId(ulong appId)
        {
            try
            {
                var game = _gameDataService.GetGameByAppId(appId);
                if (game != null)
                {
                    bool useEmulator = ResolveUseEmulatorForLaunch(game, useEmulator: true);
                    _ = LaunchGameInternalAsync(game, useEmulator).ForgetFaults(Program.LogService, nameof(LaunchGameInternalAsync));
                }
                else
                {
                    ShowGameNotFoundMessage(appId);
                }
            }
            catch (Exception ex)
            {
                ShowLaunchErrorMessage(ex.Message);
            }
        }

        private bool TryValidateSteamApiBeforeLaunch(GameConfig game, bool useEmulator)
        {
            if (useEmulator && game != null && game.LaunchMode != GoldbergLaunchMode.SteamDllBesideExe
                && game.LaunchMode == GoldbergLaunchMode.StandardSteamApi)
            {
                Program.LogService?.LogDebug(
                    "Skipping Steam API validation (standard Goldberg steam_api mode; emulator DLL is deployed on launch).");
                return true;
            }

            string validationRoot = ResolveSteamApiValidationRoot(game);
            if (string.IsNullOrEmpty(validationRoot))
            {
                Program.LogService?.LogDebug("Skipping Steam API validation (no validation root resolved)");
                return true;
            }

            Program.LogService?.LogDebug("Validating Steam API DLLs before launch");
            var apiStatus = SteamApiValidator.DetectAndValidateSteamApi(validationRoot);

            if (!((apiStatus.X32Found && !apiStatus.X32IsClean) || (apiStatus.X64Found && !apiStatus.X64IsClean)))
            {
                Program.LogService?.LogDebug("Steam API DLLs validation passed");
                return true;
            }

            Program.LogService?.LogWarning("Steam API DLLs appear to be modified or unknown versions");
            bool hasBackups = apiStatus.CleanBackups != null && apiStatus.CleanBackups.Count > 0;
            string message =
                "Modded Steam API DLLs found.\n\n" +
                (hasBackups
                    ? "A known-good file was found elsewhere in this folder (name contains \"steam_api\").\n\n"
                    : "No known-good alternate file was found (searched recursively; skipped folders that could not be read).\n\n") +
                "Yes — Restore and launch\n" +
                "No — Launch without restoring";
            var validationResult = FormMessageBoxHelper.ShowDialogIfAlive(this,
                message,
                "Steam API Validation",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (validationResult == DialogResult.Cancel)
            {
                Program.LogService?.LogMessage("User cancelled launch due to Steam API validation");
                return false;
            }
            if (validationResult == DialogResult.Yes)
            {
                if (!hasBackups)
                {
                    FormMessageBoxHelper.ShowIfAlive(this,
                        "No known-good Steam API file was found in the game folder to restore from.",
                        "Steam API Validation",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    Program.LogService?.LogMessage("User chose restore but no clean backup was found; continuing launch");
                }
                else
                {
                    int restored = SteamApiValidator.TryRestoreSteamApiFromCleanBackups(apiStatus, out string restoreError);
                    if (restored <= 0)
                    {
                        FormMessageBoxHelper.ShowIfAlive(this,
                            string.IsNullOrEmpty(restoreError) ? "Restore failed." : restoreError,
                            "Steam API Validation",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return false;
                    }
                    Program.LogService?.LogMessage($"Restored {restored} Steam API DLL(s) before launch");
                }
            }
            else
                Program.LogService?.LogMessage("User chose to launch without restoring Steam API DLLs");

            return true;
        }

        private static string ResolveSteamApiValidationRoot(GameConfig game)
        {
            string startFolder = (game?.StartFolder ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(startFolder) && Directory.Exists(startFolder))
                return Path.GetFullPath(startFolder);

            if (GameFolderPathHelper.TryResolveStoredExecutable(game, out string fullExePath))
            {
                string exeDir = Path.GetDirectoryName(fullExePath);
                if (!string.IsNullOrEmpty(exeDir) && Directory.Exists(exeDir))
                    return Path.GetFullPath(exeDir);
            }

            return null;
        }

        private async Task<bool> TryEnsureEmulatorPrerequisiteForLaunch(GameConfig game, bool useEmulator)
        {
            bool requireLaunchModeBinaries = useEmulator;
            ValidationResult prerequisite = _gameLaunchService.ValidateEmulatorFilesPrerequisite(game, requireLaunchModeBinaries);
            if (prerequisite.IsValid)
                return true;

            if (!await EmulatorUpdateService.TryEnsureGoldbergBinariesForLaunchAsync(Program.LogService, this).ConfigureAwait(true))
                return false;

            prerequisite = _gameLaunchService.ValidateEmulatorFilesPrerequisite(game, requireLaunchModeBinaries);
            if (prerequisite.IsValid)
                return true;

            ShowLaunchErrorMessage(prerequisite.ErrorMessage);
            return false;
        }

        private async Task LaunchGameInternalAsync(GameConfig game, bool useEmulator)
        {
            try
            {
                if (IsDisposed || Disposing)
                    return;
                Program.LogService?.LogDebug($"MainForm: LaunchGameInternalAsync called for {game?.AppName} (AppId: {game?.AppId}), useEmulator: {useEmulator}");

                if (_gameLaunchService.IsGameRunning(game.AppId, game))
                {
                    Program.LogService?.LogWarning($"Launch blocked: {game.AppName} is already running.");
                    ShowLaunchErrorMessage(
                        $"{game.AppName} is already running. Close the game before launching again.");
                    return;
                }

                if (!await TryEnsureEmulatorPrerequisiteForLaunch(game, useEmulator).ConfigureAwait(true))
                    return;

                if (IsDisposed || Disposing)
                    return;

                if (!TryValidateSteamApiBeforeLaunch(game, useEmulator))
                    return;

                Program.LogService?.LogDebug("Checking for launch options...");
                var launchResult = await _launchOptionService.ShowLaunchOptionsAsync(game, this).ConfigureAwait(true);
                if (launchResult.Cancelled)
                {
                    Program.LogService?.LogMessage("User cancelled launch options dialog");
                    return;
                }

                LaunchOption launchOption = launchResult.SkipLauncher ? null : launchResult.LaunchOption;
                Program.LogService?.LogDebug($"Launch option selected: {(launchOption != null ? launchOption.Description ?? launchOption.Executable : "None (default)")}, SkipLauncher: {launchResult.SkipLauncher}");

                Program.LogService?.LogDebug("Calling GameLaunchService.LaunchGame...");
                var launchGameResult = _gameLaunchService.LaunchGame(game, useEmulator: useEmulator, launchOption: launchOption);

                if (!launchGameResult.IsValid)
                {
                    Program.LogService?.LogError($"Launch failed: {launchGameResult.ErrorMessage}");
                    ShowLaunchErrorMessage(launchGameResult.ErrorMessage);
                }
                else
                {
                    Program.LogService?.LogDebug("Launch completed successfully from MainForm perspective");
                    _taskReportService.SetMessageWithAutoClear($"{game.AppName} launched.", TaskReportKind.Info);
                }
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError("Error during game launch", ex);
                ShowLaunchErrorMessage(ex.Message);
            }
        }

        private void ShowGameNotFoundMessage(ulong appId)
        {
            FormMessageBoxHelper.ShowIfAlive(this,
                $"Game with App ID {appId} not found in library.",
                "Game Not Found",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        private void ShowLaunchErrorMessage(string errorMessage)
        {
            FormMessageBoxHelper.ShowIfAlive(this,
                $"Failed to launch game: {errorMessage}",
                "Launch Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private void EditGame(Guid gameGuid)
        {
            var game = _gameDataService.GetGame(gameGuid);
            if (game == null)
                return;

            GameEditBundle editBundle = ServiceLocator.GameEditLoader.Load(game);

            using (var gameSettingsForm = new GameSettingsForm(
                game,
                isEditMode: true,
                metadata: null,
                feedbackService: _taskReportService,
                onSaveCompleted: RefreshGames,
                editBundle: editBundle))
            {
                if (gameSettingsForm.ShowDialog(this) != DialogResult.OK)
                    _taskReportService.Clear();
            }
        }

        private GameConfig GetSelectedGame()
        {
            if (lstGames.SelectedItems.Count == 0)
                return null;
            var tagged = lstGames.SelectedItems[0].Tag as GameConfig;
            if (tagged == null)
                return null;
            if (tagged.GameGuid == Guid.Empty)
                return tagged;
            return _gameDataService.GetGame(tagged.GameGuid) ?? tagged;
        }

        private List<GameConfig> GetSelectedGames()
        {
            var list = new List<GameConfig>();
            foreach (ListViewItem item in lstGames.SelectedItems)
            {
                if (item.Tag is GameConfig game)
                    list.Add(game);
            }
            return list;
        }

        private bool TryGetSelectedGameWithAppId(out GameConfig game)
        {
            game = GetSelectedGame();
            if (game != null && game.AppId > 0)
                return true;
            FormMessageBoxHelper.ShowIfAlive(this, "Please select a game with a valid App ID.", "No Game Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        private void lstGames_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    string ext = System.IO.Path.GetExtension(files[0]).ToLower();
                    if (ext == ".exe" || ext == ".bat")
                    {
                        e.Effect = DragDropEffects.Copy;
                        return;
                    }
                }
            }
            e.Effect = DragDropEffects.None;
        }

        private async void lstGames_DragDrop(object sender, DragEventArgs e)
        {
            try
            {
                string[] files = (string[])e.Data?.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    string executablePath = files[0];
                    string ext = System.IO.Path.GetExtension(executablePath).ToLower();
                    if (ext != ".exe" && ext != ".bat")
                        return;
                    await AddGameFromExecutable(executablePath);
                }
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Error in drag-drop add game: {ex.Message}", ex);
                if (!IsDisposed && !Disposing)
                    _taskReportService.SetMessage(ErrorDisplayHelper.SanitizeForUser("Adding game", ex), TaskReportKind.Error);
            }
        }

        private void ApplySort(string sortBy, string sortDirection)
        {
            _appDataService.SetSortBy(sortBy);
            _appDataService.SetSortDirection(sortDirection);

            if (sortBy == ApplicationConstants.SortByNone)
            {
                RefreshGames();
                UpdateSortMenuCheckMarks();
                return;
            }

            _gameDisplayService.ApplySort(lstGames, sortBy, sortDirection);
            UpdateSortMenuCheckMarks();

            if (_appDataService.GetViewMode() == ApplicationConstants.ViewModeDetails)
                lstGames.Invalidate();
        }

        private void RefreshLibraryFromImport()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(RefreshLibraryFromImport));
                return;
            }

            RefreshGamesImmediate();
        }

        private Func<ulong, bool> GetImportPendingPredicate()
        {
            return _legacyImportService != null
                ? new Func<ulong, bool>(_legacyImportService.IsImportPending)
                : null;
        }

        private void RefreshGames()
        {
            ScheduleRefreshGames(reloadTiles: true);
        }

        private void RefreshGamesImmediate()
        {
            FlushScheduledGameListRefresh();
            RefreshGamesCore(reloadTiles: true);
        }

        private void RunOnUiThread(Action action)
        {
            if (action == null)
                return;
            if (IsDisposed || Disposing)
                return;

            if (InvokeRequired)
                BeginInvoke(action);
            else
                action();
        }

        private void ScheduleRefreshGames(bool reloadTiles)
        {
            RunOnUiThread(() => ScheduleRefreshGamesOnUiThread(reloadTiles));
        }

        private void ScheduleRefreshGamesOnUiThread(bool reloadTiles)
        {
            if (reloadTiles)
                _gameListRefreshFullTiles = true;

            if (_gameListRefreshTimer == null)
            {
                _gameListRefreshTimer = new Timer { Interval = GameListRefreshDebounceMs };
                _gameListRefreshTimer.Tick += GameListRefreshTimer_Tick;
            }

            _gameListRefreshTimer.Stop();
            _gameListRefreshTimer.Start();
        }

        private void NotifyAddSaveListChanged(Guid gameGuid, bool reloadMosaic)
        {
            if (gameGuid == Guid.Empty)
                return;

            RunOnUiThread(() => ApplyAddSaveListUpdate(gameGuid, reloadMosaic));
        }

        private void GameListRefreshTimer_Tick(object sender, EventArgs e)
        {
            FlushScheduledGameListRefresh();
        }

        private void FlushScheduledGameListRefresh()
        {
            if (_gameListRefreshTimer != null)
                _gameListRefreshTimer.Stop();

            if (_gameListRefreshFullTiles)
            {
                _gameListRefreshFullTiles = false;
                RefreshGamesCore(reloadTiles: true);
            }
        }

        private void ApplyAddSaveListUpdate(Guid gameGuid, bool reloadMosaic)
        {
            var game = _gameDataService.GetGame(gameGuid);
            if (game == null)
            {
                ScheduleRefreshGames(reloadTiles: true);
                return;
            }

            string priorMosaicKey = _pendingAddMosaicImageKey;
            _pendingAddMosaicImageKey = null;

            var viewMode = _appDataService.GetViewMode();
            var tileImageList = GetTileImageListForViewMode(viewMode);
            var item = GameDisplayService.FindListItemByGameGuid(lstGames, gameGuid);
            if (item != null)
            {
                _gameDisplayService.UpdateListViewItem(
                    item,
                    game,
                    viewMode,
                    tileImageList ?? _largeImageList,
                    tileImageList ?? _smallImageList,
                    GetImportPendingPredicate(),
                    GetAddPendingPredicate());

                if (IsMosaicViewMode(viewMode))
                {
                    if (!string.IsNullOrEmpty(priorMosaicKey)
                        && !string.Equals(priorMosaicKey, GameDisplayService.GetMosaicImageKey(game), StringComparison.Ordinal))
                    {
                        RemoveMosaicImageKey(priorMosaicKey);
                    }

                    if (reloadMosaic)
                        _ = UpsertMosaicTileForGameAsync(game, viewMode).ForgetFaults(Program.LogService, nameof(UpsertMosaicTileForGameAsync));
                }

                lstGames.Invalidate();
                return;
            }

            RefreshGamesCore(reloadTiles: true);
        }

        private void RefreshGamesCore(bool reloadTiles)
        {
            var viewMode = _appDataService.GetViewMode();
            int detailsTopIndex = TryGetDetailsViewTopItemIndex(viewMode);

            var tileImageList = GetTileImageListForViewMode(viewMode);
            bool applySort = !_pendingAddGameListService.HasDraft;

            lstGames.BeginUpdate();
            try
            {
                _gameDisplayService.RefreshListView(
                    lstGames,
                    _gameDataService,
                    _appDataService,
                    tileImageList ?? _largeImageList,
                    tileImageList ?? _smallImageList,
                    GetImportPendingPredicate(),
                    GetAddPendingPredicate(),
                    GetGamesForListDisplay(),
                    applySort);
            }
            finally
            {
                lstGames.EndUpdate();
            }

            TryRestoreDetailsViewTopItem(viewMode, detailsTopIndex);

            if (reloadTiles && IsMosaicViewMode(viewMode))
                StartLoadTileImages(viewMode);
        }

        // TopItem is only supported in Details view (throws in LargeIcon, SmallIcon, and Tile).
        private int TryGetDetailsViewTopItemIndex(string viewMode)
        {
            if (viewMode != ApplicationConstants.ViewModeDetails || lstGames.Items.Count == 0)
                return -1;

            try
            {
                var topItem = lstGames.TopItem;
                return topItem?.Index ?? -1;
            }
            catch (InvalidOperationException)
            {
                return -1;
            }
        }

        private void TryRestoreDetailsViewTopItem(string viewMode, int topIndex)
        {
            if (viewMode != ApplicationConstants.ViewModeDetails || topIndex < 0 || topIndex >= lstGames.Items.Count)
                return;

            try
            {
                lstGames.TopItem = lstGames.Items[topIndex];
            }
            catch (ArgumentException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private static bool IsMosaicViewMode(string viewMode)
        {
            return viewMode == ApplicationConstants.ViewModeTile
                || viewMode == ApplicationConstants.ViewModeCompactTiles
                || viewMode == ApplicationConstants.ViewModeLogos;
        }

        private void ApplyViewModeMenuTexts()
        {
            miMnuBarViewTile.Text = ApplicationConstants.ViewModeTile;
            miMnuBarViewCompactTiles.Text = ApplicationConstants.ViewModeCompactTiles;
            miMnuBarViewLogos.Text = ApplicationConstants.ViewModeLogos;
            miMnuBarViewIcons.Text = ApplicationConstants.ViewModeIcons;
            miMnuBarViewDetails.Text = ApplicationConstants.ViewModeDetails;
            miCtxViewTile.Text = ApplicationConstants.ViewModeTile;
            miCtxViewCompactTiles.Text = ApplicationConstants.ViewModeCompactTiles;
            miCtxViewLogos.Text = ApplicationConstants.ViewModeLogos;
            miCtxViewIcons.Text = ApplicationConstants.ViewModeIcons;
            miCtxViewDetails.Text = ApplicationConstants.ViewModeDetails;
        }

        private void UpdateViewMenuCheckMarks()
        {
            MenuCheckMarkHelper.UpdateViewMenuCheckMarks(
                _appDataService,
                miMnuBarViewTile,
                miMnuBarViewCompactTiles,
                miMnuBarViewLogos,
                miMnuBarViewIcons,
                miMnuBarViewDetails,
                miCtxViewTile,
                miCtxViewCompactTiles,
                miCtxViewLogos,
                miCtxViewIcons,
                miCtxViewDetails);
        }

        private void UpdateSortMenuCheckMarks()
        {
            MenuCheckMarkHelper.UpdateSortMenuCheckMarks(
                _appDataService,
                miCtxViewSortNameAsc,
                miCtxViewSortNameDesc,
                miCtxViewSortAppIdAsc,
                miCtxViewSortAppIdDesc,
                miCtxViewSortNone,
                miMnuBarSortNameAsc,
                miMnuBarSortNameDesc,
                miMnuBarSortAppIdAsc,
                miMnuBarSortAppIdDesc,
                miMnuBarSortNone);
        }

        private void UpdateThemeMenuCheckMarks()
        {
            MenuCheckMarkHelper.UpdateThemeMenuCheckMarks(
                _appDataService,
                miThemeLight,
                miThemeDark,
                miThemeSystem);
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            try
            {
                RestoreWindowState();

                EmulatorUpdateService.GetAndClearLastStartupUpdateCheckError();

                _legacyImportService = new LegacyImportService();
                Action refreshLibraryOnUiThread = RefreshLibraryFromImport;
                await _legacyImportService.RunAsyncStartupMigrationAsync(_taskReportService, refreshLibraryOnUiThread).ConfigureAwait(true);
                UpdateApiKeyStatusIndicator();
                SteamInstallationPathHelper.TryRefreshSteamDllInGoldbergFolder();

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var result = await _appDataService.EnsureGlobalConfigFilesExistAsync().ConfigureAwait(false);
                        if (!result.IsValid)
                            Program.LogService?.LogWarning($"Deferred config setup: {result.ErrorMessage}");
                    }
                    catch (Exception ex)
                    {
                        Program.LogService?.LogError($"Deferred config setup failed: {ex.Message}", ex);
                    }
                }).ForgetFaults(Program.LogService, "DeferredEnsureGlobalConfigFiles");

                if (_appDataService.IsFirstRun())
                {
                    HandleFirstRun();
                }

                if (PendingAppIdLaunch.HasValue)
                {
                    ulong appId = PendingAppIdLaunch.Value;
                    PendingAppIdLaunch = null;
                    if (!IsDisposed && !Disposing)
                    {
                        BeginInvoke(new Action(() =>
                        {
                            if (IsDisposed || Disposing)
                                return;
                            LaunchGameByAppId(appId);
                        }));
                    }
                }
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Failed to load form state: {ex.Message}");
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                FlushPersistDetailsColumnWidths();
                SaveWindowState();
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Failed to save window state: {ex.Message}");
            }
        }

        private void RestoreWindowState()
        {
            WindowStateHelper.RestoreWindowState(this, _appDataService);
        }

        private void SaveWindowState()
        {
            WindowStateHelper.SaveWindowState(this, _appDataService);
        }

        private void HandleFirstRun()
        {
            try
            {
                if (!_apiKeyService.HasApiKey())
                {
                    var result = FormMessageBoxHelper.ShowDialogIfAlive(this,
                        "Enhance your SmartGoldbergEmu experience with a Steam Web API key!\n\n" +
                        "With an API key, you can:\n" +
                        "• Automatically generate achievements from Steam\n" +
                        "• Automatically generate inventory items from Steam\n\n" +
                        "The API key is free and only requires a Steam account.\n\n" +
                        "Would you like to configure an API key now?",
                        "Steam Web API Key — Optional Feature",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);

                    if (result == DialogResult.Yes)
                        OpenSettingsDialog(0);
                }

                _appDataService.CompleteFirstRun();
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Failed to handle first run: {ex.Message}");
                _appDataService.CompleteFirstRun();
            }
        }
    }
}
