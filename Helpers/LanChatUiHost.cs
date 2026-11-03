using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SmartGoldbergEmu.Forms;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Helpers
{
    // Owns open LanChatForm instances; auto-opens quietly for invites and flashes for attention.
    public static class LanChatUiHost
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, LanChatForm> OpenForms =
            new Dictionary<string, LanChatForm>(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> LastRemoteMessageCounts =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private static Form _owner;
        private static bool _hooked;

        public static void Attach(Form owner)
        {
            if (owner == null)
                return;

            lock (Gate)
            {
                _owner = owner;
                if (_hooked)
                    return;
                ServiceLocator.LanChatService.EnsureHooked();
                ServiceLocator.LanChatService.SessionUpdated += OnSessionUpdated;
                _hooked = true;
            }
        }

        public static void Detach(Form owner)
        {
            lock (Gate)
            {
                if (!ReferenceEquals(_owner, owner))
                    return;
                _owner = null;
                if (_hooked)
                {
                    try
                    {
                        ServiceLocator.LanChatService.SessionUpdated -= OnSessionUpdated;
                    }
                    catch (ObjectDisposedException)
                    {
                    }

                    _hooked = false;
                }

                // Snapshot first — Close → NotifyClosed mutates OpenForms during enumeration.
                LanChatForm[] open = new LanChatForm[OpenForms.Count];
                OpenForms.Values.CopyTo(open, 0);
                OpenForms.Clear();
                LastRemoteMessageCounts.Clear();

                foreach (LanChatForm form in open)
                {
                    try
                    {
                        if (form != null && !form.IsDisposed)
                            form.Close();
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }
            }
        }

        public static void OpenChat(string peerSteamId, IWin32Window owner = null)
        {
            OpenChat(peerSteamId, owner, userInitiated: true);
        }

        public static void OpenChat(string peerSteamId, IWin32Window owner, bool userInitiated)
        {
            if (string.IsNullOrWhiteSpace(peerSteamId))
                return;
            peerSteamId = peerSteamId.Trim();

            Form parent = owner as Form;
            if (parent == null)
            {
                lock (Gate)
                    parent = _owner;
            }

            if (parent != null && parent.InvokeRequired)
            {
                try
                {
                    parent.BeginInvoke(new Action(() => OpenChatCore(peerSteamId, parent, userInitiated)));
                }
                catch (ObjectDisposedException)
                {
                }

                return;
            }

            OpenChatCore(peerSteamId, parent, userInitiated);
        }

        public static void NotifyClosed(LanChatForm form)
        {
            if (form == null || string.IsNullOrEmpty(form.PeerSteamId))
                return;
            lock (Gate)
            {
                LanChatForm existing;
                if (OpenForms.TryGetValue(form.PeerSteamId, out existing)
                    && ReferenceEquals(existing, form))
                    OpenForms.Remove(form.PeerSteamId);
            }
        }

        private static void OnSessionUpdated(object sender, LanChatSessionEventArgs e)
        {
            if (e == null || e.Session == null || string.IsNullOrEmpty(e.Session.PeerSteamId))
                return;

            Form parent;
            lock (Gate)
                parent = _owner;

            if (parent == null || parent.IsDisposed)
                return;

            bool invite = e.Session.Status == LanChatSessionStatus.IncomingPending;
            bool remoteGrew = RemoteMessageCountGrew(e.Session);

            if (!invite && !remoteGrew)
                return;

            if (parent.InvokeRequired)
            {
                try
                {
                    parent.BeginInvoke(new Action(() => HandleIncomingAttention(e.Session, parent, invite)));
                }
                catch (ObjectDisposedException)
                {
                }

                return;
            }

            HandleIncomingAttention(e.Session, parent, invite);
        }

        private static bool RemoteMessageCountGrew(LanChatSessionSnapshot session)
        {
            int remoteCount = CountRemoteMessages(session);
            lock (Gate)
            {
                int previous;
                LastRemoteMessageCounts.TryGetValue(session.PeerSteamId, out previous);
                LastRemoteMessageCounts[session.PeerSteamId] = remoteCount;
                return remoteCount > previous;
            }
        }

        private static int CountRemoteMessages(LanChatSessionSnapshot session)
        {
            if (session.Messages == null)
                return 0;
            int count = 0;
            for (int i = 0; i < session.Messages.Count; i++)
            {
                LanChatMessage m = session.Messages[i];
                if (m != null && !m.IsLocal)
                    count++;
            }

            return count;
        }

        private static void HandleIncomingAttention(LanChatSessionSnapshot session, Form parent, bool invite)
        {
            LanChatForm existing;
            lock (Gate)
            {
                OpenForms.TryGetValue(session.PeerSteamId, out existing);
            }

            if (existing != null && !existing.IsDisposed)
            {
                // Open chat still flashes; muted peers do not steal attention when closed.
                if (!IsPeerMuted(session.PeerSteamId))
                    existing.NotifyAttention();
                return;
            }

            if (IsPeerMuted(session.PeerSteamId))
                return;

            // Invite or later message with chat closed: open quietly (no focus steal).
            // Flashing MainForm alone is invisible when it is already foreground.
            OpenChatCore(session.PeerSteamId, parent, userInitiated: false);
        }

        private static bool IsPeerMuted(string peerSteamId)
        {
            if (string.IsNullOrWhiteSpace(peerSteamId))
                return false;
            try
            {
                return ServiceLocator.AppDataService.IsLanMutedSteamId(peerSteamId);
            }
            catch
            {
                return false;
            }
        }

        private static void OpenChatCore(string peerSteamId, Form parent, bool userInitiated)
        {
            if (string.IsNullOrEmpty(peerSteamId))
                return;

            LanChatForm form;
            lock (Gate)
            {
                if (OpenForms.TryGetValue(peerSteamId, out form)
                    && form != null
                    && !form.IsDisposed)
                {
                    try
                    {
                        if (userInitiated)
                        {
                            if (form.WindowState == FormWindowState.Minimized)
                                form.WindowState = FormWindowState.Normal;
                            form.SetOpenWithoutActivation(false);
                            form.FocusComposer();
                        }
                        else
                        {
                            form.NotifyAttention();
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                    catch (InvalidOperationException)
                    {
                    }

                    return;
                }

                form = new LanChatForm(peerSteamId);
                form.SetOpenWithoutActivation(!userInitiated);
                OpenForms[peerSteamId] = form;
            }

            try
            {
                // No Owner — owned forms never get a taskbar button; chat needs its own.
                if (parent != null && !parent.IsDisposed)
                    CenterOnParent(form, parent);
                form.Show();

                if (userInitiated)
                {
                    form.SetOpenWithoutActivation(false);
                    form.FocusComposer();
                }
                else
                {
                    form.NotifyAttention();
                }
            }
            catch (ObjectDisposedException)
            {
                NotifyClosed(form);
            }
            catch (Exception)
            {
                NotifyClosed(form);
                try
                {
                    if (form != null && !form.IsDisposed)
                        form.Dispose();
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }

        private static void CenterOnParent(Form form, Form parent)
        {
            if (form == null || parent == null || parent.IsDisposed)
                return;

            try
            {
                form.StartPosition = FormStartPosition.Manual;
                int x = parent.Location.X + Math.Max(0, (parent.Width - form.Width) / 2);
                int y = parent.Location.Y + Math.Max(0, (parent.Height - form.Height) / 2);
                form.Location = new System.Drawing.Point(x, y);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }
    }
}
