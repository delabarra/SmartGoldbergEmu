using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Forms
{
    public partial class GameSearchForm : Form
    {
        private const int SearchDebounceMs = 300;
        private const int MaxSearchResults = 20;
        private const int MaxStatusWidthPx = 424;

        private List<AppSearchResult> _searchResults = new List<AppSearchResult>();
        private ulong? _selectedAppId;
        private readonly ThemeService _themeService;
        private CancellationTokenSource _searchCancellationTokenSource;
        private CancellationTokenSource _debounceCancellationTokenSource;
        private bool _isSearching;
        private int _lastTooltipIndex = -1;

        public ulong? SelectedAppId => _selectedAppId;

        private bool HasValidSelection =>
            lstResults.SelectedIndex >= 0 && lstResults.SelectedIndex < _searchResults.Count;

        public GameSearchForm() : this(ServiceLocator.ThemeService)
        {
        }

        public GameSearchForm(ThemeService themeService)
        {
            InitializeComponent();
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            btnOK.Enabled = false;
            ApplyTheme();
            _themeService.ThemeChanged += ThemeService_ThemeChanged;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            txtSearch.Focus();
        }

        private async void OnSearchTextChanged(object sender, EventArgs e)
        {
            CancelAndDispose(ref _searchCancellationTokenSource);
            CancelAndDispose(ref _debounceCancellationTokenSource);

            var searchTerm = txtSearch.Text.Trim();
            if (string.IsNullOrEmpty(searchTerm))
            {
                ClearResults();
                return;
            }

            _debounceCancellationTokenSource = new CancellationTokenSource();
            var debounceToken = _debounceCancellationTokenSource.Token;

            try
            {
                await Task.Delay(SearchDebounceMs, debounceToken).ConfigureAwait(false);
                var stillCurrent = false;
                RunOnUiThread(() =>
                {
                    if (IsDisposed || Disposing)
                        return;
                    stillCurrent = string.Equals(txtSearch.Text.Trim(), searchTerm, StringComparison.Ordinal);
                });
                if (!stillCurrent)
                    return;
                await PerformSearchAsync(searchTerm).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex) when (!IsDisposed && !Disposing)
            {
                UpdateStatus($"Error: {ex.Message}");
                Program.LogService?.LogError("Search error", ex);
            }
        }

        private static void CancelAndDispose(ref CancellationTokenSource cts)
        {
            if (cts == null)
                return;
            cts.Cancel();
            cts.Dispose();
            cts = null;
        }

        private void ClearResults()
        {
            lstResults.Items.Clear();
            _searchResults.Clear();
            _selectedAppId = null;
            _lastTooltipIndex = -1;
            UpdateStatus("Ready");
        }

        private void UpdateStatus(string message, string tooltipText = null)
        {
            if (InvokeRequired)
            {
                if (IsDisposed || Disposing)
                    return;
                Invoke(new Action<string, string>(UpdateStatus), message, tooltipText);
                return;
            }
            lblStatus.Text = message;
            toolTip.SetToolTip(lblStatus, tooltipText);
        }

        private void RunOnUiThread(Action action)
        {
            if (IsDisposed || Disposing)
                return;
            if (InvokeRequired)
                Invoke(action);
            else
                action();
        }

        private void SetSearchingState(bool isSearching)
        {
            RunOnUiThread(() =>
            {
                _isSearching = isSearching;
                btnOK.Enabled = !isSearching && HasValidSelection;
                lstResults.Enabled = !isSearching;
            });
        }

        private async Task PerformSearchAsync(string searchTerm)
        {
            CancelAndDispose(ref _searchCancellationTokenSource);
            _searchCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _searchCancellationTokenSource.Token;

            try
            {
                SetSearchingState(true);
                UpdateStatus("Searching...");
                RunOnUiThread(() =>
                {
                    if (IsDisposed || Disposing)
                        return;
                    lstResults.Items.Clear();
                    _searchResults.Clear();
                    _selectedAppId = null;
                    _lastTooltipIndex = -1;
                });

                Program.LogService?.LogDebug($"Starting search for: {searchTerm}");

                var results = await FetchSearchResultsAsync(searchTerm).ConfigureAwait(false);

                Program.LogService?.LogDebug($"Search returned {results?.Count ?? 0} results");

                if (cancellationToken.IsCancellationRequested || IsDisposed || Disposing)
                    return;

                _searchResults = results ?? new List<AppSearchResult>();
                RunOnUiThread(UpdateSearchResults);
            }
            catch (OperationCanceledException)
            {
                if (!IsDisposed && !Disposing)
                    UpdateStatus("Search cancelled");
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested && !IsDisposed && !Disposing)
                {
                    var errorMsg = $"Search error: {ex.Message}";
                    if (ex.InnerException != null)
                        errorMsg += $" ({ex.InnerException.Message})";
                    UpdateStatus(errorMsg);
                    Program.LogService?.LogError($"Search error: {ex.Message}", ex);
                }
            }
            finally
            {
                if (!IsDisposed && !Disposing)
                    SetSearchingState(false);
            }
        }

        private async Task<List<AppSearchResult>> FetchSearchResultsAsync(string searchTerm)
        {
            if (IsDisposed || Disposing)
                return new List<AppSearchResult>();

            if (!ulong.TryParse(searchTerm, out var appId))
                return await SteamGameSearchService.SearchByNameAsync(searchTerm, maxResults: MaxSearchResults).ConfigureAwait(false);

            UpdateStatus($"Looking up App ID {appId} in Steam Network...");
            var (appData, _) = await ServiceLocator.GameSetupService
                .FetchPicsMetadataWithRootAsync(appId.ToString())
                .ConfigureAwait(false);

            if (IsDisposed || Disposing)
                return new List<AppSearchResult>();

            if (appData != null && appData.Success && !string.IsNullOrEmpty(appData.Name))
            {
                if (appData.Type != null && string.Equals(appData.Type, "game", StringComparison.OrdinalIgnoreCase))
                {
                    return new List<AppSearchResult>
                    {
                        new AppSearchResult
                        {
                            AppId = appId,
                            Name = appData.Name,
                            Source = "Steam (game assets)"
                        }
                    };
                }
                UpdateStatus($"App ID {appId} is not a game (type: {appData.Type ?? "unknown"})");
            }
            else
            {
                UpdateStatus($"App ID {appId} not found");
            }

            return new List<AppSearchResult>();
        }

        private void UpdateSearchResults()
        {
            if (_searchResults.Count > 0)
            {
                lstResults.Items.AddRange(_searchResults.ToArray());
                UpdateStatus($"Found {_searchResults.Count} result(s)");
                lstResults.SelectedIndex = 0;
            }
            else
            {
                UpdateStatus("No results found");
            }
        }

        private void OnSearchKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;
            e.SuppressKeyPress = true;
            if (HasValidSelection)
                OnOk_Click(sender, e);
            else if (lstResults.Items.Count > 0)
                lstResults.SelectedIndex = 0;
        }

        private static string TruncateNameToFit(string name, Font font, int maxWidthPx)
        {
            const string prefix = "Name: \"";
            const string suffix = "\"";
            var prefixW = TextRenderer.MeasureText(prefix, font).Width;
            var suffixW = TextRenderer.MeasureText(suffix, font).Width;
            var availableForName = Math.Max(20, maxWidthPx - prefixW - suffixW);

            var displayName = name ?? string.Empty;
            if (string.IsNullOrEmpty(displayName))
                return prefix + suffix;
            if (TextRenderer.MeasureText(displayName, font).Width <= availableForName)
                return prefix + displayName + suffix;

            const string ellipsis = "...";
            for (var len = displayName.Length - 1; len > 0; len--)
            {
                var truncated = displayName.Substring(0, len) + ellipsis;
                if (TextRenderer.MeasureText(truncated, font).Width <= availableForName)
                    return prefix + truncated + suffix;
            }
            return prefix + ellipsis + suffix;
        }

        private void OnResultsSelectedIndexChanged(object sender, EventArgs e)
        {
            if (HasValidSelection)
            {
                var selectedResult = _searchResults[lstResults.SelectedIndex];
                _selectedAppId = selectedResult.AppId;
                var nameDisplay = TruncateNameToFit(selectedResult.Name, lblStatus.Font, MaxStatusWidthPx);
                var fullText = $"AppId: {selectedResult.AppId}{Environment.NewLine}Name: \"{selectedResult.Name}\"";
                UpdateStatus($"AppId: {selectedResult.AppId}{Environment.NewLine}{nameDisplay}", fullText);
                btnOK.Enabled = true;
            }
            else
            {
                _selectedAppId = null;
                UpdateStatus(_isSearching ? "Searching..." : "Ready");
                btnOK.Enabled = false;
            }
        }

        private void AcceptSelection()
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void OnResultsDoubleClick(object sender, EventArgs e)
        {
            if (HasValidSelection)
                AcceptSelection();
        }

        private void OnResultsMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;
            var index = lstResults.IndexFromPoint(e.Location);
            if (index >= 0 && index < _searchResults.Count)
                lstResults.SelectedIndex = index;
        }

        private void OnResultsMouseMove(object sender, MouseEventArgs e)
        {
            var index = lstResults.IndexFromPoint(e.Location);
            if (index == _lastTooltipIndex)
                return;
            _lastTooltipIndex = index;
            if (index >= 0 && index < _searchResults.Count)
            {
                var result = _searchResults[index];
                toolTip.SetToolTip(lstResults, $"AppId: {result.AppId}{Environment.NewLine}Name: \"{result.Name}\"");
            }
            else
            {
                toolTip.SetToolTip(lstResults, "");
            }
        }

        private void OnListContextOpening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = !HasValidSelection;
        }

        private void CopySelectedResult(Func<AppSearchResult, string> getText)
        {
            if (!HasValidSelection)
                return;
            try
            {
                Clipboard.SetText(getText(_searchResults[lstResults.SelectedIndex]) ?? string.Empty);
            }
            catch
            {
            }
        }

        private void OnCopyAppId_Click(object sender, EventArgs e) =>
            CopySelectedResult(r => r.AppId.ToString());

        private void OnCopyName_Click(object sender, EventArgs e) =>
            CopySelectedResult(r => r.Name ?? string.Empty);

        private void OnOk_Click(object sender, EventArgs e)
        {
            if (HasValidSelection)
                AcceptSelection();
            else
                FormMessageBoxHelper.ShowIfAlive(this, "Please select a game from the list.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void ApplyTheme()
        {
            _themeService.ApplyTheme(this);
        }

        private void ThemeService_ThemeChanged(object sender, ThemeChangedEventArgs e)
        {
            if (IsDisposed || Disposing)
                return;
            RunOnUiThread(ApplyTheme);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CancelAndDispose(ref _searchCancellationTokenSource);
                CancelAndDispose(ref _debounceCancellationTokenSource);
                if (_themeService != null)
                    _themeService.ThemeChanged -= ThemeService_ThemeChanged;
                components?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
