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
using SmartGoldbergEmu.Services;
using SmartGoldbergEmu.StubKit;
using SmartGoldbergEmu.Validation;
using SteamKit;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Timer = System.Windows.Forms.Timer;

namespace SmartGoldbergEmu.Forms
{
    public partial class MainForm : ThemedForm
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
        private readonly ImageListOwnedImages _tileOwnedImages = new ImageListOwnedImages();
        private readonly ImageListOwnedImages _compactTileOwnedImages = new ImageListOwnedImages();
        private readonly ImageListOwnedImages _logoOwnedImages = new ImageListOwnedImages();
        private ApiKeyStatusIndicatorHelper _apiKeyStatusIndicatorHelper;
        private UriFileWatcherHelper _uriFileWatcherHelper;
        private string _persistedDetailsColumnWidths;
        private Timer _detailsColumnWidthsSaveTimer;
        private const int DetailsColumnWidthsSaveDebounceMs = 250;
        private Timer _gameListRefreshTimer;
        private const int GameListRefreshDebounceMs = 80;
        private bool _gameListRefreshFullTiles;
        // Dispose Steam/theme/image singletons before allowing Close so the process does not linger after the UI is gone.
        private bool _closeDisposeStarted;
        private bool _closeAfterDisposeReady;
        private CancellationTokenSource _formLifetimeCts = new CancellationTokenSource();
        private int _tileImageLoadGeneration;
        private string _pendingAddMosaicImageKey;
        // After games.ini commit, draft is cleared but assets may still be downloading — keep waiting art on these rows.
        private readonly HashSet<Guid> _addSaveWaitingForAssetsGuids = new HashSet<Guid>();
        private readonly HashSet<string> _waitingMosaicAnimatedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Timer _waitingMosaicAnimTimer;
        private int _waitingMosaicAnimFrameIndex;
        private string _waitingMosaicAnimViewMode;
        private Bitmap[] _waitingMosaicDisplayFrames;
        private int[] _waitingMosaicDisplayFrameDelays;
        private string _waitingMosaicDisplayFramesViewMode;
        private bool _waitingMosaicDisplayFramesDropShadow;
        private int _waitingMosaicAnimGeneration;
        private int _stubKitDropDownLoadId;
        private bool _stubKitDropDownReopening;

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
            : base(themeService)
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

            Text = ApplicationVersionHelper.GetWindowTitle();
            ApplyViewModeMenuTexts();

            ServiceLocator.SetTaskReportService(_taskReportService);

            InitializeImageLists();
            InitializeApiKeyStatusIndicator();
            InitializeGameDisplay();
            SetupContextMenus();
            InitializeTheme();
            SetupListViewOwnerDraw();

            _uriFileWatcherHelper = new UriFileWatcherHelper(this, LaunchGameByAppId);
            _uriFileWatcherHelper.Setup();
        }

        private void InitializeApiKeyStatusIndicator()
        {
            _apiKeyStatusIndicatorHelper?.Dispose();

            _apiKeyStatusIndicatorHelper = new ApiKeyStatusIndicatorHelper(lblApiKeyStatus, _apiKeyService);
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
                Program.LogService?.LogError("Failed to initialize theme", ex);
            }
        }

        protected override void OnThemeApplied()
        {
            UpdateThemeIcon();
            UpdateThemeMenuCheckMarks();
            // Skip mosaic reload during the first handle creation, before games are loaded.
            if (Visible)
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
            CancelFormLifetime();

            _taskReportService.Clear();

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

            StopWaitingMosaicAnimation(disposeDisplayFrames: true);

            // ImageList.Dispose owns Depth32Bit originals; drop side-map refs first to avoid double-Dispose.
            _gameDisplayService.ReleaseIconImageOwnership();
            _tileOwnedImages.ReleaseOwnership();
            _compactTileOwnedImages.ReleaseOwnership();
            _logoOwnedImages.ReleaseOwnership();

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

            _tileOwnedImages.Dispose();
            _compactTileOwnedImages.Dispose();
            _logoOwnedImages.Dispose();

            _apiKeyStatusIndicatorHelper?.Dispose();
            _uriFileWatcherHelper?.Dispose();

            ClearStubKitDropDownItems();

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
            _tileImageList = CreateImageList(MosaicViewHelper.TileViewImageSize);
            _compactTileImageList = CreateImageList(MosaicViewHelper.CompactTilesViewImageSize);
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
                Program.LogService?.LogError("Failed to initialize game display", ex);
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

        private ImageListOwnedImages GetTileOwnedImagesForViewMode(string viewMode)
        {
            if (viewMode == ApplicationConstants.ViewModeTile)
                return _tileOwnedImages;
            if (viewMode == ApplicationConstants.ViewModeCompactTiles)
                return _compactTileOwnedImages;
            if (viewMode == ApplicationConstants.ViewModeLogos)
                return _logoOwnedImages;
            return null;
        }

        private void ClearInactiveMosaicImageLists(string activeViewMode)
        {
            if (activeViewMode != ApplicationConstants.ViewModeTile)
                _tileOwnedImages.Clear(_tileImageList);
            if (activeViewMode != ApplicationConstants.ViewModeCompactTiles)
                _compactTileOwnedImages.Clear(_compactTileImageList);
            if (activeViewMode != ApplicationConstants.ViewModeLogos)
                _logoOwnedImages.Clear(_logoImageList);
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
                    GetAddPendingPredicate(),
                    GetUpdatePendingPredicate());
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError("Failed to load games", ex);
            }
        }

        private void StartLoadTileImages(string viewMode)
        {
            int generation = ++_tileImageLoadGeneration;
            _ = LoadTileImagesAsync(viewMode, generation, FormLifetimeToken)
                .ForgetFaults(Program.LogService, nameof(LoadTileImagesAsync));
        }

        private CancellationToken FormLifetimeToken
        {
            get
            {
                CancellationTokenSource cts = _formLifetimeCts;
                if (cts == null)
                    return new CancellationToken(canceled: true);
                return cts.Token;
            }
        }

        private void CancelFormLifetime()
        {
            CancellationTokenSource cts = Interlocked.Exchange(ref _formLifetimeCts, null);
            if (cts == null)
                return;
            try
            {
                cts.Cancel();
            }
            catch
            {
            }
            try
            {
                cts.Dispose();
            }
            catch
            {
            }
        }

        private async Task LoadTileImagesAsync(string viewMode, int generation, CancellationToken cancellationToken)
        {
            try
            {
                if (IsDisposed || Disposing || cancellationToken.IsCancellationRequested)
                    return;

                var gameImageService = ServiceLocator.GameImageService;
                var imageNormalizationService = ServiceLocator.ImageNormalizationService;
                var targetImageList = GetTileImageListForViewMode(viewMode);
                if (targetImageList == null)
                    return;

                var ownedImages = GetTileOwnedImagesForViewMode(viewMode);
                if (ownedImages == null)
                    return;

                ownedImages.Clear(targetImageList);
                StopWaitingMosaicTimer();
                _waitingMosaicAnimatedKeys.Clear();

                var effectiveTheme = _themeService.EffectiveTheme;
                _themeService.GetFallbackMosaicArtColors(effectiveTheme, out var mosaicBackground, out var mosaicForeground);
                await gameImageService.EnsureMosaicFallbackForViewAsync(viewMode, effectiveTheme, mosaicBackground, mosaicForeground).ConfigureAwait(true);

                if (IsDisposed || Disposing || cancellationToken.IsCancellationRequested || generation != _tileImageLoadGeneration)
                    return;

                var addedAppIds = new HashSet<string>();
                bool logosDropShadow = viewMode == ApplicationConstants.ViewModeLogos
                    && _appDataService.GetLogosViewDropShadow();
                bool waitingDropShadow = ShouldApplyWaitingMosaicDropShadow();

                foreach (ListViewItem item in lstGames.Items)
                {
                    if (IsDisposed || Disposing || cancellationToken.IsCancellationRequested || generation != _tileImageLoadGeneration)
                        return;

                    var game = item.Tag as GameConfig;
                    if (game == null)
                        continue;

                    string imageKey = GameDisplayService.GetMosaicImageKey(
                        game,
                        GetAddPendingPredicate(),
                        GetUpdatePendingPredicate());

                    if (addedAppIds.Contains(imageKey))
                        continue;

                    Bitmap imageCopy = await LoadMosaicDisplayBitmapForGameAsync(
                        game,
                        viewMode,
                        gameImageService,
                        imageNormalizationService,
                        logosDropShadow,
                        waitingDropShadow).ConfigureAwait(true);

                    if (imageCopy == null)
                        continue;

                    if (cancellationToken.IsCancellationRequested || generation != _tileImageLoadGeneration)
                    {
                        imageCopy.Dispose();
                        continue;
                    }

                    ownedImages.Set(targetImageList, imageKey, imageCopy);
                    addedAppIds.Add(imageKey);
                    if (ShouldUseWaitingMosaicPlaceholder(game))
                        RegisterWaitingMosaicAnimation(imageKey, viewMode);
                }

                if (IsDisposed || Disposing || cancellationToken.IsCancellationRequested || generation != _tileImageLoadGeneration)
                    return;

                lstGames.Invalidate();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError("Failed to load tile images", ex);
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

                var ownedImages = GetTileOwnedImagesForViewMode(viewMode);
                if (ownedImages == null)
                    return;

                string imageKey = GameDisplayService.GetMosaicImageKey(
                    game,
                    GetAddPendingPredicate(),
                    GetUpdatePendingPredicate());
                if (string.IsNullOrEmpty(imageKey))
                    return;

                var effectiveTheme = _themeService.EffectiveTheme;
                _themeService.GetFallbackMosaicArtColors(effectiveTheme, out var mosaicBackground, out var mosaicForeground);
                await gameImageService.EnsureMosaicFallbackForViewAsync(viewMode, effectiveTheme, mosaicBackground, mosaicForeground).ConfigureAwait(true);

                if (IsDisposed || Disposing)
                    return;

                bool logosDropShadow = viewMode == ApplicationConstants.ViewModeLogos
                    && _appDataService.GetLogosViewDropShadow();
                bool waitingDropShadow = ShouldApplyWaitingMosaicDropShadow();

                Bitmap imageCopy = await LoadMosaicDisplayBitmapForGameAsync(
                    game,
                    viewMode,
                    gameImageService,
                    imageNormalizationService,
                    logosDropShadow,
                    waitingDropShadow).ConfigureAwait(true);

                if (imageCopy == null)
                    return;

                if (IsDisposed || Disposing)
                {
                    imageCopy.Dispose();
                    return;
                }

                ownedImages.Set(targetImageList, imageKey, imageCopy);

                var item = GameDisplayService.FindListItemByGameGuid(lstGames, game.GameGuid);
                if (item != null)
                    item.ImageKey = imageKey;

                if (ShouldUseWaitingMosaicPlaceholder(game))
                    RegisterWaitingMosaicAnimation(imageKey, viewMode);
                else
                    UnregisterWaitingMosaicAnimation(imageKey);

                lstGames.Invalidate();
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Failed to update tile for {game?.AppName} (AppId {game?.AppId})", ex);
            }
        }

        private async Task<Bitmap> LoadMosaicDisplayBitmapForGameAsync(
            GameConfig game,
            string viewMode,
            GameImageService gameImageService,
            ImageNormalizationService imageNormalizationService,
            bool logosDropShadow,
            bool waitingDropShadow)
        {
            if (ShouldUseWaitingMosaicPlaceholder(game))
            {
                Bitmap waitingDisplay = TryCreateWaitingMosaicDisplayBitmap(
                    viewMode,
                    gameImageService,
                    imageNormalizationService,
                    logosDropShadow,
                    waitingDropShadow);
                if (waitingDisplay != null)
                    return waitingDisplay;
            }

            string imagePath = gameImageService.ResolveArtworkPathForViewMode(game.AppId, viewMode);

            if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
            {
                try
                {
                    using (var image = Image.FromFile(imagePath))
                    {
                        return CreateMosaicDisplayBitmapFromSource(
                            image,
                            viewMode,
                            imageNormalizationService,
                            logosDropShadow);
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

                return CreateMosaicDisplayBitmapFromSource(
                    rawFallback,
                    viewMode,
                    imageNormalizationService,
                    logosDropShadow);
            }
        }

        private bool ShouldUseWaitingMosaicPlaceholder(GameConfig game)
        {
            if (game == null || game.GameGuid == Guid.Empty)
                return false;

            if (_addSaveWaitingForAssetsGuids.Contains(game.GameGuid))
                return true;

            // New add drafts only (not update drafts that already show library art under the AppId key).
            return _pendingAddGameListService.IsPendingGame(game)
                && !_pendingAddGameListService.IsPendingUpdate(game);
        }

        private static Bitmap TryCreateWaitingMosaicDisplayBitmap(
            string viewMode,
            GameImageService gameImageService,
            ImageNormalizationService imageNormalizationService,
            bool logosDropShadow,
            bool waitingDropShadow)
        {
            // Prefer Steam clientui hashed spinner; if missing, use FallbackTileArt.csv mosaic art.
            using (var rawWaiting = gameImageService.TryCloneWaitingMosaicPlaceholderBitmap())
            {
                if (rawWaiting != null)
                    return CreateMosaicDisplayBitmapFromSource(
                        rawWaiting,
                        viewMode,
                        imageNormalizationService,
                        logosDropShadow,
                        waitingPlaceholder: true,
                        waitingDropShadow);
            }

            using (var rawCsvFallback = gameImageService.TryCloneMosaicFallbackBitmap())
            {
                if (rawCsvFallback == null)
                    return null;
                return CreateMosaicDisplayBitmapFromSource(
                    rawCsvFallback,
                    viewMode,
                    imageNormalizationService,
                    logosDropShadow,
                    waitingPlaceholder: true,
                    waitingDropShadow);
            }
        }

        private static Bitmap CreateMosaicDisplayBitmapFromSource(
            Image source,
            string viewMode,
            ImageNormalizationService imageNormalizationService,
            bool logosDropShadow,
            bool waitingPlaceholder = false,
            bool waitingDropShadow = false)
        {
            if (source == null || imageNormalizationService == null)
                return null;

            return imageNormalizationService.CreateMosaicDisplayBitmap(
                source,
                viewMode,
                logosDropShadow,
                waitingPlaceholder,
                waitingDropShadow);
        }

        // Seeds the AppId mosaic key with waiting art before the list drops the pending-* key (avoids a blank tile on Save).
        private bool TrySeedMosaicKeyWithWaitingPlaceholder(string imageKey, string viewMode)
        {
            if (string.IsNullOrEmpty(imageKey) || !IsMosaicViewMode(viewMode))
                return false;

            var targetImageList = GetTileImageListForViewMode(viewMode);
            var ownedImages = GetTileOwnedImagesForViewMode(viewMode);
            if (targetImageList == null || ownedImages == null)
                return false;

            var gameImageService = ServiceLocator.GameImageService;
            var imageNormalizationService = ServiceLocator.ImageNormalizationService;
            bool logosDropShadow = viewMode == ApplicationConstants.ViewModeLogos
                && _appDataService.GetLogosViewDropShadow();
            bool waitingDropShadow = ShouldApplyWaitingMosaicDropShadow();

            Bitmap waitingDisplay = TryCreateWaitingMosaicDisplayBitmap(
                viewMode,
                gameImageService,
                imageNormalizationService,
                logosDropShadow,
                waitingDropShadow);
            if (waitingDisplay == null)
                return false;

            ownedImages.Set(targetImageList, imageKey, waitingDisplay);
            RegisterWaitingMosaicAnimation(imageKey, viewMode);
            return true;
        }

        private void RegisterWaitingMosaicAnimation(string imageKey, string viewMode)
        {
            if (string.IsNullOrEmpty(imageKey) || !IsMosaicViewMode(viewMode))
                return;

            _waitingMosaicAnimatedKeys.Add(imageKey);
            _waitingMosaicAnimViewMode = viewMode;
            // Kick decode immediately so the spinner starts soon after the first static frame.
            _ = EnsureWaitingMosaicAnimationRunningAsync()
                .ForgetFaults(Program.LogService, nameof(EnsureWaitingMosaicAnimationRunningAsync));
        }

        private void UnregisterWaitingMosaicAnimation(string imageKey)
        {
            if (string.IsNullOrEmpty(imageKey))
                return;

            _waitingMosaicAnimatedKeys.Remove(imageKey);
            if (_waitingMosaicAnimatedKeys.Count == 0)
                StopWaitingMosaicAnimation(disposeDisplayFrames: true);
        }

        private async Task EnsureWaitingMosaicAnimationRunningAsync()
        {
            if (IsDisposed || Disposing || _waitingMosaicAnimatedKeys.Count == 0)
                return;

            int generation = _waitingMosaicAnimGeneration;
            string viewMode = _waitingMosaicAnimViewMode ?? _appDataService.GetViewMode();
            if (!HasWaitingMosaicDisplayFramesForView(viewMode))
            {
                bool ready = await ServiceLocator.GameImageService.EnsureWaitingMosaicAnimationAsync().ConfigureAwait(true);
                if (generation != _waitingMosaicAnimGeneration
                    || IsDisposed
                    || Disposing
                    || !ready
                    || _waitingMosaicAnimatedKeys.Count == 0)
                {
                    return;
                }

                viewMode = _waitingMosaicAnimViewMode ?? _appDataService.GetViewMode();
                if (!TryBuildWaitingMosaicDisplayFrames(viewMode))
                    return;
            }

            if (generation != _waitingMosaicAnimGeneration || IsDisposed || Disposing)
                return;

            StartWaitingMosaicTimerAndApply();
        }

        private void StartWaitingMosaicTimerAndApply()
        {
            if (_waitingMosaicDisplayFrames == null || _waitingMosaicDisplayFrames.Length < 2)
                return;

            if (_waitingMosaicAnimTimer == null)
            {
                _waitingMosaicAnimTimer = new Timer();
                _waitingMosaicAnimTimer.Tick += WaitingMosaicAnimTimer_Tick;
            }

            if (_waitingMosaicAnimFrameIndex < 0
                || _waitingMosaicAnimFrameIndex >= _waitingMosaicDisplayFrames.Length)
            {
                _waitingMosaicAnimFrameIndex = 0;
            }

            int interval = GetWaitingMosaicFrameDelayMs(_waitingMosaicAnimFrameIndex);
            if (interval < 15)
                interval = 15;
            _waitingMosaicAnimTimer.Interval = interval;
            ApplyWaitingMosaicAnimationFrame();
            if (!_waitingMosaicAnimTimer.Enabled)
                _waitingMosaicAnimTimer.Start();
        }

        private void WaitingMosaicAnimTimer_Tick(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing || _waitingMosaicAnimatedKeys.Count == 0)
            {
                StopWaitingMosaicAnimation(disposeDisplayFrames: false);
                return;
            }

            if (_waitingMosaicDisplayFrames == null || _waitingMosaicDisplayFrames.Length < 2)
            {
                StopWaitingMosaicAnimation(disposeDisplayFrames: true);
                return;
            }

            _waitingMosaicAnimFrameIndex++;
            if (_waitingMosaicAnimFrameIndex >= _waitingMosaicDisplayFrames.Length)
                _waitingMosaicAnimFrameIndex = 0;

            ApplyWaitingMosaicAnimationFrame();

            int interval = GetWaitingMosaicFrameDelayMs(_waitingMosaicAnimFrameIndex);
            if (interval < 15)
                interval = 15;
            if (_waitingMosaicAnimTimer != null && _waitingMosaicAnimTimer.Interval != interval)
                _waitingMosaicAnimTimer.Interval = interval;
        }

        private void ApplyWaitingMosaicAnimationFrame()
        {
            if (_waitingMosaicDisplayFrames == null
                || _waitingMosaicDisplayFrames.Length == 0
                || _waitingMosaicAnimFrameIndex < 0
                || _waitingMosaicAnimFrameIndex >= _waitingMosaicDisplayFrames.Length)
            {
                return;
            }

            string viewMode = _waitingMosaicAnimViewMode ?? _appDataService.GetViewMode();
            var targetImageList = GetTileImageListForViewMode(viewMode);
            var ownedImages = GetTileOwnedImagesForViewMode(viewMode);
            if (targetImageList == null || ownedImages == null || !IsMosaicViewMode(viewMode))
                return;

            Image frameSource = _waitingMosaicDisplayFrames[_waitingMosaicAnimFrameIndex];
            if (frameSource == null)
                return;

            try
            {
                // Copy keys — Set may re-enter UI and mutate the waiting set.
                string[] keys = new string[_waitingMosaicAnimatedKeys.Count];
                _waitingMosaicAnimatedKeys.CopyTo(keys);

                foreach (string key in keys)
                {
                    if (string.IsNullOrEmpty(key))
                        continue;

                    // ImageList copies pixels from a clone it can own; display frames stay alive for the next tick.
                    ownedImages.Set(targetImageList, key, new Bitmap(frameSource));
                }

                // ListView caches ImageList slots; clear+restore ImageKey forces a visual update.
                foreach (ListViewItem item in lstGames.Items)
                {
                    string imageKey = item.ImageKey;
                    if (string.IsNullOrEmpty(imageKey) || !_waitingMosaicAnimatedKeys.Contains(imageKey))
                        continue;
                    item.ImageKey = string.Empty;
                    item.ImageKey = imageKey;
                }

                lstGames.Invalidate();
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError("Failed to apply waiting mosaic animation frame", ex);
            }
        }

        private bool TryBuildWaitingMosaicDisplayFrames(string viewMode)
        {
            if (!IsMosaicViewMode(viewMode))
                return false;

            bool logosDropShadow = viewMode == ApplicationConstants.ViewModeLogos
                && _appDataService.GetLogosViewDropShadow();
            bool waitingDropShadow = ShouldApplyWaitingMosaicDropShadow();
            bool cachedDropShadow = GetWaitingMosaicDisplayFramesCacheDropShadow(viewMode, logosDropShadow, waitingDropShadow);

            if (_waitingMosaicDisplayFrames != null
                && string.Equals(_waitingMosaicDisplayFramesViewMode, viewMode, StringComparison.Ordinal)
                && _waitingMosaicDisplayFramesDropShadow == cachedDropShadow)
            {
                return true;
            }

            if (!ServiceLocator.GameImageService.TryGetWaitingMosaicAnimationFrames(out var sourceFrames)
                || sourceFrames == null
                || sourceFrames.Length < 2)
            {
                return false;
            }

            DisposeWaitingMosaicDisplayFrames();

            var imageNormalizationService = ServiceLocator.ImageNormalizationService;
            var built = new Bitmap[sourceFrames.Length];
            var delays = new int[sourceFrames.Length];
            try
            {
                for (int i = 0; i < sourceFrames.Length; i++)
                {
                    built[i] = CreateMosaicDisplayBitmapFromSource(
                        sourceFrames[i].Bitmap,
                        viewMode,
                        imageNormalizationService,
                        logosDropShadow,
                        waitingPlaceholder: true,
                        waitingDropShadow);
                    delays[i] = sourceFrames[i].DelayMilliseconds;
                }
            }
            catch
            {
                for (int i = 0; i < built.Length; i++)
                    built[i]?.Dispose();
                return false;
            }

            _waitingMosaicDisplayFrames = built;
            _waitingMosaicDisplayFrameDelays = delays;
            _waitingMosaicDisplayFramesViewMode = viewMode;
            _waitingMosaicDisplayFramesDropShadow = cachedDropShadow;
            return true;
        }

        private bool ShouldApplyWaitingMosaicDropShadow()
        {
            return _themeService != null
                && _themeService.EffectiveTheme == ThemeMode.Light;
        }

        private static bool GetWaitingMosaicDisplayFramesCacheDropShadow(
            string viewMode,
            bool logosDropShadow,
            bool waitingDropShadow)
        {
            // Logos use the logos-shadow setting; store/library use light-mode waiting shadow.
            return viewMode == ApplicationConstants.ViewModeLogos
                ? logosDropShadow
                : waitingDropShadow;
        }

        private bool HasWaitingMosaicDisplayFramesForView(string viewMode)
        {
            if (_waitingMosaicDisplayFrames == null || _waitingMosaicDisplayFrames.Length < 2)
                return false;
            if (!string.Equals(_waitingMosaicDisplayFramesViewMode, viewMode, StringComparison.Ordinal))
                return false;

            bool logosDropShadow = viewMode == ApplicationConstants.ViewModeLogos
                && _appDataService.GetLogosViewDropShadow();
            bool waitingDropShadow = ShouldApplyWaitingMosaicDropShadow();
            bool cachedDropShadow = GetWaitingMosaicDisplayFramesCacheDropShadow(viewMode, logosDropShadow, waitingDropShadow);
            return _waitingMosaicDisplayFramesDropShadow == cachedDropShadow;
        }

        private int GetWaitingMosaicFrameDelayMs(int frameIndex)
        {
            if (_waitingMosaicDisplayFrameDelays == null || _waitingMosaicDisplayFrameDelays.Length == 0)
                return 33;

            if (frameIndex < 0 || frameIndex >= _waitingMosaicDisplayFrameDelays.Length)
                frameIndex = 0;
            int ms = _waitingMosaicDisplayFrameDelays[frameIndex];
            return ms < 10 ? 10 : ms;
        }

        private void StopWaitingMosaicTimer()
        {
            if (_waitingMosaicAnimTimer != null)
            {
                _waitingMosaicAnimTimer.Stop();
                _waitingMosaicAnimTimer.Tick -= WaitingMosaicAnimTimer_Tick;
                _waitingMosaicAnimTimer.Dispose();
                _waitingMosaicAnimTimer = null;
            }

            _waitingMosaicAnimFrameIndex = 0;
        }

        private void StopWaitingMosaicAnimation(bool disposeDisplayFrames)
        {
            StopWaitingMosaicTimer();
            if (disposeDisplayFrames || _waitingMosaicAnimatedKeys.Count == 0)
            {
                _waitingMosaicAnimGeneration++;
                _waitingMosaicAnimatedKeys.Clear();
                DisposeWaitingMosaicDisplayFrames();
                ServiceLocator.GameImageService.ReleaseWaitingMosaicAnimationFrames();
            }
        }

        private void DisposeWaitingMosaicDisplayFrames()
        {
            if (_waitingMosaicDisplayFrames == null)
            {
                _waitingMosaicDisplayFrameDelays = null;
                return;
            }

            for (int i = 0; i < _waitingMosaicDisplayFrames.Length; i++)
                _waitingMosaicDisplayFrames[i]?.Dispose();
            _waitingMosaicDisplayFrames = null;
            _waitingMosaicDisplayFrameDelays = null;
            _waitingMosaicDisplayFramesViewMode = null;
            _waitingMosaicDisplayFramesDropShadow = false;
        }

        private void RemoveMosaicImageKey(string imageKey)
        {
            if (string.IsNullOrEmpty(imageKey))
                return;

            UnregisterWaitingMosaicAnimation(imageKey);
            _tileOwnedImages.Remove(_tileImageList, imageKey);
            _compactTileOwnedImages.Remove(_compactTileImageList, imageKey);
            _logoOwnedImages.Remove(_logoImageList, imageKey);
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
            miCtxRowRefreshCatalog.Click += OnRefreshGameCatalogAndAssets_Click;
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
            miCtxRowOpenGameAssetsFolder.Click += OnOpenGameAssetsFolder_Click;

            miCtxRowCopyGuid.Click += OnCopyGuid_Click;
            miCtxRowCreateShortcut.Click += OnCreateShortcut_Click;
            miCtxRowCreateSteamAppIdFile.Click += OnCreateSteamAppIdFile_Click;
            miCtxRowRemoveSteamStub.DropDownOpening += OnRemoveSteamStub_DropDownOpening;
            ClearStubKitDropDownItems();
            AddStubKitAutoHandleMenuHeader();
            miCtxRowRemoveSteamStub.DropDownItems.Add(new ToolStripMenuItem("Loading…") { Enabled = false });

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
            ClearInactiveMosaicImageLists(viewMode);
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
                    Program.LogService?.LogDebug("Fork selection saved");
            }
        }

        private void OnCheckUpdates_Click(object sender, EventArgs e)
        {
            _ = OnCheckUpdatesAsync().ForgetFaults(Program.LogService, nameof(OnCheckUpdatesAsync));
        }

        private async Task OnCheckUpdatesAsync()
        {
            if (IsDisposed || Disposing || FormLifetimeToken.IsCancellationRequested)
                return;
            Program.LogService?.LogDebug("Manual Goldberg update check");
            _taskReportService.SetMessage("Checking for updates...");
            try
            {
                await EmulatorUpdateService.CheckForUpdatesWithUIAsync(
                    Program.LogService,
                    this,
                    isStartup: false,
                    onCheckStart: null,
                    onCheckComplete: null).ConfigureAwait(true);
                if (IsDisposed || Disposing || FormLifetimeToken.IsCancellationRequested)
                    return;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError("Goldberg update check UI flow failed", ex);
                _taskReportService.SetMessage(ErrorDisplayHelper.SanitizeForUser("Update check", ex), TaskReportKind.Error);
            }
            finally
            {
                if (!IsDisposed && !Disposing && !FormLifetimeToken.IsCancellationRequested)
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

            Program.LogService?.LogDebug("Manual Goldberg reinstall");
            _taskReportService.SetMessage("Preparing emulator reinstall...");

            try
            {
                await EmulatorUpdateService.ReinstallWithUIAsync(Program.LogService, this).ConfigureAwait(true);
                if (IsDisposed || Disposing)
                    return;
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError("Goldberg reinstall failed", ex);
                _taskReportService.SetMessage(ErrorDisplayHelper.SanitizeForUser("Reinstall", ex), TaskReportKind.Error);
            }
            finally
            {
                _taskReportService.SetMessage(string.Empty);
            }
        }

        private void OnViewEmulatorChangelog_Click(object sender, EventArgs e)
        {
            _ = OnViewEmulatorChangelogAsync().ForgetFaults(Program.LogService, nameof(OnViewEmulatorChangelogAsync));
        }

        private async Task OnViewEmulatorChangelogAsync()
        {
            if (IsDisposed || Disposing)
                return;

            Program.LogService?.LogDebug("View emulator changelog");
            _taskReportService.SetMessage("Loading changelog...");

            try
            {
                await EmulatorUpdateService.ShowLatestChangelogWithUIAsync(Program.LogService, this).ConfigureAwait(true);
                if (IsDisposed || Disposing)
                    return;
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError("Emulator changelog failed", ex);
                _taskReportService.SetMessage(ErrorDisplayHelper.SanitizeForUser("Emulator changelog", ex), TaskReportKind.Error);
            }
            finally
            {
                _taskReportService.SetMessage(string.Empty);
            }
        }

        private async void OnAddGame_Click(object sender, EventArgs e)
        {
            // Pre-warm Steam while the user picks an exe / AppId; drop the session when add finishes or is cancelled.
            using (ServiceLocator.SteamProductInfoService.HoldSession(preWarm: true))
            {
                try
                {
                    string executablePath = SelectGameExecutable();
                    if (string.IsNullOrEmpty(executablePath))
                        return;

                    await AddGameFromExecutable(executablePath);
                }
                catch (Exception ex)
                {
                    Program.LogService?.LogError("Error adding game", ex);
                    if (!IsDisposed && !Disposing)
                        _taskReportService.SetMessage(ErrorDisplayHelper.SanitizeForUser("Adding game", ex), TaskReportKind.Error);
                }
            }
        }

        private async Task AddGameFromExecutable(string executablePath)
        {
            try
            {
                if (IsDisposed || Disposing || string.IsNullOrWhiteSpace(executablePath))
                    return;

                _taskReportService.SetProgress(0, 0);

                ulong appId = ServiceLocator.GameAddCollector.ResolveAppIdForCollect(executablePath);
                if (appId == 0)
                {
                    _taskReportService.SetMessageWithAutoClear("Adding game cancelled.");
                    return;
                }

                if (IsDisposed || Disposing)
                    return;

                GameConfig duplicate = _gameDataService.FindDuplicateForAdd(executablePath, appId, out bool matchedByExecutable);
                if (duplicate != null)
                {
                    DuplicateGameAction action = DuplicateGameDialogHelper.Show(this, duplicate, matchedByExecutable);
                    if (action == DuplicateGameAction.Edit)
                    {
                        _taskReportService.SetMessageWithAutoClear("Opening the existing game for edit.");
                        EditGame(duplicate.GameGuid);
                        return;
                    }

                    _taskReportService.SetMessageWithAutoClear("Adding game cancelled.");
                    return;
                }

                _pendingAddGameListService.SetDraft(PendingAddGameListService.CreateDraftFromExecutable(executablePath));

                ShowPendingAddInList();

                // Stay on the UI sync context: pending-list helpers and GameSettings touch lstGames.
                GameAddCollectResult collectResult = await ServiceLocator.GameAddCollector
                    .CollectFromExecutableAsync(executablePath, this, _taskReportService, appId)
                    .ConfigureAwait(true);

                if (IsDisposed || Disposing)
                    return;
                if (collectResult.Cancelled)
                {
                    ClearPendingAddListEntry();
                    // Metadata fetch errors are already on the strip from GameSetupService.
                    if (!collectResult.MetadataFetchFailed)
                        _taskReportService.SetMessageWithAutoClear("Adding game cancelled.");
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
                {
                    _taskReportService.SetMessageWithAutoClear("Adding game cancelled.");
                    return;
                }

                if (IsDisposed || Disposing)
                    return;

                // Stub check after the game is in the library.
                if (_gameDataService.GetGame(gameConfig.GameGuid) != null)
                {
                    string stubExe = executablePath;
                    ulong stubAppId = gameConfig.AppId;
                    string stubName = gameConfig.AppName;
                    // Schedule after add returns so list/mosaic can paint; do not block save completion.
                    BeginInvoke(new MethodInvoker(() =>
                    {
                        if (IsDisposed || Disposing)
                            return;
                        _ = OfferSteamStubRemovalIfNeededAsync(stubExe, stubAppId, stubName)
                            .ForgetFaults(Program.LogService, nameof(OfferSteamStubRemovalIfNeededAsync));
                    }));
                }
            }
            catch (Exception ex)
            {
                ClearPendingAddListEntry();
                Program.LogService?.LogError("Error adding game", ex);
                _taskReportService.SetMessage(ErrorDisplayHelper.SanitizeForUser("Adding game", ex), TaskReportKind.Error);
            }
        }

        private async Task<bool> OpenGameSettingsFormAsync(GameConfig gameConfig, OnlineAppData metadata, GameAddBundle addBundle = null)
        {
            bool existedBeforeDialog = _gameDataService.GetGame(gameConfig.GameGuid) != null;
            PendingAddGameSave pendingAddSave = null;
            try
            {
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
            finally
            {
                if (pendingAddSave == null)
                {
                    addBundle?.ReleaseHeavyRuntimeData();
                    gameConfig?.ReleaseHeavyRuntimeData();
                }
                else if (addBundle != null)
                {
                    // Do not touch Game here: failed save may restore the draft; success already released it.
                    addBundle.AchievementsPreviewJson = null;
                    addBundle.ItemsJson = null;
                    addBundle.PrefetchedSchemas = null;
                    addBundle.Metadata = null;
                    addBundle.Catalog = null;
                }
            }
        }

        private async Task<bool> CompletePendingAddSaveAsync(PendingAddGameSave pending)
        {
            if (pending?.GameConfig == null)
                return false;

            GameConfig draftToRestore = pending.GameConfig;
            Guid savedGameGuid = draftToRestore.GameGuid;
            bool isUpdate = pending.IsUpdateOfExisting;
            _pendingAddGameListService.Clear();
            if (!isUpdate && savedGameGuid != Guid.Empty)
                _addSaveWaitingForAssetsGuids.Add(savedGameGuid);

            GameSettingsSnapshot snapshot = pending.SettingsSnapshot ?? new GameSettingsSnapshot { AppId = pending.GameConfig.AppId };

            var formSaveRequest = new GameSettingsSaveRequest
            {
                GameConfig = pending.GameConfig,
                IsEditMode = false,
                Metadata = pending.Metadata,
                CustomStatsRawJson = pending.CustomStatsRawJson,
                TaskReportService = _taskReportService,
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
                OnSuccessfulSaveCompleted = () => NotifyAddSaveListChanged(savedGameGuid, reloadMosaic: false),
                PrefetchedSchemas = pending.PrefetchedSchemas
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
                    CredentialsTouched = pending.CredentialsTouched,
                    IsUpdateOfExisting = isUpdate
                }).ConfigureAwait(true);

                if (!saveResult.IsSuccess)
                {
                    _addSaveWaitingForAssetsGuids.Remove(savedGameGuid);
                    if (_gameDataService.GetGame(draftToRestore.GameGuid) == null || isUpdate)
                    {
                        _pendingAddGameListService.SetDraft(draftToRestore, isUpdate: isUpdate);
                        ShowPendingAddInList();
                    }

                    if (saveResult.HasCustomStatsJsonError)
                    {
                        AppTaskDialogHelper.ShowOk(
                            this,
                            "Custom stats contain invalid JSON.\n" +
                            "Please fix the format before saving.",
                            MessageBoxIcon.Warning);
                    }
                    else if (!string.IsNullOrWhiteSpace(saveResult.ErrorMessage))
                    {
                        _taskReportService.SetMessage(saveResult.ErrorMessage, TaskReportKind.Error);
                    }
                    return false;
                }

                pending.Metadata = null;
                pending.PrefetchedSchemas = null;
                pending.CustomStatsRawJson = null;
                pending.AdditionalFilesSaveRequest = null;
                pending.SaveDlcAndPaths = null;
                pending.GameConfig = null;
                return true;
            }
            catch (Exception ex)
            {
                _addSaveWaitingForAssetsGuids.Remove(savedGameGuid);
                if (_gameDataService.GetGame(draftToRestore.GameGuid) == null || isUpdate)
                {
                    _pendingAddGameListService.SetDraft(draftToRestore, isUpdate: isUpdate);
                    ShowPendingAddInList();
                }

                Program.LogService?.LogError("Error saving game", ex);
                _taskReportService.SetMessage(ErrorDisplayHelper.SanitizeForUser("Saving game", ex), TaskReportKind.Error);
                return false;
            }
        }

        private void ClearPendingAddListEntry()
        {
            if (IsDisposed || Disposing)
                return;
            if (InvokeRequired)
            {
                try
                {
                    Invoke(new Action(ClearPendingAddListEntry));
                }
                catch (ObjectDisposedException)
                {
                }
                return;
            }

            if (!_pendingAddGameListService.HasDraft)
                return;

            Guid gameGuid = _pendingAddGameListService.GetDraft().GameGuid;
            string mosaicKey = _pendingAddMosaicImageKey;
            bool wasUpdate = _pendingAddGameListService.IsUpdateDraft;
            GameConfig draft = _pendingAddGameListService.GetDraft();
            _pendingAddGameListService.Clear();
            _pendingAddMosaicImageKey = null;
            _addSaveWaitingForAssetsGuids.Remove(gameGuid);
            draft?.ReleaseHeavyRuntimeData();

            if (wasUpdate && _gameDataService.GetGame(gameGuid) != null)
            {
                RestoreLibraryGameInList(gameGuid);
                // Update drafts share the AppId mosaic key with the library tile — do not remove it.
                if (!string.IsNullOrEmpty(mosaicKey)
                    && mosaicKey.StartsWith("pending-", StringComparison.OrdinalIgnoreCase))
                    RemoveMosaicImageKey(mosaicKey);
                return;
            }

            RemovePendingAddFromListView(gameGuid, mosaicKey);
        }

        private void RestoreLibraryGameInList(Guid gameGuid)
        {
            GameConfig game = _gameDataService.GetGame(gameGuid);
            if (game == null)
                return;

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
                    GetAddPendingPredicate(),
                    GetUpdatePendingPredicate());
                lstGames.Invalidate();
                return;
            }

            ScheduleRefreshGames(reloadTiles: true);
        }

        private void ShowPendingAddInList()
        {
            if (IsDisposed || Disposing)
                return;
            if (InvokeRequired)
            {
                try
                {
                    Invoke(new Action(ShowPendingAddInList));
                }
                catch (ObjectDisposedException)
                {
                }
                return;
            }

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
                GetAddPendingPredicate(),
                GetUpdatePendingPredicate());

            _pendingAddMosaicImageKey = GameDisplayService.GetMosaicImageKey(
                draft,
                GetAddPendingPredicate(),
                GetUpdatePendingPredicate());
            EnsurePendingAddItemVisible();

            if (IsMosaicViewMode(viewMode))
                _ = UpsertMosaicTileForGameAsync(draft, viewMode).ForgetFaults(Program.LogService, nameof(UpsertMosaicTileForGameAsync));
        }

        private void UpdatePendingAddInList()
        {
            if (IsDisposed || Disposing)
                return;
            if (InvokeRequired)
            {
                try
                {
                    Invoke(new Action(UpdatePendingAddInList));
                }
                catch (ObjectDisposedException)
                {
                }
                return;
            }

            var draft = _pendingAddGameListService.GetDraft();
            if (draft == null)
                return;

            string priorMosaicKey = _pendingAddMosaicImageKey;
            string newMosaicKey = GameDisplayService.GetMosaicImageKey(
                draft,
                GetAddPendingPredicate(),
                GetUpdatePendingPredicate());
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
                    GetAddPendingPredicate(),
                    GetUpdatePendingPredicate());
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
                if (!string.IsNullOrEmpty(priorMosaicKey)
                    && priorMosaicKey.StartsWith("pending-", StringComparison.OrdinalIgnoreCase))
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

        private Func<GameConfig, bool> GetUpdatePendingPredicate()
        {
            return _pendingAddGameListService.IsPendingUpdate;
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
                openFileDialog.Title = "Select Game Executable";
                FileDialogBrowseHelper.ApplyInitialDirectory(
                    openFileDialog,
                    FileDialogBrowseHelper.Purpose.GameExecutable);

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    FileDialogBrowseHelper.RememberFile(
                        FileDialogBrowseHelper.Purpose.GameExecutable,
                        openFileDialog.FileName);
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
            Program.LogService?.LogDebug("Manual launcher update check");
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
                Program.LogService?.LogError("Launcher update check UI flow failed", ex);
                _taskReportService.SetMessage(ErrorDisplayHelper.SanitizeForUser("Launcher update check", ex), TaskReportKind.Error);
            }
            finally
            {
                _taskReportService.SetMessage(string.Empty);
            }
        }

        private void OnReinstallLauncher_Click(object sender, EventArgs e)
        {
            _ = OnReinstallLauncherAsync().ForgetFaults(Program.LogService, nameof(OnReinstallLauncherAsync));
        }

        private async Task OnReinstallLauncherAsync()
        {
            if (IsDisposed || Disposing)
                return;

            Program.LogService?.LogDebug("Manual launcher reinstall");
            _taskReportService.SetMessage("Preparing launcher reinstall...");

            try
            {
                await LauncherUpdateService.ReinstallWithUIAsync(Program.LogService, this).ConfigureAwait(true);
                if (IsDisposed || Disposing)
                    return;
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError("Launcher reinstall failed", ex);
                _taskReportService.SetMessage(ErrorDisplayHelper.SanitizeForUser("Launcher reinstall", ex), TaskReportKind.Error);
            }
            finally
            {
                _taskReportService.SetMessage(string.Empty);
            }
        }

        private void OnViewLauncherChangelog_Click(object sender, EventArgs e)
        {
            _ = OnViewLauncherChangelogAsync().ForgetFaults(Program.LogService, nameof(OnViewLauncherChangelogAsync));
        }

        private async Task OnViewLauncherChangelogAsync()
        {
            if (IsDisposed || Disposing)
                return;

            Program.LogService?.LogDebug("View launcher changelog");
            _taskReportService.SetMessage("Loading changelog...");

            try
            {
                await LauncherUpdateService.ShowLatestChangelogWithUIAsync(Program.LogService, this).ConfigureAwait(true);
                if (IsDisposed || Disposing)
                    return;
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError("Launcher changelog failed", ex);
                _taskReportService.SetMessage(ErrorDisplayHelper.SanitizeForUser("Launcher changelog", ex), TaskReportKind.Error);
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
                _taskReportService.SetMessageWithAutoClear("Adding game cancelled.");
                return;
            }

            games = games.Where(g => !_pendingAddGameListService.IsPendingGame(g)).ToList();
            if (games.Count == 0)
                return;

            var (confirmed, deleteFiles) = RemoveGamesDialogHelper.Show(games, this);
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
                    Program.LogService?.LogError(
                        $"Failed to remove game {game.AppName} (AppId {game.AppId}): {removeResult.ErrorMessage}");
                }
            }

            if (removed > 0)
            {
                RefreshGames();
                _taskReportService.SetMessageWithAutoClear(
                    removed == 1 ? $"{games[0].AppName} removed." : $"{removed} games removed.");
            }

            if (removed < games.Count && !string.IsNullOrWhiteSpace(lastError))
            {
                string userMessage = "Failed to remove some games from library.";
                if (!lastError.Contains("\\") && !lastError.Contains("/"))
                    userMessage = $"Failed to remove game: {lastError}";
                FormMessageBoxHelper.ShowIfAlive(this, userMessage, "Remove Game", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void OnRefreshGameCatalogAndAssets_Click(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing)
                return;
            if (!TryGetSelectedGameWithAppId(out GameConfig selectedGame))
                return;

            Program.LogService?.LogDebug(
                $"Catalog and asset refresh: {selectedGame.AppName} (AppId {selectedGame.AppId})");

            using (ServiceLocator.SteamProductInfoService.HoldSession())
            {
                try
                {
                    var feedbackService = GetLocatorTaskReportOrNull();
                    await ServiceLocator.GoldbergArtifactService
                        .RefreshGameCatalogAndAssetsAsync(selectedGame, feedbackService)
                        .ConfigureAwait(true);

                    if (IsDisposed || Disposing)
                        return;

                    NotifyAddSaveListChanged(selectedGame.GameGuid, reloadMosaic: true);
                    Program.LogService?.LogMessage(
                        $"Catalog for AppId {selectedGame.AppId} retrieved.");
                }
                catch (Exception ex)
                {
                    Program.LogService?.LogError(
                        $"Catalog for AppId {selectedGame.AppId} failed: {selectedGame.AppName}",
                        ex);
                    FormMessageBoxHelper.ShowIfAlive(this, "Failed to refresh game data and assets. Please check the SmartGoldbergEmu log for details.", "Refresh Game Data", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async void OnGenerateAchievements_Click(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing)
                return;
            if (!TryGetSelectedGameWithAppId(out GameConfig selectedGame))
                return;

            Program.LogService?.LogDebug(
                $"Achievement generation: {selectedGame.AppName} (AppId {selectedGame.AppId})");

            try
            {
                var feedbackService = GetLocatorTaskReportOrNull();
                await ServiceLocator.GoldbergArtifactService
                    .GenerateAchievementsFromMenuAsync(selectedGame, feedbackService)
                    .ConfigureAwait(true);

                if (IsDisposed || Disposing)
                    return;
                Program.LogService?.LogMessage(
                    $"Emulator files for AppId {selectedGame.AppId} generated.");
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError(
                    $"Emulator files for AppId {selectedGame.AppId} failed (achievements): {selectedGame.AppName}",
                    ex);
                FormMessageBoxHelper.ShowIfAlive(this, "Failed to generate achievements. Please check the SmartGoldbergEmu log for details.", "Generate Achievements", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnGameProperties_Click(object sender, EventArgs e)
        {
            if (GetSelectedGame() is GameConfig game)
                EditGame(game.GameGuid);
        }

        private async void OnRemoveSteamStub_DropDownOpening(object sender, EventArgs e)
        {
            // Show() after async rebuild re-enters Opening; skip so we do not loop.
            if (_stubKitDropDownReopening || IsDisposed || Disposing)
                return;

            int loadId = ++_stubKitDropDownLoadId;
            // Capture before await — clearing/rebuilding DropDownItems after await parks the menu at (0,0).
            Point dropLocation = GetStubKitDropDownScreenLocation(miCtxRowRemoveSteamStub);

            ClearStubKitDropDownItems();
            AddStubKitAutoHandleMenuHeader();

            var game = GetSelectedGame();
            if (game == null)
            {
                AddStubKitPlaceholderMenuItem("No game selected");
                return;
            }

            if (!GameFolderPathHelper.TryResolveExecutableForStubRemoval(game, out _))
            {
                AddStubKitPlaceholderMenuItem("No executable found");
                return;
            }

            var loadingItem = new ToolStripMenuItem("Loading…") { Enabled = false };
            miCtxRowRemoveSteamStub.DropDownItems.Add(loadingItem);

            IReadOnlyList<StubExecutableTarget> targets;
            try
            {
                targets = await ServiceLocator.StubKitService.ResolveTargetsAsync(game).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                if (!IsStubKitDropDownLoadCurrent(loadId))
                    return;

                Program.LogService?.LogError("StubKit: failed to resolve launch executables.", ex);
                ClearStubKitDropDownItems();
                AddStubKitAutoHandleMenuHeader();
                AddStubKitPlaceholderMenuItem("Could not load executables");
                ReopenStubKitDropDownAt(dropLocation);
                return;
            }

            if (!IsStubKitDropDownLoadCurrent(loadId))
                return;

            ClearStubKitDropDownItems();
            AddStubKitAutoHandleMenuHeader();

            if (targets == null || targets.Count == 0)
            {
                AddStubKitPlaceholderMenuItem("No executable found");
                ReopenStubKitDropDownAt(dropLocation);
                return;
            }

            // Show each exe immediately as Loading…, then fill Patch/Restore/No stub as PE checks finish.
            var menuItems = new List<ToolStripMenuItem>();
            foreach (StubExecutableTarget target in targets)
            {
                if (target == null || string.IsNullOrWhiteSpace(target.FullPath))
                    continue;

                var item = new ToolStripMenuItem
                {
                    Tag = target,
                    Image = TryExtractStubMenuIcon(target.FullPath)
                };
                ApplyStubKitMenuItemPresentation(item, target);
                miCtxRowRemoveSteamStub.DropDownItems.Add(item);
                menuItems.Add(item);
            }

            if (miCtxRowRemoveSteamStub.DropDownItems.Count <= 2)
            {
                AddStubKitPlaceholderMenuItem("No executable found");
                ReopenStubKitDropDownAt(dropLocation);
                return;
            }

            ReopenStubKitDropDownAt(dropLocation);

            foreach (ToolStripMenuItem item in menuItems)
            {
                if (!IsStubKitDropDownLoadCurrent(loadId))
                    return;

                var target = item.Tag as StubExecutableTarget;
                if (target == null || !target.IsDetectionPending)
                    continue;

                try
                {
                    await ServiceLocator.StubKitService.DetectTargetAsync(target).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    if (!IsStubKitDropDownLoadCurrent(loadId))
                        return;

                    Program.LogService?.LogError("StubKit: failed to detect SteamStub on " + target.FullPath, ex);
                    target.IsDetectionPending = false;
                    target.CanRemove = false;
                    target.HasSteamStub = false;
                    target.StubName = "none";
                }

                if (!IsStubKitDropDownLoadCurrent(loadId))
                    return;

                ApplyStubKitMenuItemPresentation(item, target);
            }

            if (IsStubKitDropDownLoadCurrent(loadId))
                StubKitService.ReleaseTemporaryBuffers();
        }

        private void ApplyStubKitMenuItemPresentation(ToolStripMenuItem item, StubExecutableTarget target)
        {
            if (item == null || target == null)
                return;

            string text = string.IsNullOrWhiteSpace(target.DisplayName)
                ? Path.GetFileName(target.FullPath)
                : target.DisplayName.Trim();

            StubExecutableMenuAction action = target.MenuAction;
            switch (action)
            {
                case StubExecutableMenuAction.Loading:
                    text += " - [Loading…]";
                    break;
                case StubExecutableMenuAction.Patch:
                    text += " - [Patch]";
                    break;
                case StubExecutableMenuAction.Restore:
                    text += " - [Restore]";
                    break;
                default:
                    text += " - [No stub]";
                    break;
            }

            string pathHint = string.IsNullOrWhiteSpace(target.RelativeOrExeHint)
                ? target.FullPath
                : target.RelativeOrExeHint + Environment.NewLine + target.FullPath;

            string statusHint;
            switch (action)
            {
                case StubExecutableMenuAction.Loading:
                    statusHint = "Checking this executable for SteamStub…"
                        + Environment.NewLine + pathHint;
                    break;
                case StubExecutableMenuAction.Patch:
                    statusHint = string.IsNullOrWhiteSpace(target.StubName)
                        ? "Remove SteamStub from this executable." + Environment.NewLine + pathHint
                        : "Remove " + target.StubName.Trim() + "." + Environment.NewLine + pathHint;
                    break;
                case StubExecutableMenuAction.Restore:
                    statusHint = "Restore the original executable from the backup."
                        + Environment.NewLine + pathHint;
                    break;
                default:
                    statusHint = "No SteamStub found on this executable."
                        + Environment.NewLine + pathHint;
                    break;
            }

            bool actionable = action == StubExecutableMenuAction.Patch
                || action == StubExecutableMenuAction.Restore;

            item.Click -= OnRemoveSteamStubTarget_Click;
            item.Text = text;
            item.Enabled = actionable;
            item.ToolTipText = statusHint;
            if (actionable)
                item.Click += OnRemoveSteamStubTarget_Click;
        }

        private bool IsStubKitDropDownLoadCurrent(int loadId)
        {
            return loadId == _stubKitDropDownLoadId
                && !IsDisposed
                && !Disposing
                && ctxGamesItem != null
                && ctxGamesItem.Visible;
        }

        // DropDownLocation is protected on net48; submenu opens at the item's right edge.
        private static Point GetStubKitDropDownScreenLocation(ToolStripMenuItem item)
        {
            if (item == null)
                return Point.Empty;

            ToolStrip parent = item.GetCurrentParent();
            if (parent == null)
                return Point.Empty;

            Rectangle bounds = item.Bounds;
            return parent.PointToScreen(new Point(bounds.Right, bounds.Top));
        }

        private void ReopenStubKitDropDownAt(Point dropLocation)
        {
            if (miCtxRowRemoveSteamStub?.DropDown == null)
                return;
            if (ctxGamesItem == null || !ctxGamesItem.Visible)
                return;

            _stubKitDropDownReopening = true;
            try
            {
                if (dropLocation.IsEmpty)
                    miCtxRowRemoveSteamStub.ShowDropDown();
                else
                    miCtxRowRemoveSteamStub.DropDown.Show(dropLocation);
            }
            finally
            {
                _stubKitDropDownReopening = false;
            }
        }

        private void ClearStubKitDropDownItems()
        {
            if (miCtxRowRemoveSteamStub == null)
                return;

            ToolStripItemCollection items = miCtxRowRemoveSteamStub.DropDownItems;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                ToolStripItem item = items[i];
                items.RemoveAt(i);
                if (item.Image != null)
                {
                    Image image = item.Image;
                    item.Image = null;
                    image.Dispose();
                }

                item.Dispose();
            }
        }

        // Checked toggle first, then a separator, then per-exe Patch/Restore items.
        private void AddStubKitAutoHandleMenuHeader()
        {
            if (miCtxRowRemoveSteamStub == null)
                return;

            bool enabled = false;
            try
            {
                enabled = _appDataService.GetAutoHandleSteamStubs();
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning("StubKit: could not read Auto handle SteamStubs setting: " + ex.Message);
            }

            var autoItem = new ToolStripMenuItem("Auto handle SteamStubs - (Auto patch)")
            {
                Name = "miCtxStubAutoHandle",
                CheckOnClick = true,
                Checked = enabled,
                ToolTipText = "When checked, removable SteamStub is unpacked automatically when adding or launching a game (no confirm dialog)."
            };
            autoItem.Click += OnStubKitAutoHandle_Click;
            miCtxRowRemoveSteamStub.DropDownItems.Add(autoItem);
            miCtxRowRemoveSteamStub.DropDownItems.Add(new ToolStripSeparator());
        }

        private void OnStubKitAutoHandle_Click(object sender, EventArgs e)
        {
            var item = sender as ToolStripMenuItem;
            if (item == null)
                return;

            var result = _appDataService.SetAutoHandleSteamStubs(item.Checked);
            if (result.IsValid)
                return;

            item.Checked = !item.Checked;
            FormMessageBoxHelper.ShowIfAlive(
                this,
                result.ErrorMessage ?? "Could not save Auto handle SteamStubs.",
                StubKitFeedback.DialogTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        private void AddStubKitPlaceholderMenuItem(string text)
        {
            miCtxRowRemoveSteamStub.DropDownItems.Add(new ToolStripMenuItem(text) { Enabled = false });
        }

        private async void OnRemoveSteamStubTarget_Click(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing)
                return;

            var menuItem = sender as ToolStripMenuItem;
            var target = menuItem?.Tag as StubExecutableTarget;
            if (target == null || string.IsNullOrWhiteSpace(target.FullPath))
                return;

            if (target.MenuAction == StubExecutableMenuAction.Restore)
                await RestoreStubKitExecutableAsync(target.FullPath).ConfigureAwait(true);
            else if (target.MenuAction == StubExecutableMenuAction.Patch)
                await ApplyStubKitToExecutableAsync(target.FullPath).ConfigureAwait(true);
        }

        // Returns false when the user cancels (Cancel / window X); true to continue (Accept, Skip, or no prompt).
        private async Task<bool> OfferSteamStubRemovalIfNeededAsync(string executablePath, ulong appId, string gameName)
        {
            if (IsDisposed || Disposing)
                return true;
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
                return true;

            string extension = Path.GetExtension(executablePath);
            if (!string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase))
                return true;

            DetectResult detect;
            try
            {
                detect = await Task.Run(() => StubKitService.DetectExecutable(executablePath))
                    .ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError("StubKit: failed to check executable for SteamStub.", ex);
                return true;
            }

            if (IsDisposed || Disposing)
                return true;
            if (detect == null || !detect.CanRemove)
                return true;

            bool autoHandle = false;
            try
            {
                autoHandle = _appDataService != null && _appDataService.GetAutoHandleSteamStubs();
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning("StubKit: could not read Auto handle SteamStubs setting: " + ex.Message);
            }

            if (autoHandle)
            {
                Program.LogService?.LogDebug("StubKit: auto-handling SteamStub on " + executablePath);
                await ApplyStubKitToExecutableAsync(executablePath, gameName).ConfigureAwait(true);
                return true;
            }

            const int idAccept = 100;
            const int idSkip = 101;
            AppTaskDialogResult answer = AppTaskDialogForm.Show(
                this,
                new AppTaskDialogRequest
                {
                    Content = StubKitFeedback.OfferRemoveQuestion(appId, gameName, Path.GetFileName(executablePath)),
                    // Wider than default so long Game: lines fit before wrapping.
                    MaxClientWidth = 640,
                    Icon = MessageBoxIcon.Question,
                    VerificationText = "Auto handle SteamStubs - (Auto patch)",
                    VerificationChecked = false,
                    Buttons = new List<AppTaskDialogButton>
                    {
                        new AppTaskDialogButton(idAccept, "Accept") { IsDefault = true },
                        new AppTaskDialogButton(idSkip, "Skip"),
                        new AppTaskDialogButton(TaskDialogHelper.IdCancel, "Cancel") { IsCancel = true }
                    }
                });
            if (answer.ButtonId == TaskDialogHelper.IdCancel)
                return false;
            if (answer.ButtonId != idAccept)
                return true;

            if (answer.VerificationChecked)
            {
                var saveResult = _appDataService.SetAutoHandleSteamStubs(true);
                if (!saveResult.IsValid)
                {
                    Program.LogService?.LogWarning(
                        "StubKit: could not save Auto handle SteamStubs: " + (saveResult.ErrorMessage ?? "unknown error"));
                }
                else
                    Program.LogService?.LogDebug("StubKit: Auto handle SteamStubs enabled from confirm dialog");
            }

            await ApplyStubKitToExecutableAsync(executablePath, gameName).ConfigureAwait(true);
            return true;
        }

        private string TryResolveLaunchExecutableForStubCheck(GameConfig game, LaunchOption launchOption)
        {
            if (game == null)
                return null;

            try
            {
                ResolvedLaunchCommand command = _gameLaunchService.GetResolvedLaunchCommand(game, launchOption);
                if (command != null && !string.IsNullOrWhiteSpace(command.ExecutablePath) && File.Exists(command.ExecutablePath))
                    return command.ExecutablePath;
            }
            catch (Exception ex)
            {
                Program.LogService?.LogDebug("StubKit: could not resolve launch executable for SteamStub check: " + ex.Message);
            }

            if (GameFolderPathHelper.TryResolveExecutableForStubRemoval(game, out string settingsPath)
                && !string.IsNullOrWhiteSpace(settingsPath)
                && File.Exists(settingsPath))
            {
                return settingsPath;
            }

            return null;
        }

        private async Task ApplyStubKitToExecutableAsync(string executablePath, string gameName = null)
        {
            if (IsDisposed || Disposing)
                return;

            string name = !string.IsNullOrWhiteSpace(gameName)
                ? gameName.Trim()
                : GetSelectedGame()?.AppName;
            if (string.IsNullOrWhiteSpace(name))
                name = "game";

            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                ShowStubKitApplyFeedback(name, new StubKitApplyResult { Outcome = StubKitApplyOutcome.ExecutablePathInvalid });
                return;
            }

            Program.LogService?.LogDebug($"Removing SteamStub on {name}: {executablePath}");
            _taskReportService.SetMessage(StubKitFeedback.PatchingInProgress(name));

            try
            {
                var result = await ServiceLocator.StubKitService
                    .ApplyAsync(executablePath, Program.LogService)
                    .ConfigureAwait(true);
                if (IsDisposed || Disposing)
                    return;

                ShowStubKitApplyFeedback(name, result);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Removing SteamStub on {name} failed.", ex);
                ShowStubKitApplyFeedback(name, new StubKitApplyResult
                {
                    Outcome = StubKitApplyOutcome.Unexpected,
                    LogDetail = ex.Message
                });
            }
        }

        private async Task RestoreStubKitExecutableAsync(string executablePath)
        {
            if (IsDisposed || Disposing)
                return;

            string name = GetSelectedGame()?.AppName;
            if (string.IsNullOrWhiteSpace(name))
                name = "game";

            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                ShowStubKitApplyFeedback(name, new StubKitApplyResult { Outcome = StubKitApplyOutcome.ExecutablePathInvalid });
                return;
            }

            Program.LogService?.LogDebug($"Restoring SteamStub backup for {name}: {executablePath}");
            _taskReportService.SetMessage(StubKitFeedback.RestoringInProgress(name));

            try
            {
                var result = await ServiceLocator.StubKitService.RestoreAsync(executablePath, Program.LogService).ConfigureAwait(true);
                if (IsDisposed || Disposing)
                    return;

                ShowStubKitApplyFeedback(name, result);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Restoring SteamStub backup for {name} failed.", ex);
                ShowStubKitApplyFeedback(name, new StubKitApplyResult
                {
                    Outcome = StubKitApplyOutcome.Unexpected,
                    LogDetail = ex.Message
                });
            }
        }

        private void ShowStubKitApplyFeedback(string gameName, StubKitApplyResult result)
        {
            if (result == null)
                return;

            if (!string.IsNullOrWhiteSpace(result.LogDetail))
                Program.LogService?.LogMessage("StubKit detail: " + result.LogDetail);

            string message = StubKitFeedback.ResultMessage(result.Outcome, gameName);
            TaskReportKind kind = StubKitFeedback.KindForOutcome(result.Outcome);
            Program.LogService?.LogMessage(message);

            if (result.Outcome == StubKitApplyOutcome.Success ||
                result.Outcome == StubKitApplyOutcome.Restored)
            {
                _taskReportService.SetMessageWithAutoClear(message, kind);
                return;
            }

            _taskReportService.SetMessage(message, kind);
            FormMessageBoxHelper.ShowIfAlive(
                this,
                message,
                StubKitFeedback.DialogTitle,
                MessageBoxButtons.OK,
                StubKitFeedback.IconForOutcome(result.Outcome));
        }

        private static Image TryExtractStubMenuIcon(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
                return null;

            try
            {
                using (Icon icon = ServiceLocator.IconService.ExtractSmallIcon(executablePath))
                {
                    if (icon == null)
                        return null;
                    return icon.ToBitmap();
                }
            }
            catch
            {
                return null;
            }
        }

        private async void OnGenerateItems_Click(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing)
                return;
            if (!TryGetSelectedGameWithAppId(out GameConfig selectedGame))
                return;

            Program.LogService?.LogDebug(
                $"{PathConstants.GoldbergItemsJsonFileName} generation: {selectedGame.AppName} (AppId {selectedGame.AppId})");

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
                    if (!string.Equals(result.ErrorMessage, "No items found.", StringComparison.Ordinal)
                        && !string.Equals(result.ErrorMessage, "Skipped.", StringComparison.Ordinal))
                    {
                        Program.LogService?.LogWarning(
                            $"Emulator files for AppId {selectedGame.AppId} incomplete (items): {result.ErrorMessage}");
                    }
                    else
                    {
                        Program.LogService?.LogDebug(
                            $"Emulator files for AppId {selectedGame.AppId}: items skipped ({result.ErrorMessage})");
                    }

                    AppTaskDialogHelper.ShowOk(this, result.ErrorMessage, MessageBoxIcon.Information);
                    return;
                }

                Program.LogService?.LogMessage(
                    $"Emulator files for AppId {selectedGame.AppId} generated.");
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError(
                    $"Emulator files for AppId {selectedGame.AppId} failed (items): {selectedGame.AppName}",
                    ex);
                FormMessageBoxHelper.ShowIfAlive(this, $"Failed to generate {PathConstants.GoldbergItemsJsonFileName}. Please check the SmartGoldbergEmu log for details.", "Generate Items", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    FormMessageBoxHelper.ShowIfAlive(this, "Invalid Valve data file path detected.", "Valve Data File", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                Program.LogService?.LogError("Failed to open Valve data file", ex);
                FormMessageBoxHelper.ShowIfAlive(this, "Failed to open Valve data file.", "Valve Data File", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                FormMessageBoxHelper.ShowIfAlive(this, "Invalid URL format detected.", "Invalid URL", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                Process.Start(url);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Failed to open {pageName}", ex);
                FormMessageBoxHelper.ShowIfAlive(this, $"Failed to open {pageName}.", "Could Not Open Link", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                AppTaskDialogHelper.ShowOk(
                    this,
                    "Please select a game with a valid executable path.",
                    MessageBoxIcon.Information);
                return;
            }

            if (!GameFolderPathHelper.TryGetExistingExecutableDirectory(game, out string folderPath))
            {
                AppTaskDialogHelper.ShowOk(
                    this,
                    "The game folder was not found. The files may have been moved or uninstalled.",
                    MessageBoxIcon.Warning);
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
            ShellFolderHelper.OpenFolderForOwner(this, settingsPath, createIfMissing: true, "Folder Not Found", "Could Not Open Settings Folder");
        }

        private void OnOpenGameAssetsFolder_Click(object sender, EventArgs e)
        {
            if (!TryGetSelectedGameWithAppId(out GameConfig game))
                return;

            string assetsPath = PathConstants.CombineGamesPerAppResourcesDirectory(
                PathConstants.GamesDirectory,
                game.AppId.ToString());
            ShellFolderHelper.OpenFolderForOwner(this, assetsPath, createIfMissing: true, "Folder Not Found", "Could Not Open Game Assets Folder");
        }

        private void OnOpenGoldbergFolder_Click(object sender, EventArgs e) =>
            ShellFolderHelper.OpenFolderForOwner(this, PathConstants.GoldbergDirectory, createIfMissing: true, "Folder Not Found", "Could Not Open Goldberg Folder");

        private void OnOpenLauncherFolder_Click(object sender, EventArgs e) =>
            ShellFolderHelper.OpenFolderForOwner(this, PathConstants.AppBaseDirectory, createIfMissing: false, "Folder Not Found", "Could Not Open Launcher Folder");

        private void OnOpenExtraDllsFolder_Click(object sender, EventArgs e)
        {
            string extraDllsDir = ServiceLocator.GoldbergFilesService.EnsureSteamClientExtraDllsDirectory();
            ShellFolderHelper.OpenFolderForOwner(this, extraDllsDir, createIfMissing: true, "Folder Not Found", "Could Not Open Extra DLLs Folder");
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
                string title = icon == MessageBoxIcon.Information ? "File Not Found" : "Could Not Open Inventory";
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
                AppTaskDialogHelper.ShowOk(
                    this,
                    "Please select a game with a valid App ID and executable path.",
                    MessageBoxIcon.Information);
                return;
            }

            if (!GameFolderPathHelper.TryGetExistingExecutableDirectory(game, out _))
            {
                AppTaskDialogHelper.ShowOk(
                    this,
                    "The game folder was not found. The files may have been moved or uninstalled.",
                    MessageBoxIcon.Warning);
                return;
            }

            ValidationResult result = ServiceLocator.EmulatorConfigService.TryEnsureSteamAppIdBesideExecutable(game);
            if (result.IsValid)
            {
                AppTaskDialogHelper.ShowOk(
                    this,
                    PathConstants.SteamAppIdFileName + " for appid " + game.AppId + " created successfully.",
                    MessageBoxIcon.Information);
                return;
            }

            string message = result.ErrorMessage ?? "Failed to create file.";
            MessageBoxIcon icon = message.IndexOf("does not exist", StringComparison.OrdinalIgnoreCase) >= 0
                ? MessageBoxIcon.Warning
                : MessageBoxIcon.Error;
            string title = icon == MessageBoxIcon.Warning ? "Folder Not Found" : "Create Steam App ID";
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
                }
                catch (Exception ex)
                {
                    FormMessageBoxHelper.ShowIfAlive(this, $"Failed to copy entry GUID: {ex.Message}", "Copy GUID", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                AppTaskDialogHelper.ShowOk(this, "Please select a game.", MessageBoxIcon.Information);
            }
        }

        private void OnCreateShortcut_Click(object sender, EventArgs e)
        {
            var game = GetSelectedGame();
            if (game == null)
            {
                AppTaskDialogHelper.ShowOk(this, "Please select a game.", MessageBoxIcon.Information);
                return;
            }

            if (!UriProtocolRegistryService.IsProtocolRegistered())
            {
                AppTaskDialogHelper.ShowOk(
                    this,
                    "The " + ApplicationConstants.UriProtocolAuthorityPrefix
                    + " protocol is not registered.\n" +
                    "Please restart the application to register it automatically.\n\n" +
                    "If the problem persists, try running the application as administrator.",
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
                    FileDialogBrowseHelper.ApplyInitialDirectory(
                        saveFileDialog,
                        FileDialogBrowseHelper.Purpose.Shortcut);

                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        FileDialogBrowseHelper.RememberFile(
                            FileDialogBrowseHelper.Purpose.Shortcut,
                            saveFileDialog.FileName);
                        GameFolderPathHelper.TryResolveIconSourcePath(game, out string iconPath);
                        if (string.IsNullOrEmpty(iconPath))
                            iconPath = null;

                        if (ShortcutService.Create(saveFileDialog.FileName, game.AppId, game.AppName, iconPath))
                        {
                            Program.LogService?.LogMessage(
                                "Shortcut created: " + saveFileDialog.FileName);
                        }
                        else
                        {
                            FormMessageBoxHelper.ShowIfAlive(this,
                                "Failed to create shortcut. Please check the file path and permissions.",
                                "Create Shortcut",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError("Failed to create shortcut", ex);
                FormMessageBoxHelper.ShowIfAlive(this,
                    "Failed to create shortcut. Please check the file path and permissions.",
                    "Create Shortcut",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void ctxGamesItem_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var game = GetSelectedGame();
            bool hasGame = game != null;
            bool hasAppId = hasGame && game.AppId > 0;
            bool exePresent = hasGame && GameFolderPathHelper.TryResolveStoredExecutable(game, out _);
            bool exeFolderPresent = hasGame && GameFolderPathHelper.TryGetExistingExecutableDirectory(game, out _);
            bool stubExePresent = hasGame && GameFolderPathHelper.TryResolveExecutableForStubRemoval(game, out _);

            miCtxRowRun.Enabled = exePresent;
            miCtxRowRunWithoutEmu.Enabled = exePresent;
            miCtxRowOpenExecutableFolder.Enabled = exeFolderPresent;
            miCtxRowCreateSteamAppIdFile.Enabled = exeFolderPresent && hasAppId;
            miCtxRowCreateShortcut.Enabled = hasGame;
            miCtxRowOpenValveDataFile.Enabled = hasAppId;
            miCtxRowOpenGameAssetsFolder.Enabled = hasAppId;

            miCtxRowGuid.Enabled = hasGame;
            miCtxRowCopyGuid.Text = hasGame
                ? game.GameGuid.ToString()
                : "{guid}";

            miCtxRowRemoveSteamStub.Visible = true;
            miCtxRowRemoveSteamStub.Enabled = stubExePresent;
            if (hasGame && !stubExePresent)
                Program.LogService?.LogDebug($"Remove SteamStub disabled: could not resolve executable (StartFolder={game.StartFolder}, Path={game.Path}).");
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
                    _taskReportService.SetMessageWithAutoClear("Save the game before launching it.");
                    return;
                }

                if (!TryEnsureGameExecutablePresent(game))
                    return;

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

            if (!SteamApiValidator.HasDirtySteamApi(apiStatus))
            {
                Program.LogService?.LogDebug("Steam API DLLs validation passed");
                return true;
            }

            Program.LogService?.LogWarning("Steam API DLLs appear to be modified or unknown versions");
            bool hasBackups = apiStatus.CleanBackups != null && apiStatus.CleanBackups.Count > 0;
            string message =
                "Modded Steam API DLLs found.\n\n" +
                (hasBackups
                    ? "A known-good file was found elsewhere in this folder (name contains \"steam_api\")."
                    : "No known-good alternate file was found.\n" +
                      "Searched recursively; skipped folders that could not be read.");
            const int idRestore = 100;
            const int idLaunchAnyway = 101;
            AppTaskDialogResult validationResult = AppTaskDialogForm.Show(
                this,
                new AppTaskDialogRequest
                {
                    Content = message,
                    Icon = MessageBoxIcon.Warning,
                    Buttons = new List<AppTaskDialogButton>
                    {
                        new AppTaskDialogButton(idRestore, "Restore and launch"),
                        new AppTaskDialogButton(idLaunchAnyway, "Launch without restoring") { IsDefault = true },
                        new AppTaskDialogButton(TaskDialogHelper.IdCancel, "Cancel") { IsCancel = true }
                    }
                });
            int validationButton = validationResult.ButtonId;
            if (validationButton == TaskDialogHelper.IdCancel)
            {
                Program.LogService?.LogDebug("User cancelled launch due to Steam API validation");
                return false;
            }
            if (validationButton == idRestore)
            {
                if (!hasBackups)
                {
                    AppTaskDialogHelper.ShowOk(
                        this,
                        "No known-good Steam API file was found in the game folder to restore from.",
                        MessageBoxIcon.Information);
                    Program.LogService?.LogDebug("User chose restore but no clean backup was found; continuing launch");
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
                Program.LogService?.LogDebug("User chose to launch without restoring Steam API DLLs");

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
                if (IsDisposed || Disposing || FormLifetimeToken.IsCancellationRequested)
                    return;
                Program.LogService?.LogDebug($"MainForm: LaunchGameInternalAsync called for {game?.AppName} (AppId: {game?.AppId}), useEmulator: {useEmulator}");

                if (!TryEnsureGameExecutablePresent(game))
                    return;

                if (_gameLaunchService.IsGameRunning(game.AppId, game))
                {
                    Program.LogService?.LogWarning($"Launch blocked: {game.AppName} is already running.");
                    ShowLaunchErrorMessage(
                        $"{game.AppName} is already running. Close the game before launching again.");
                    return;
                }

                if (!await TryEnsureEmulatorPrerequisiteForLaunch(game, useEmulator).ConfigureAwait(true))
                    return;

                if (IsDisposed || Disposing || FormLifetimeToken.IsCancellationRequested)
                    return;

                if (!TryValidateSteamApiBeforeLaunch(game, useEmulator))
                    return;

                Program.LogService?.LogDebug("Checking for launch options...");
                var launchResult = await _launchOptionService
                    .ShowLaunchOptionsAsync(game, this, FormLifetimeToken)
                    .ConfigureAwait(true);
                if (launchResult.Cancelled || FormLifetimeToken.IsCancellationRequested)
                {
                    Program.LogService?.LogDebug("User cancelled launch options dialog");
                    return;
                }

                LaunchOption launchOption = launchResult.SkipLauncher ? null : launchResult.LaunchOption;
                Program.LogService?.LogDebug($"Launch option selected: {(launchOption != null ? launchOption.Description ?? launchOption.Executable : "None (default)")}, SkipLauncher: {launchResult.SkipLauncher}");

                string launchExecutablePath = TryResolveLaunchExecutableForStubCheck(game, launchOption);
                if (!await OfferSteamStubRemovalIfNeededAsync(launchExecutablePath, game.AppId, game.AppName).ConfigureAwait(true))
                {
                    Program.LogService?.LogDebug("User cancelled SteamStub prompt; launch aborted");
                    return;
                }
                if (IsDisposed || Disposing || FormLifetimeToken.IsCancellationRequested)
                    return;

                Program.LogService?.LogDebug("Calling GameLaunchService.LaunchGame...");
                var launchGameResult = _gameLaunchService.LaunchGame(game, useEmulator: useEmulator, launchOption: launchOption);

                if (!launchGameResult.IsValid)
                {
                    Program.LogService?.LogError(
                        $"Launch failed: AppId={game.AppId}, name={game.AppName}, mode={game.LaunchMode}, emu={useEmulator}: {launchGameResult.ErrorMessage}");
                    ShowLaunchErrorMessage(launchGameResult.ErrorMessage);
                }
                else
                {
                    Program.LogService?.LogDebug("Launch completed from MainForm perspective");
                    _taskReportService.SetMessageWithAutoClear($"{game.AppName} launched.");
                }
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Error during game launch: AppId={game?.AppId}, name={game?.AppName}", ex);
                ShowLaunchErrorMessage(ex.Message);
            }
        }

        private bool TryEnsureGameExecutablePresent(GameConfig game)
        {
            if (game != null && GameFolderPathHelper.TryResolveStoredExecutable(game, out _))
                return true;

            Program.LogService?.LogError(
                $"Launch aborted: executable not found (AppId={game?.AppId}, name={game?.AppName}, path={game?.Path})");
            ShowLaunchErrorMessage(GameFolderPathHelper.GetMissingStoredExecutableMessage(game));
            return false;
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
            AppTaskDialogHelper.ShowOk(
                this,
                $"Failed to launch game: {errorMessage}",
                MessageBoxIcon.Error);
        }

        private void EditGame(Guid gameGuid)
        {
            var game = _gameDataService.GetGame(gameGuid);
            if (game == null)
                return;

            GameEditBundle editBundle = ServiceLocator.GameEditLoader.Load(game);

            try
            {
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
            finally
            {
                editBundle?.ReleaseHeavyRuntimeData();
                // The dialog edits the live library row; App ID lookups attach Catalog/AppInfo to it even on Cancel.
                game.ReleaseHeavyRuntimeData();
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
            AppTaskDialogHelper.ShowOk(
                this,
                "Please select a game with a valid App ID.",
                MessageBoxIcon.Information);
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
            using (ServiceLocator.SteamProductInfoService.HoldSession(preWarm: true))
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
                _addSaveWaitingForAssetsGuids.Remove(gameGuid);
                ScheduleRefreshGames(reloadTiles: true);
                return;
            }

            string priorMosaicKey = _pendingAddMosaicImageKey;
            _pendingAddMosaicImageKey = null;

            var viewMode = _appDataService.GetViewMode();
            var tileImageList = GetTileImageListForViewMode(viewMode);
            string libraryMosaicKey = GameDisplayService.GetMosaicImageKey(game);

            if (reloadMosaic)
                _addSaveWaitingForAssetsGuids.Remove(gameGuid);

            var item = GameDisplayService.FindListItemByGameGuid(lstGames, gameGuid);
            if (item != null)
            {
                bool libraryKeySeeded = false;
                if (IsMosaicViewMode(viewMode)
                    && !reloadMosaic
                    && _addSaveWaitingForAssetsGuids.Contains(gameGuid)
                    && !string.IsNullOrEmpty(libraryMosaicKey))
                {
                    libraryKeySeeded = TrySeedMosaicKeyWithWaitingPlaceholder(libraryMosaicKey, viewMode);
                }

                _gameDisplayService.UpdateListViewItem(
                    item,
                    game,
                    viewMode,
                    tileImageList ?? _largeImageList,
                    tileImageList ?? _smallImageList,
                    GetImportPendingPredicate(),
                    GetAddPendingPredicate(),
                    GetUpdatePendingPredicate());

                if (IsMosaicViewMode(viewMode))
                {
                    bool libraryKeyReady = !string.IsNullOrEmpty(libraryMosaicKey)
                        && tileImageList != null
                        && tileImageList.Images.ContainsKey(libraryMosaicKey);

                    // If the AppId key is not ready yet, keep pointing at pending-* art so Save does not blank the tile.
                    if (!reloadMosaic
                        && !libraryKeyReady
                        && !string.IsNullOrEmpty(priorMosaicKey)
                        && tileImageList != null
                        && tileImageList.Images.ContainsKey(priorMosaicKey))
                    {
                        item.ImageKey = priorMosaicKey;
                    }
                    else if (!string.IsNullOrEmpty(priorMosaicKey)
                        && priorMosaicKey.StartsWith("pending-", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(priorMosaicKey, libraryMosaicKey, StringComparison.Ordinal)
                        && (reloadMosaic || libraryKeySeeded || libraryKeyReady))
                    {
                        RemoveMosaicImageKey(priorMosaicKey);
                    }

                    if (reloadMosaic)
                        _ = UpsertMosaicTileForGameAsync(game, viewMode).ForgetFaults(Program.LogService, nameof(UpsertMosaicTileForGameAsync));
                    else if (!libraryKeyReady && _addSaveWaitingForAssetsGuids.Contains(gameGuid))
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
                    applySort,
                    GetUpdatePendingPredicate());
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
                        CancellationToken ct = ServiceLocator.ApplicationLifetimeToken;
                        ct.ThrowIfCancellationRequested();
                        var result = await _appDataService.EnsureGlobalConfigFilesExistAsync().ConfigureAwait(false);
                        if (!result.IsValid)
                            Program.LogService?.LogWarning($"Deferred config setup: {result.ErrorMessage}");
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        Program.LogService?.LogError($"Deferred config setup failed: {ex.Message}", ex);
                    }
                }, ServiceLocator.ApplicationLifetimeToken).ForgetFaults(Program.LogService, "DeferredEnsureGlobalConfigFiles");

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
                Program.LogService?.LogError("Failed to load form state", ex);
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
                Program.LogService?.LogError("Failed to save window state", ex);
            }

            if (_closeAfterDisposeReady)
                return;

            CancelFormLifetime();

            // Forced exits cannot cancel FormClosing; tear down synchronously (Steam dispose interrupts waiters).
            if (e.CloseReason == CloseReason.WindowsShutDown
                || e.CloseReason == CloseReason.TaskManagerClosing
                || e.CloseReason == CloseReason.ApplicationExitCall)
            {
                if (!_closeDisposeStarted)
                {
                    _closeDisposeStarted = true;
                    try
                    {
                        ServiceLocator.DisposeApplicationResources();
                    }
                    catch
                    {
                    }
                }

                _closeAfterDisposeReady = true;
                return;
            }

            e.Cancel = true;
            if (_closeDisposeStarted)
                return;

            _closeDisposeStarted = true;
            Enabled = false;
            UseWaitCursor = true;

            _ = Task.Run(() =>
            {
                ServiceLocator.DisposeApplicationResources();
            }).ContinueWith(_ =>
            {
                void FinishClose()
                {
                    _closeAfterDisposeReady = true;
                    if (!IsDisposed && !Disposing)
                        Close();
                }

                if (IsDisposed || Disposing)
                    return;

                try
                {
                    if (InvokeRequired)
                    {
                        if (IsHandleCreated)
                            BeginInvoke(new Action(FinishClose));
                        return;
                    }

                    FinishClose();
                }
                catch (ObjectDisposedException)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }, TaskScheduler.Default).ForgetFaults(Program.LogService, "DisposeApplicationResourcesThenClose");
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
                _appDataService.CompleteFirstRun();
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError("Failed to handle first run", ex);
                _appDataService.CompleteFirstRun();
            }
        }
    }
}
