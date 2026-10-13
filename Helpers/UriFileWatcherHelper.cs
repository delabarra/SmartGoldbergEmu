using System;
using System.IO;
using System.Windows.Forms;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Helpers
{
    // Picks up URI protocol handoff files written by a second app instance.
    public class UriFileWatcherHelper : IDisposable
    {
        private FileSystemWatcher _fileWatcher;
        private readonly Control _control; // For BeginInvoke
        private Action<ulong> _onUriProcessed;
        private bool _disposed;

        public UriFileWatcherHelper(Control control, Action<ulong> onUriProcessed)
        {
            _control = control ?? throw new ArgumentNullException(nameof(control));
            _onUriProcessed = onUriProcessed ?? throw new ArgumentNullException(nameof(onUriProcessed));
        }

        public void Setup()
        {
            if (_disposed)
                return;
            try
            {
                DisposeFileWatcher();

                string handoffDir = PathConstants.LocalAppDataPerUserDirectory;
                Directory.CreateDirectory(handoffDir);

                _fileWatcher = new FileSystemWatcher(handoffDir)
                {
                    Filter = PathConstants.LauncherUriProtocolPendingFileSearchPattern,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true
                };

                // The second instance renames a finished .partial file into place (Renamed); Created covers older writers.
                _fileWatcher.Created += FileWatcher_Created;
                _fileWatcher.Renamed += FileWatcher_Renamed;

                CheckExistingUriFiles(handoffDir);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning($"Failed to setup URI file watcher: {ex.Message}");
            }
        }

        // Handoff files may have been written before the watcher started.
        private void CheckExistingUriFiles(string handoffDir)
        {
            try
            {
                if (string.IsNullOrEmpty(handoffDir) || !Directory.Exists(handoffDir))
                    return;

                var uriFiles = Directory.GetFiles(handoffDir, PathConstants.LauncherUriProtocolPendingFileSearchPattern);
                foreach (var file in uriFiles)
                {
                    ProcessUriFile(file);
                }
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning($"Error checking existing URI files: {ex.Message}");
            }
        }

        private void FileWatcher_Created(object sender, FileSystemEventArgs e)
        {
            QueueUriFile(e.FullPath);
        }

        private void FileWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            if (!e.Name.EndsWith(PathConstants.LauncherUriProtocolPendingFileExtension, StringComparison.OrdinalIgnoreCase))
                return;
            QueueUriFile(e.FullPath);
        }

        // Watcher events arrive on a thread-pool thread; an exception here would terminate the process.
        private void QueueUriFile(string fullPath)
        {
            if (_disposed || _control == null || _control.IsDisposed || _control.Disposing || !_control.IsHandleCreated)
                return;

            try
            {
                _control.BeginInvoke(new Action(() =>
                {
                    if (_disposed || _control == null || _control.IsDisposed || _control.Disposing)
                        return;
                    ProcessUriFile(fullPath);
                }));
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ObjectDisposedException)
            {
                Program.LogService?.LogWarning($"URI handoff skipped while the main window is closing: {ex.Message}");
            }
        }

        private void ProcessUriFile(string filePath)
        {
            try
            {
                if (_disposed || _control == null || _control.IsDisposed || _control.Disposing)
                    return;
                if (!File.Exists(filePath))
                    return;

                string uri = File.ReadAllText(filePath, System.Text.Encoding.UTF8).Trim();

                try
                {
                    File.Delete(filePath);
                }
                catch (Exception ex)
                {
                    Program.LogService?.LogWarning($"Failed to delete URI temp file: {ex.Message}");
                }

                var parseResult = UriProtocolService.ParseRunCommand(uri);
                if (parseResult.Success)
                {
                    Program.LogService?.LogMessage($"URI protocol launch from another instance: AppId {parseResult.AppId}");
                    if (!_disposed && _onUriProcessed != null && _control != null && !_control.IsDisposed && !_control.Disposing)
                        _onUriProcessed(parseResult.AppId);
                }
                else
                {
                    Program.LogService?.LogWarning($"Invalid URI from file: {uri} - {parseResult.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Error processing URI file: {ex.Message}", ex);
            }
        }

        public void Dispose()
        {
            _disposed = true;
            DisposeFileWatcher();
            _onUriProcessed = null;
        }

        private void DisposeFileWatcher()
        {
            if (_fileWatcher == null)
                return;

            _fileWatcher.EnableRaisingEvents = false;
            _fileWatcher.Created -= FileWatcher_Created;
            _fileWatcher.Renamed -= FileWatcher_Renamed;
            _fileWatcher.Dispose();
            _fileWatcher = null;
        }
    }
}

