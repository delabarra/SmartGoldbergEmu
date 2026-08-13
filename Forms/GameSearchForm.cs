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
    public partial class GameSearchForm : ThemedForm
    {
        private const int SearchDebounceMs = 300;
        private const int MaxSearchResults = 20;
        private const int MaxStatusWidthPx = 424;
        private const string StatusFilteringUnidentifiedDlc = "Filtering out unidentified DLC...";

        private List<AppSearchResult> _searchResults = new List<AppSearchResult>();
        private ulong? _selectedAppId;
        private CancellationTokenSource _searchCancellationTokenSource;
        private CancellationTokenSource _debounceCancellationTokenSource;
        private GameSearchFilterConnection _filterConnection;
        private bool _isSearching;
        private bool _statusLocked;
        private int _lastTooltipIndex = -1;

        public ulong? SelectedAppId => _selectedAppId;

        private bool HasValidSelection =>
            lstResults.SelectedIndex >= 0 && lstResults.SelectedIndex < _searchResults.Count;

        public GameSearchForm() : this(ServiceLocator.ThemeService)
        {
        }

        public GameSearchForm(ThemeService themeService)
            : base(themeService)
        {
            InitializeComponent();
            btnOK.Enabled = false;
            _filterConnection = SteamGameSearchService.CreateFilterConnection();
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
            UpdateStatus("");
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
            _statusLocked = false;
            lblStatus.Text = message;
            toolTip.SetToolTip(lblStatus, tooltipText);
        }

        private void LockStatus(string message, CancellationToken cancellationToken)
        {
            if (InvokeRequired)
            {
                if (IsDisposed || Disposing)
                    return;
                Invoke(new Action<string, CancellationToken>(LockStatus), message, cancellationToken);
                return;
            }
            if (cancellationToken.IsCancellationRequested || IsDisposed || Disposing)
                return;
            _statusLocked = true;
            lblStatus.Text = message;
            toolTip.SetToolTip(lblStatus, null);
        }

        private void UpdateBusyStatus(string message)
        {
            if (_statusLocked)
                return;
            UpdateStatus(message);
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
                btnOK.Enabled = HasValidSelection;
            });
        }

        private async Task PerformSearchAsync(string searchTerm)
        {
            CancelAndDispose(ref _searchCancellationTokenSource);
            _searchCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                ServiceLocator.ApplicationLifetimeToken);
            var cancellationToken = _searchCancellationTokenSource.Token;

            try
            {
                SetSearchingState(true);
                UpdateStatus("Searching...");

                Program.LogService?.LogDebug($"Starting search for: {searchTerm}");

                var searchToken = cancellationToken;
                var progress = new Progress<IReadOnlyList<AppSearchResult>>(liveResults =>
                {
                    if (searchToken.IsCancellationRequested || IsDisposed || Disposing)
                        return;
                    ApplyLiveSearchResults(liveResults);
                });
                var statusProgress = new Progress<string>(status =>
                {
                    if (searchToken.IsCancellationRequested || IsDisposed || Disposing)
                        return;
                    LockStatus(
                        string.IsNullOrEmpty(status) ? StatusFilteringUnidentifiedDlc : status,
                        searchToken);
                });
                var results = await FetchSearchResultsAsync(
                    searchTerm, cancellationToken, progress, statusProgress)
                    .ConfigureAwait(false);

                Program.LogService?.LogDebug($"Search returned {results?.Count ?? 0} results");

                if (cancellationToken.IsCancellationRequested || IsDisposed || Disposing)
                    return;

                ApplyLiveSearchResults(results ?? new List<AppSearchResult>(), searchFinished: true);
            }
            catch (OperationCanceledException)
            {
                // Superseded by a newer query while typing — keep status as-is.
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

        private async Task<List<AppSearchResult>> FetchSearchResultsAsync(
            string searchTerm,
            CancellationToken cancellationToken,
            IProgress<IReadOnlyList<AppSearchResult>> progress,
            IProgress<string> statusProgress)
        {
            if (IsDisposed || Disposing)
                return new List<AppSearchResult>();

            cancellationToken.ThrowIfCancellationRequested();

            return await SteamGameSearchService
                .SearchByNameAsync(
                    searchTerm,
                    maxResults: MaxSearchResults,
                    cancellationToken: cancellationToken,
                    progress: progress,
                    statusProgress: statusProgress,
                    filterConnection: _filterConnection)
                .ConfigureAwait(false);
        }

        private void ApplyLiveSearchResults(IReadOnlyList<AppSearchResult> results)
        {
            ApplyLiveSearchResults(results, searchFinished: false);
        }

        private void ApplyLiveSearchResults(IReadOnlyList<AppSearchResult> results, bool searchFinished)
        {
            if (IsDisposed || Disposing)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ApplyLiveSearchResults(results, searchFinished)));
                return;
            }

            if (searchFinished)
                _statusLocked = false;

            var nextResults = results != null
                ? new List<AppSearchResult>(results)
                : new List<AppSearchResult>();

            if (!searchFinished && SameResultIds(_searchResults, nextResults))
                return;

            ulong? previousAppId = _selectedAppId;
            _searchResults = nextResults;
            _lastTooltipIndex = -1;

            lstResults.BeginUpdate();
            try
            {
                lstResults.Items.Clear();
                if (_searchResults.Count > 0)
                    lstResults.Items.AddRange(_searchResults.ToArray());
            }
            finally
            {
                lstResults.EndUpdate();
            }

            if (_searchResults.Count == 0)
            {
                _selectedAppId = null;
                btnOK.Enabled = false;
                if (searchFinished)
                    UpdateStatus("No results found");
                else
                    UpdateBusyStatus("Searching...");
                return;
            }

            int restoreIndex = -1;
            if (previousAppId.HasValue)
            {
                for (int i = 0; i < _searchResults.Count; i++)
                {
                    if (_searchResults[i].AppId == previousAppId.Value)
                    {
                        restoreIndex = i;
                        break;
                    }
                }
            }

            if (restoreIndex < 0)
                restoreIndex = 0;

            lstResults.SelectedIndex = restoreIndex;

            if (searchFinished)
            {
                ShowSelectedAppStatus();
            }
            else
            {
                if (HasValidSelection)
                {
                    _selectedAppId = _searchResults[lstResults.SelectedIndex].AppId;
                    btnOK.Enabled = true;
                }
                UpdateBusyStatus("Searching...");
            }
        }

        private static bool SameResultIds(List<AppSearchResult> current, List<AppSearchResult> next)
        {
            if (current == null || next == null)
                return current == next;
            if (current.Count != next.Count)
                return false;
            for (int i = 0; i < current.Count; i++)
            {
                if (current[i].AppId != next[i].AppId)
                    return false;
            }
            return true;
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
                ShowSelectedAppStatus();
            }
            else
            {
                _selectedAppId = null;
                if (_isSearching)
                    UpdateBusyStatus("Searching...");
                else
                    UpdateStatus("");
                btnOK.Enabled = false;
            }
        }

        private void ShowSelectedAppStatus()
        {
            if (!HasValidSelection)
                return;

            var selectedResult = _searchResults[lstResults.SelectedIndex];
            _selectedAppId = selectedResult.AppId;
            btnOK.Enabled = true;
            if (_statusLocked)
                return;

            var nameDisplay = TruncateNameToFit(selectedResult.Name, lblStatus.Font, MaxStatusWidthPx);
            var fullText = $"AppId: {selectedResult.AppId}{Environment.NewLine}Name: \"{selectedResult.Name}\"";
            UpdateStatus($"AppId: {selectedResult.AppId}{Environment.NewLine}{nameDisplay}", fullText);
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
                AppTaskDialogHelper.ShowOk(this, "Please select a game from the list.", MessageBoxIcon.Information);
        }

        private void OnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CancelAndDispose(ref _searchCancellationTokenSource);
                CancelAndDispose(ref _debounceCancellationTokenSource);
                if (_filterConnection != null)
                {
                    _filterConnection.Dispose();
                    _filterConnection = null;
                }
                components?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
