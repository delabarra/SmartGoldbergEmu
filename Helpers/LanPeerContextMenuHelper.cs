using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Extensions;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Helpers
{
    // Shared peer context menu for LAN Players rows and LAN Chat profile header.
    public static class LanPeerContextMenuHelper
    {
        public sealed class Options
        {
            // False on the chat form — Message… would only re-focus the same window.
            public bool IncludeMessage { get; set; } = true;

            public bool IncludeRemoveFromPlayersSeen { get; set; } = true;

            // Players list opens chat after unblock; chat form already has the window.
            public bool OpenChatOnAllowAgain { get; set; } = true;

            // Mute / favorites / seen-list changes — refresh the calling UI.
            public Action AfterLocalChange { get; set; }
        }

        public static void ClearItems(ContextMenuStrip menu)
        {
            if (menu == null)
                return;

            while (menu.Items.Count > 0)
            {
                ToolStripItem item = menu.Items[0];
                menu.Items.RemoveAt(0);
                item.Dispose();
            }
        }

        // Chat → play together → favorites → safety → copy.
        public static void PopulatePeerMenu(
            ContextMenuStrip menu,
            Form owner,
            string steamId,
            LanPresenceService presence,
            AppDataService appData,
            Options options = null)
        {
            if (menu == null || owner == null || presence == null || string.IsNullOrEmpty(steamId))
                return;

            if (options == null)
                options = new Options();

            bool present = presence.IsPeerPresent(steamId);
            bool chatBlocked = false;
            try
            {
                chatBlocked = ServiceLocator.LanChatService.IsChatBlocked(steamId);
            }
            catch
            {
            }

            bool addedTopGroup = false;
            if (chatBlocked)
            {
                menu.Items.Add(
                    "Allow chat again",
                    null,
                    (s, e) => OnAllowChatAgain(owner, steamId, options));
                addedTopGroup = true;
            }
            else if (options.IncludeMessage && present && presence.IsOnline && !presence.IsLocalBusy)
            {
                menu.Items.Add(
                    "Message…",
                    null,
                    (s, e) => LanChatUiHost.OpenChat(steamId, owner.Owner ?? owner));
                addedTopGroup = true;
            }

            bool addedPlayAction = false;
            if (present && presence.IsOnline)
            {
                LanPresencePeer peer;
                presence.TryGetPeer(steamId, out peer);

                ulong joinAppId = LanGameJoinService.GetJoinableAppId(peer);
                if (joinAppId != 0)
                {
                    GameConfig joinGame = ServiceLocator.LanGameJoinService.TryGetLibraryGame(joinAppId);
                    if (joinGame != null
                        && LanGameJoinService.SupportsSteamLobbyJoin(joinAppId, joinGame))
                    {
                        EnsureMenuSeparator(menu, ref addedTopGroup);
                        menu.Items.Add(
                            "Join game",
                            null,
                            (s, e) => OnJoinPeerGame(owner, presence, steamId));
                        addedPlayAction = true;
                    }
                }

                LanPresencePeer self = presence.GetLocalSelfSnapshot();
                if (self != null && self.IsPlaying && self.AppId != 0)
                {
                    GameConfig inviteGame = ServiceLocator.LanGameJoinService.TryGetLibraryGame(self.AppId);
                    if (LanGameJoinService.SupportsSteamLobbyJoin(self.AppId, inviteGame))
                    {
                        if (!addedPlayAction)
                            EnsureMenuSeparator(menu, ref addedTopGroup);
                        menu.Items.Add(
                            "Invite to game…",
                            null,
                            (s, e) => OnInvitePeerToGame(owner, presence, steamId));
                        addedPlayAction = true;
                    }
                }

                if (peer != null && peer.IsPlaying && peer.AppId != 0)
                {
                    if (!addedPlayAction)
                        EnsureMenuSeparator(menu, ref addedTopGroup);
                    menu.Items.Add(
                        "Open Steam store page…",
                        null,
                        (s, e) => OnOpenPeerSteamStore(owner, presence, steamId));
                    addedPlayAction = true;
                }
            }

            if (addedTopGroup || addedPlayAction)
                menu.Items.Add(new ToolStripSeparator());

            bool favorite = false;
            if (appData != null)
            {
                try
                {
                    favorite = appData.IsLanFavoriteSteamId(steamId);
                }
                catch
                {
                }
            }

            menu.Items.Add(
                favorite ? "Remove from favorites" : "Add to favorites",
                null,
                (s, e) => OnToggleFavorite(owner, appData, steamId, options));

            if (options.IncludeRemoveFromPlayersSeen && !present && appData != null)
            {
                bool remembered = false;
                try
                {
                    remembered = appData.IsLanSeenPeer(steamId);
                }
                catch
                {
                }

                if (remembered)
                {
                    menu.Items.Add(
                        "Remove from Players seen",
                        null,
                        (s, e) => OnRemoveFromPlayersSeen(owner, presence, steamId, options));
                }
            }

            menu.Items.Add(new ToolStripSeparator());

            bool muted = false;
            if (appData != null)
            {
                try
                {
                    muted = appData.IsLanMutedSteamId(steamId);
                }
                catch
                {
                }
            }

            menu.Items.Add(
                muted ? "Unmute" : "Mute",
                null,
                (s, e) => OnToggleMute(owner, appData, steamId, options));

            if (!chatBlocked)
            {
                menu.Items.Add(
                    "Block chat",
                    null,
                    (s, e) => OnBlockChat(owner, steamId));
            }

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(
                "Copy Steam ID",
                null,
                (s, e) => OnCopySteamId(owner, steamId));
        }

        private static void EnsureMenuSeparator(ContextMenuStrip menu, ref bool priorGroupAdded)
        {
            if (!priorGroupAdded)
                return;
            menu.Items.Add(new ToolStripSeparator());
            priorGroupAdded = false;
        }

        private static void OnJoinPeerGame(Form owner, LanPresenceService presence, string steamId)
        {
            if (owner == null || owner.IsDisposed || presence == null || string.IsNullOrEmpty(steamId))
                return;

            LanPresencePeer peer;
            if (!presence.TryGetPeer(steamId, out peer) || peer == null || !peer.IsPresent)
            {
                FormMessageBoxHelper.ShowIfAlive(
                    owner,
                    "That player is no longer available.",
                    "LAN Players",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            Form dialogOwner = owner.Owner ?? owner;
            CancellationToken token = ServiceLocator.ApplicationLifetimeToken;
            _ = JoinPeerGameUiAsync(owner, peer, dialogOwner, token)
                .ForgetFaults(Program.LogService, nameof(OnJoinPeerGame));
        }

        private static async Task JoinPeerGameUiAsync(
            Form owner,
            LanPresencePeer peer,
            Form dialogOwner,
            CancellationToken token)
        {
            ValidationResult result;
            try
            {
                result = await ServiceLocator.LanGameJoinService
                    .JoinPeerGameAsync(peer, dialogOwner, token)
                    .ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning("LAN join failed: " + ex.Message);
                if (owner != null && !owner.IsDisposed && !owner.Disposing)
                {
                    FormMessageBoxHelper.ShowIfAlive(
                        owner,
                        "Could not join: " + ex.Message,
                        "LAN Players",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return;
            }

            if (result != null && !result.IsValid
                && owner != null && !owner.IsDisposed && !owner.Disposing
                && !string.Equals(
                    result.ErrorMessage,
                    LanGameJoinService.RelaunchCancelledMessage,
                    StringComparison.Ordinal))
            {
                FormMessageBoxHelper.ShowIfAlive(
                    owner,
                    result.ErrorMessage ?? "Could not join.",
                    "LAN Players",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private static void OnInvitePeerToGame(Form owner, LanPresenceService presence, string steamId)
        {
            if (owner == null || owner.IsDisposed || presence == null || string.IsNullOrEmpty(steamId))
                return;

            LanPresencePeer peer;
            if (!presence.TryGetPeer(steamId, out peer) || peer == null || !peer.IsPresent)
            {
                FormMessageBoxHelper.ShowIfAlive(
                    owner,
                    "That player is no longer available.",
                    "LAN Players",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            CancellationToken token = ServiceLocator.ApplicationLifetimeToken;
            _ = InvitePeerToGameUiAsync(owner, presence, peer, token)
                .ForgetFaults(Program.LogService, nameof(OnInvitePeerToGame));
        }

        private static async Task InvitePeerToGameUiAsync(
            Form owner,
            LanPresenceService presence,
            LanPresencePeer peer,
            CancellationToken token)
        {
            ValidationResult result;
            try
            {
                owner.UseWaitCursor = true;
                result = await ServiceLocator.LanGameJoinService
                    .InvitePeerToLocalGameAsync(peer, token)
                    .ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning("LAN game invite failed: " + ex.Message);
                result = ValidationResult.Failure(ex.Message);
            }
            finally
            {
                if (owner != null && !owner.IsDisposed && !owner.Disposing)
                    owner.UseWaitCursor = false;
            }

            if (owner == null || owner.IsDisposed || owner.Disposing)
                return;

            if (result != null && result.IsValid)
            {
                LanPresencePeer self = presence.GetLocalSelfSnapshot();
                ulong appId = self != null ? self.AppId : 0;
                string gameName = self != null ? self.GameName : null;
                ServiceLocator.LanChatService.RecordGameInviteSent(peer.SteamId, appId, gameName);
                LanChatUiHost.OpenChat(peer.SteamId, owner.Owner ?? owner, userInitiated: true);
                return;
            }

            FormMessageBoxHelper.ShowIfAlive(
                owner,
                result != null ? (result.ErrorMessage ?? "Could not send invite.") : "Could not send invite.",
                "LAN Players",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        private static void OnOpenPeerSteamStore(Form owner, LanPresenceService presence, string steamId)
        {
            if (owner == null || presence == null || string.IsNullOrEmpty(steamId))
                return;

            LanPresencePeer peer;
            if (!presence.TryGetPeer(steamId, out peer)
                || peer == null
                || !peer.IsPlaying
                || peer.AppId == 0)
            {
                FormMessageBoxHelper.ShowIfAlive(
                    owner,
                    "That player is not in a game right now.",
                    "LAN Players",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            string url = string.Format(
                CultureInfo.InvariantCulture,
                ApplicationConstants.SteamStoreAppUrlFormat,
                peer.AppId);
            if (!PathValidationHelper.IsSafeUrl(url))
            {
                FormMessageBoxHelper.ShowIfAlive(
                    owner,
                    "Invalid URL format detected.",
                    "Invalid URL",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            try
            {
                Process.Start(url);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError("Failed to open Steam store page", ex);
                FormMessageBoxHelper.ShowIfAlive(
                    owner,
                    "Failed to open store page.",
                    "Could Not Open Link",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static void OnCopySteamId(Form owner, string steamId)
        {
            if (string.IsNullOrEmpty(steamId))
                return;

            try
            {
                Clipboard.SetText(steamId, TextDataFormat.UnicodeText);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning("LAN: failed to copy Steam ID: " + ex.Message);
                FormMessageBoxHelper.ShowIfAlive(
                    owner,
                    "Could not copy Steam ID to the clipboard.",
                    "LAN Players",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private static void OnBlockChat(Form owner, string steamId)
        {
            if (string.IsNullOrEmpty(steamId))
                return;

            try
            {
                ValidationResult result = ServiceLocator.LanChatService.SetChatBlocked(steamId, true);
                if (!result.IsValid)
                {
                    FormMessageBoxHelper.ShowIfAlive(
                        owner,
                        result.ErrorMessage ?? "Could not block chat.",
                        "LAN Chat",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning("LAN chat block failed: " + ex.Message);
            }
        }

        private static void OnAllowChatAgain(Form owner, string steamId, Options options)
        {
            if (string.IsNullOrEmpty(steamId))
                return;

            try
            {
                ValidationResult result = ServiceLocator.LanChatService.SetChatBlocked(steamId, false);
                if (!result.IsValid)
                {
                    FormMessageBoxHelper.ShowIfAlive(
                        owner,
                        result.ErrorMessage ?? "Could not unblock chat.",
                        "LAN Chat",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning("LAN chat unblock failed: " + ex.Message);
                return;
            }

            if (options != null && options.OpenChatOnAllowAgain && owner != null)
                LanChatUiHost.OpenChat(steamId, owner.Owner ?? owner);
        }

        private static void OnRemoveFromPlayersSeen(
            Form owner,
            LanPresenceService presence,
            string steamId,
            Options options)
        {
            if (presence == null || string.IsNullOrEmpty(steamId))
                return;

            try
            {
                ValidationResult result = presence.ForgetSeenPeer(steamId);
                if (!result.IsValid)
                {
                    FormMessageBoxHelper.ShowIfAlive(
                        owner,
                        result.ErrorMessage ?? "Could not remove player from Players seen.",
                        "LAN Players",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning("LAN: failed to remove seen peer: " + ex.Message);
                return;
            }

            if (options != null && options.AfterLocalChange != null)
                options.AfterLocalChange();
        }

        private static void OnToggleFavorite(
            Form owner,
            AppDataService appData,
            string steamId,
            Options options)
        {
            if (appData == null || string.IsNullOrEmpty(steamId))
                return;

            try
            {
                bool currentlyFavorite = appData.IsLanFavoriteSteamId(steamId);
                ValidationResult result = appData.SetLanFavoriteSteamId(steamId, !currentlyFavorite);
                if (!result.IsValid)
                {
                    FormMessageBoxHelper.ShowIfAlive(
                        owner,
                        result.ErrorMessage ?? "Could not update favorites.",
                        "LAN Players",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning("LAN: failed to update favorite: " + ex.Message);
                return;
            }

            if (options != null && options.AfterLocalChange != null)
                options.AfterLocalChange();
        }

        private static void OnToggleMute(
            Form owner,
            AppDataService appData,
            string steamId,
            Options options)
        {
            if (appData == null || string.IsNullOrEmpty(steamId))
                return;

            try
            {
                bool currentlyMuted = appData.IsLanMutedSteamId(steamId);
                ValidationResult result = appData.SetLanMutedSteamId(steamId, !currentlyMuted);
                if (!result.IsValid)
                {
                    FormMessageBoxHelper.ShowIfAlive(
                        owner,
                        result.ErrorMessage ?? "Could not update mute list.",
                        "LAN Players",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning("LAN: failed to update mute: " + ex.Message);
                return;
            }

            if (options != null && options.AfterLocalChange != null)
                options.AfterLocalChange();
        }
    }
}
