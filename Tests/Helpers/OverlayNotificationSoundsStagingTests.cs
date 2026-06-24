using System.IO;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Tests.TestSupport;
using Xunit;

namespace SmartGoldbergEmu.Tests.Helpers
{
    public sealed class OverlayNotificationSoundsStagingTests
    {
        [Fact]
        public void TryStageIntoGameSoundsFolder_copies_overlay_notification_wavs()
        {
            string source = TestFileHelper.CreateTempDirectory("sge-sounds-src-");
            string dest = TestFileHelper.CreateTempDirectory("sge-sounds-dst-");
            try
            {
                File.WriteAllBytes(
                    Path.Combine(source, PathConstants.SteamClientUiAchievementNotificationWav),
                    new byte[] { 1, 2, 3 });
                File.WriteAllBytes(
                    Path.Combine(source, PathConstants.SteamClientUiFriendNotificationWav),
                    new byte[] { 4, 5, 6 });
                File.WriteAllBytes(Path.Combine(source, "desktop_toast_default.wav"), new byte[] { 7 });

                Assert.True(OverlayNotificationSoundsStaging.TryStageIntoGameSoundsFolder(source, dest, out int copied));
                Assert.Equal(2, copied);

                Assert.True(File.Exists(Path.Combine(dest, PathConstants.SteamClientUiAchievementNotificationWav)));
                Assert.True(File.Exists(Path.Combine(dest, PathConstants.SteamClientUiFriendNotificationWav)));
                Assert.False(File.Exists(Path.Combine(dest, "desktop_toast_default.wav")));
            }
            finally
            {
                try { Directory.Delete(source, recursive: true); } catch { }
                try { Directory.Delete(dest, recursive: true); } catch { }
            }
        }
    }
}
