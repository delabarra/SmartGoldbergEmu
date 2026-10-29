using System;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;

namespace SmartGoldbergEmu.Services
{
    // Add-save status: pipeline steps plus the library image download running beside them.
    // Step text and step progress win; the image counter shows as text only without a step message and uses the bar when no step does.
    public sealed class AddSaveTaskReport : ITaskReportService
    {
        private readonly ITaskReportService _inner;
        private readonly string _gameDisplayName;
        private readonly object _sync = new object();
        private string _stepMessage = string.Empty;
        private TaskReportKind _stepKind = TaskReportKind.Info;
        private bool _stepProgressActive;
        // After Goldberg/achievements finish, keep showing library-image status even before the first N/M report.
        private bool _yieldStripToImages;
        private int _imagesCompleted;
        private int _imagesTotal;

        public AddSaveTaskReport(ITaskReportService inner, string gameDisplayName = null)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _gameDisplayName = string.IsNullOrWhiteSpace(gameDisplayName) ? null : gameDisplayName.Trim();
        }

        private bool ImagesActive => _imagesTotal > 0 && _imagesCompleted < _imagesTotal;

        // Drop pipeline step text so the library image counter can own the strip (e.g. after Goldberg/achievements finish).
        public void ClearStepMessage()
        {
            lock (_sync)
            {
                _stepMessage = string.Empty;
                _stepKind = TaskReportKind.Info;
                _stepProgressActive = false;
                _yieldStripToImages = true;
                ForwardMessage_NoLock();
                ForwardImageBar_NoLock();
            }
        }

        public void SetMessage(string message)
        {
            SetMessage(message, TaskReportKind.Info);
        }

        public void SetMessage(string message, TaskReportKind kind)
        {
            lock (_sync)
            {
                _stepMessage = message ?? string.Empty;
                _stepKind = kind;
                _yieldStripToImages = false;
                ForwardMessage_NoLock();
            }
        }

        public void SetMessageWithAutoClear(string message, TaskReportKind kind = TaskReportKind.Info, int delayMs = TaskReportDefaults.AutoClearDelayMs)
        {
            lock (_sync)
            {
                _stepMessage = message ?? string.Empty;
                _stepKind = kind;
                _stepProgressActive = false;
                _yieldStripToImages = false;
                if (!ImagesActive)
                {
                    _inner.SetMessageWithAutoClear(message, kind, delayMs);
                    return;
                }

                // Auto-clear would wipe the image counter mid-download; the next step replaces this text instead.
                ForwardMessage_NoLock();
                ForwardImageBar_NoLock();
            }
        }

        public void SetProgress(int current, int total)
        {
            lock (_sync)
            {
                _stepProgressActive = total > 0;
                if (_stepProgressActive)
                    _inner.SetProgress(current, total);
                else
                    ForwardImageBar_NoLock();
            }
        }

        public void ReportImageProgress(int completed, int total)
        {
            lock (_sync)
            {
                bool wasActive = ImagesActive;
                _imagesCompleted = completed;
                _imagesTotal = total;
                if (!wasActive && !ImagesActive)
                    return;

                if (string.IsNullOrEmpty(_stepMessage))
                    ForwardMessage_NoLock();
                if (!_stepProgressActive)
                    ForwardImageBar_NoLock();
            }
        }

        // Download ended (also on failure or early exit, where the counter never reaches its total).
        public void EndImageProgress()
        {
            lock (_sync)
            {
                _imagesCompleted = 0;
                _imagesTotal = 0;
                _yieldStripToImages = false;
                if (string.IsNullOrEmpty(_stepMessage))
                    ForwardMessage_NoLock();
                if (!_stepProgressActive)
                    ForwardImageBar_NoLock();
            }
        }

        private void ForwardMessage_NoLock()
        {
            bool showImageStatus = string.IsNullOrEmpty(_stepMessage) && (ImagesActive || _yieldStripToImages);
            string text = showImageStatus
                ? AddGameStatusMessages.DownloadingLibraryImages(_gameDisplayName, _imagesCompleted, _imagesTotal)
                : _stepMessage;
            _inner.SetMessage(text, _stepKind);
        }

        private void ForwardImageBar_NoLock()
        {
            if (ImagesActive)
                _inner.SetProgress(_imagesCompleted, _imagesTotal);
            else
                _inner.SetProgress(0, 0);
        }
    }
}
