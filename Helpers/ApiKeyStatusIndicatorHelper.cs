using System;
using System.Windows.Forms;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Helpers
{
    // Status-bar slot stays wired for Settings refresh, but missing keys are not treated as a warning.
    public class ApiKeyStatusIndicatorHelper : IDisposable
    {
        private readonly ToolStripStatusLabel _statusLabel;
        private readonly SteamApiKeyService _apiKeyService;

        public ApiKeyStatusIndicatorHelper(ToolStripStatusLabel statusLabel, SteamApiKeyService apiKeyService)
        {
            _statusLabel = statusLabel ?? throw new ArgumentNullException(nameof(statusLabel));
            _apiKeyService = apiKeyService ?? throw new ArgumentNullException(nameof(apiKeyService));
        }

        public void Initialize()
        {
            HideIndicator();
            // Drop invalid-format keys quietly so Settings/start stay consistent.
            ApiKeyStatus status = _apiKeyService.GetStatus();
            if (status.HasKey && !status.HasValidFormat)
                _apiKeyService.RemoveApiKey();
        }

        public void Update()
        {
            HideIndicator();
        }

        private void HideIndicator()
        {
            if (_statusLabel == null || _statusLabel.IsDisposed)
                return;
            _statusLabel.Visible = false;
            _statusLabel.Text = string.Empty;
        }

        public void Dispose()
        {
            HideIndicator();
        }
    }
}
