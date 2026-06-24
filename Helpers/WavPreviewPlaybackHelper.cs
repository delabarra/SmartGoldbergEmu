using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SmartGoldbergEmu.Helpers
{
    // Win32 PlaySound (SND_ASYNC + SND_PURGE) stops reliably; SoundPlayer.PlaySync does not.
    internal static class WavPreviewPlaybackHelper
    {
        private static readonly object Sync = new object();
        private static int _activeSessionId = -1;
        private static Timer _endTimer;

        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        private static extern bool PlaySound(string pszSound, IntPtr hmod, SoundFlags fdwSound);

        [Flags]
        private enum SoundFlags : uint
        {
            Async = 0x0001,
            NoDefault = 0x0002,
            Purge = 0x0040,
            Filename = 0x00020000,
        }

        public static bool TryPlay(int sessionId, string wavPath, Form uiForm, Action<int> onPlaybackEnded)
        {
            if (string.IsNullOrWhiteSpace(wavPath) || uiForm == null)
                return false;

            lock (Sync)
            {
                StopLocked();
                _activeSessionId = sessionId;
                if (!PlaySound(wavPath, IntPtr.Zero, SoundFlags.Async | SoundFlags.Filename | SoundFlags.NoDefault))
                {
                    _activeSessionId = -1;
                    return false;
                }

                ScheduleEndTimer(sessionId, wavPath, uiForm, onPlaybackEnded);
                return true;
            }
        }

        public static void Stop()
        {
            lock (Sync)
            {
                _activeSessionId = -1;
                StopLocked();
            }
        }

        private static void StopLocked()
        {
            DisposeEndTimer();
            PlaySound(null, IntPtr.Zero, SoundFlags.Purge);
        }

        private static void ScheduleEndTimer(int sessionId, string wavPath, Form uiForm, Action<int> onPlaybackEnded)
        {
            DisposeEndTimer();

            int intervalMs = TryGetWavDurationMilliseconds(wavPath);
            if (intervalMs <= 0)
                intervalMs = 3000;
            intervalMs += 80;

            _endTimer = new Timer { Interval = intervalMs };
            _endTimer.Tick += (sender, e) =>
            {
                int endedSessionId;
                Action<int> callback;
                lock (Sync)
                {
                    DisposeEndTimer();
                    if (_activeSessionId != sessionId)
                        return;
                    endedSessionId = sessionId;
                    _activeSessionId = -1;
                    callback = onPlaybackEnded;
                }

                if (callback == null || uiForm.IsDisposed || !uiForm.IsHandleCreated)
                    return;

                try
                {
                    uiForm.BeginInvoke(new Action(() => callback(endedSessionId)));
                }
                catch (ObjectDisposedException)
                {
                }
            };
            _endTimer.Start();
        }

        private static void DisposeEndTimer()
        {
            if (_endTimer == null)
                return;
            _endTimer.Stop();
            _endTimer.Dispose();
            _endTimer = null;
        }

        private static int TryGetWavDurationMilliseconds(string wavPath)
        {
            try
            {
                using (var stream = File.OpenRead(wavPath))
                using (var reader = new BinaryReader(stream))
                {
                    if (stream.Length < 44)
                        return 0;
                    if (new string(reader.ReadChars(4)) != "RIFF")
                        return 0;
                    reader.ReadInt32();
                    if (new string(reader.ReadChars(4)) != "WAVE")
                        return 0;

                    int sampleRate = 0;
                    int bitsPerSample = 0;
                    int channels = 0;
                    int dataSize = 0;

                    while (stream.Position + 8 <= stream.Length)
                    {
                        string chunkId = new string(reader.ReadChars(4));
                        int chunkSize = reader.ReadInt32();
                        if (chunkSize < 0 || stream.Position + chunkSize > stream.Length)
                            break;

                        if (chunkId == "fmt ")
                        {
                            if (chunkSize < 16)
                            {
                                reader.ReadBytes(chunkSize);
                                continue;
                            }

                            reader.ReadInt16();
                            channels = reader.ReadInt16();
                            sampleRate = reader.ReadInt32();
                            reader.ReadInt32();
                            reader.ReadInt16();
                            bitsPerSample = reader.ReadInt16();
                            int remaining = chunkSize - 16;
                            if (remaining > 0)
                                reader.ReadBytes(remaining);
                        }
                        else if (chunkId == "data")
                        {
                            dataSize = chunkSize;
                            break;
                        }
                        else
                        {
                            reader.ReadBytes(chunkSize);
                        }
                    }

                    if (sampleRate <= 0 || channels <= 0 || bitsPerSample <= 0 || dataSize <= 0)
                        return 0;

                    int bytesPerSecond = sampleRate * channels * (bitsPerSample / 8);
                    if (bytesPerSecond <= 0)
                        return 0;

                    return (int)Math.Ceiling(dataSize * 1000.0 / bytesPerSecond);
                }
            }
            catch
            {
                return 0;
            }
        }
    }
}
