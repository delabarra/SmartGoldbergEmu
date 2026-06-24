using System;
using System.Windows.Forms;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Helpers
{
    /// <summary>
    /// Helper class for managing API key status indicator with animation.
    /// </summary>
    public class ApiKeyStatusIndicatorHelper : IDisposable
    {
        private readonly ToolStripStatusLabel _statusLabel;
        private readonly SteamApiKeyService _apiKeyService;
        private Timer _animationTimer;
        private int _animationStep;
        private EventHandler _clickHandler;

        /// <summary>
        /// Event fired when the indicator is clicked (to open settings).
        /// </summary>
        public event EventHandler IndicatorClicked;

        /// <summary>
        /// Initializes a new instance of the ApiKeyStatusIndicatorHelper.
        /// </summary>
        /// <param name="statusLabel">The tool strip status label to use as the status indicator.</param>
        /// <param name="apiKeyService">The API key service to check status.</param>
        public ApiKeyStatusIndicatorHelper(ToolStripStatusLabel statusLabel, SteamApiKeyService apiKeyService)
        {
            _statusLabel = statusLabel ?? throw new ArgumentNullException(nameof(statusLabel));
            _apiKeyService = apiKeyService ?? throw new ArgumentNullException(nameof(apiKeyService));
        }

        /// <summary>
        /// Initializes the API key status indicator.
        /// </summary>
        public void Initialize()
        {
            // Clean up any existing click handler to prevent duplicates
            if (_clickHandler != null)
            {
                _statusLabel.Click -= _clickHandler;
            }

            // Clean up any existing animation timer
            CleanupTimer();

            // Get API key status from service
            var status = _apiKeyService.GetStatus();

            // Check if API key is missing or invalid
            if (!status.HasKey || !status.HasValidFormat || !status.IsValid)
            {
                // If key exists but has invalid format, remove it
                if (status.HasKey && !status.HasValidFormat)
                {
                    _apiKeyService.RemoveApiKey();
                    status = _apiKeyService.GetStatus(); // Refresh status after removal
                }

                // Show the indicator and start animation
                _statusLabel.Visible = true;
                StartAnimation();

                // Add click event handler
                _clickHandler = (s, e) => OnIndicatorClicked();
                _statusLabel.Click += _clickHandler;
            }
            else
            {
                // Valid API key - hide indicator
                _statusLabel.Visible = false;
            }
        }

        /// <summary>
        /// Updates the API key status indicator without showing error messages.
        /// Used for real-time updates when validation happens in SettingsForm.
        /// </summary>
        public void Update()
        {
            // Clean up any existing animation timer
            CleanupTimer();

            // Get API key status from service
            var status = _apiKeyService.GetStatus();

            // Check if API key is missing or invalid
            if (!status.HasKey || !status.HasValidFormat || !status.IsValid)
            {
                // Show the indicator and start animation
                _statusLabel.Visible = true;
                StartAnimation();

                // Ensure click event handler is attached
                if (_clickHandler != null)
                {
                    _statusLabel.Click -= _clickHandler;
                }
                _clickHandler = (s, e) => OnIndicatorClicked();
                _statusLabel.Click += _clickHandler;
            }
            else
            {
                // Valid API key - hide indicator
                _statusLabel.Visible = false;
            }
        }

        private void StartAnimation()
        {
            // Create timer for emoji animation
            _animationTimer = new Timer();
            _animationTimer.Interval = 500; // 0.5 seconds
            _animationTimer.Tick += AnimationTimer_Tick;
            _animationTimer.Start();
            _animationStep = 0;
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            if (_statusLabel == null || _statusLabel.IsDisposed)
            {
                CleanupTimer();
                return;
            }

            if (_animationStep < 6) // 3 seconds / 0.5 seconds = 6 steps
            {
                // Show warning emoji for 3 seconds (6 steps of 0.5s each)
                _statusLabel.Text = "⚠️";
                _animationStep++;
            }
            else if (_animationStep < 7) // 0.5 seconds (1 step)
            {
                // Show key emoji for 0.5 seconds
                _statusLabel.Text = "🔑";
                _animationStep++;
            }
            else
            {
                // Reset animation
                _animationStep = 0;
            }
        }

        private void OnIndicatorClicked()
        {
            if (_statusLabel == null || _statusLabel.IsDisposed)
                return;
            IndicatorClicked?.Invoke(this, EventArgs.Empty);
        }

        private void CleanupTimer()
        {
            if (_animationTimer != null)
            {
                _animationTimer.Stop();
                _animationTimer.Tick -= AnimationTimer_Tick;
                _animationTimer.Dispose();
                _animationTimer = null;
            }
        }

        /// <summary>
        /// Disposes of the helper resources.
        /// </summary>
        public void Dispose()
        {
            CleanupTimer();
            if (_clickHandler != null && _statusLabel != null)
            {
                _statusLabel.Click -= _clickHandler;
            }
        }
    }
}

