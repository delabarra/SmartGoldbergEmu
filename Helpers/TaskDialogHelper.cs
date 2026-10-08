using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SmartGoldbergEmu.Constants;

namespace SmartGoldbergEmu.Helpers
{
    public sealed class TaskDialogCustomButton
    {
        public TaskDialogCustomButton(int id, string text)
        {
            Id = id;
            Text = text ?? string.Empty;
        }

        public int Id { get; }
        public string Text { get; }
    }

    public static class TaskDialogHelper
    {
        public const int IdOk = 1;
        public const int IdCancel = 2;
        public const int IdRetry = 4;
        public const int IdYes = 6;
        public const int IdNo = 7;
        public const int IdClose = 8;

        private const int TdcbfOkButton = 0x0001;
        private const int TdcbfYesButton = 0x0002;
        private const int TdcbfNoButton = 0x0004;
        private const int TdcbfCancelButton = 0x0008;
        private const int TdcbfRetryButton = 0x0010;
        private const int TdcbfCloseButton = 0x0020;

        private const int TdfEnableHyperlinks = 0x0001;
        private const int TdfAllowDialogCancellation = 0x0008;
        private const int TdfPositionRelativeToWindow = 0x1000;
        private const int TdfSizeToContent = 0x01000000;

        private const uint TdnHyperlinkClicked = 3;

        // MAKEINTRESOURCEW(-n) as PWSTR for stock Task Dialog icons (no TDF_USE_HICON_MAIN).
        private static readonly IntPtr TdWarningIcon = new IntPtr(-1);
        private static readonly IntPtr TdErrorIcon = new IntPtr(-2);
        private static readonly IntPtr TdInformationIcon = new IntPtr(-3);
        private static readonly IntPtr TdShieldIcon = new IntPtr(-4);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int TaskDialogCallback(
            IntPtr hwnd,
            uint msg,
            IntPtr wParam,
            IntPtr lParam,
            IntPtr lpRefData);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct TaskDialogButtonNative
        {
            public int nButtonID;
            public IntPtr pszButtonText;
        }

        // Default pack so IntPtr fields align like native TASKDIALOGCONFIG on x64.
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct TaskDialogConfig
        {
            public uint cbSize;
            public IntPtr hwndParent;
            public IntPtr hInstance;
            public int dwFlags;
            public int dwCommonButtons;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pszWindowTitle;
            public IntPtr mainIcon;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pszMainInstruction;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pszContent;
            public uint cButtons;
            public IntPtr pButtons;
            public int nDefaultButton;
            public uint cRadioButtons;
            public IntPtr pRadioButtons;
            public int nDefaultRadioButton;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pszVerificationText;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pszExpandedInformation;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pszExpandedControlText;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pszCollapsedControlText;
            public IntPtr footerIcon;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pszFooter;
            public TaskDialogCallback pfCallback;
            public IntPtr lpCallbackData;
            public uint cxWidth;
        }

        [DllImport("comctl32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int TaskDialogIndirect(
            [In] ref TaskDialogConfig pTaskConfig,
            out int pnButton,
            out int pnRadioButton,
            out bool pfVerificationFlagChecked);

        public static bool IsOwnerDead(IWin32Window owner)
        {
            return owner is Control c && (c.IsDisposed || c.Disposing);
        }

        public static DialogResult Show(
            IWin32Window owner,
            string content,
            string mainInstruction,
            MessageBoxButtons buttons,
            MessageBoxIcon icon)
        {
            return Show(owner, content, mainInstruction, buttons, icon, MessageBoxDefaultButton.Button1, null, false);
        }

        public static DialogResult Show(
            IWin32Window owner,
            string content,
            string mainInstruction,
            MessageBoxButtons buttons,
            MessageBoxIcon icon,
            MessageBoxDefaultButton defaultButton)
        {
            return Show(owner, content, mainInstruction, buttons, icon, defaultButton, null, false);
        }

        public static DialogResult Show(
            IWin32Window owner,
            string content,
            string mainInstruction,
            MessageBoxButtons buttons,
            MessageBoxIcon icon,
            MessageBoxDefaultButton defaultButton,
            string footer,
            bool enableHyperlinks)
        {
            if (IsOwnerDead(owner))
                return DialogResult.Cancel;

            MapCommonButtons(buttons, defaultButton, out int commonButtons, out int defaultButtonId);
            int flags = TdfAllowDialogCancellation | TdfPositionRelativeToWindow | TdfSizeToContent;
            if (enableHyperlinks)
                flags |= TdfEnableHyperlinks;

            int buttonId;
            if (TryShowNative(
                owner,
                mainInstruction,
                content,
                MapIcon(icon),
                flags,
                commonButtons,
                defaultButtonId,
                null,
                footer,
                null,
                enableHyperlinks,
                out buttonId,
                out _))
            {
                return MapCommonButtonIdToDialogResult(buttonId, buttons);
            }

            return MessageBox.Show(
                owner,
                content ?? string.Empty,
                mainInstruction ?? ApplicationConstants.WindowTitle,
                buttons,
                icon,
                defaultButton);
        }

        // Returns the clicked custom button id, or IdCancel if the owner is dead / dialog cancelled.
        public static int ShowCustom(
            IWin32Window owner,
            string content,
            string mainInstruction,
            MessageBoxIcon icon,
            int defaultButtonId,
            TaskDialogCustomButton[] customButtons,
            string footer = null,
            bool enableHyperlinks = false)
        {
            return ShowCustom(
                owner,
                content,
                mainInstruction,
                icon,
                defaultButtonId,
                customButtons,
                footer,
                enableHyperlinks,
                null,
                out _);
        }

        // Custom buttons plus optional verification checkbox (Task Dialog footer).
        public static int ShowCustom(
            IWin32Window owner,
            string content,
            string mainInstruction,
            MessageBoxIcon icon,
            int defaultButtonId,
            TaskDialogCustomButton[] customButtons,
            string footer,
            bool enableHyperlinks,
            string verificationText,
            out bool verificationChecked)
        {
            verificationChecked = false;
            if (IsOwnerDead(owner))
                return IdCancel;

            if (customButtons == null || customButtons.Length == 0)
                throw new ArgumentException("At least one custom button is required.", nameof(customButtons));

            int flags = TdfAllowDialogCancellation | TdfPositionRelativeToWindow | TdfSizeToContent;
            if (enableHyperlinks)
                flags |= TdfEnableHyperlinks;

            int buttonId;
            if (TryShowNative(
                owner,
                mainInstruction,
                content,
                MapIcon(icon),
                flags,
                0,
                defaultButtonId,
                customButtons,
                footer,
                verificationText,
                enableHyperlinks,
                out buttonId,
                out verificationChecked))
            {
                return buttonId;
            }

            // MessageBox fallback: first custom button maps to Yes/OK, second to No.
            string fallbackBody = BuildFallbackCustomBody(content, customButtons);
            if (!string.IsNullOrEmpty(verificationText))
                fallbackBody = fallbackBody + "\n\n(" + verificationText + " is not available in fallback mode.)";

            var fallbackButtons = customButtons.Length >= 2
                ? MessageBoxButtons.YesNo
                : MessageBoxButtons.OK;
            DialogResult fallback = MessageBox.Show(
                owner,
                fallbackBody,
                mainInstruction ?? ApplicationConstants.WindowTitle,
                fallbackButtons,
                icon,
                MessageBoxDefaultButton.Button2);
            if (fallback == DialogResult.Yes || fallback == DialogResult.OK)
                return customButtons[0].Id;
            if (fallback == DialogResult.No && customButtons.Length >= 2)
                return customButtons[1].Id;
            return IdCancel;
        }

        private static string BuildFallbackCustomBody(string content, TaskDialogCustomButton[] customButtons)
        {
            string body = content ?? string.Empty;
            if (customButtons == null || customButtons.Length == 0)
                return body;

            var lines = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(body))
            {
                lines.Append(body);
                lines.Append("\n\n");
            }

            for (int i = 0; i < customButtons.Length; i++)
            {
                if (i == 0)
                    lines.Append("Yes — ").Append(customButtons[i].Text);
                else if (i == 1)
                    lines.Append("\nNo — ").Append(customButtons[i].Text);
                else
                    lines.Append("\n").Append(customButtons[i].Text);
            }

            return lines.ToString();
        }

        private static bool TryShowNative(
            IWin32Window owner,
            string mainInstruction,
            string content,
            IntPtr mainIcon,
            int flags,
            int commonButtons,
            int defaultButtonId,
            TaskDialogCustomButton[] customButtons,
            string footer,
            string verificationText,
            bool enableHyperlinks,
            out int buttonId,
            out bool verificationChecked)
        {
            buttonId = IdCancel;
            verificationChecked = false;
            IntPtr buttonsPtr = IntPtr.Zero;
            IntPtr[] buttonTextPtrs = null;
            TaskDialogCallback callback = null;

            try
            {
                var config = new TaskDialogConfig
                {
                    cbSize = (uint)Marshal.SizeOf(typeof(TaskDialogConfig)),
                    hwndParent = owner != null ? owner.Handle : IntPtr.Zero,
                    hInstance = IntPtr.Zero,
                    dwFlags = flags,
                    dwCommonButtons = commonButtons,
                    pszWindowTitle = ApplicationConstants.WindowTitle,
                    mainIcon = mainIcon,
                    pszMainInstruction = string.IsNullOrEmpty(mainInstruction) ? null : mainInstruction,
                    pszContent = string.IsNullOrEmpty(content) ? null : content,
                    nDefaultButton = defaultButtonId,
                    pszVerificationText = string.IsNullOrEmpty(verificationText) ? null : verificationText,
                    pszFooter = string.IsNullOrEmpty(footer) ? null : footer,
                    cxWidth = 0
                };

                if (customButtons != null && customButtons.Length > 0)
                {
                    buttonTextPtrs = new IntPtr[customButtons.Length];
                    int stride = Marshal.SizeOf(typeof(TaskDialogButtonNative));
                    buttonsPtr = Marshal.AllocHGlobal(stride * customButtons.Length);
                    for (int i = 0; i < customButtons.Length; i++)
                    {
                        buttonTextPtrs[i] = Marshal.StringToCoTaskMemUni(customButtons[i].Text ?? string.Empty);
                        var native = new TaskDialogButtonNative
                        {
                            nButtonID = customButtons[i].Id,
                            pszButtonText = buttonTextPtrs[i]
                        };
                        Marshal.StructureToPtr(native, IntPtr.Add(buttonsPtr, i * stride), false);
                    }

                    config.cButtons = (uint)customButtons.Length;
                    config.pButtons = buttonsPtr;
                }

                if (enableHyperlinks)
                {
                    // Keep the delegate rooted for the duration of the native call.
                    callback = OnTaskDialogCallback;
                    config.pfCallback = callback;
                }

                int hr = TaskDialogIndirect(ref config, out buttonId, out _, out verificationChecked);
                return hr >= 0;
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning("TaskDialogIndirect failed; falling back to MessageBox: " + ex.Message);
                return false;
            }
            finally
            {
                if (buttonTextPtrs != null)
                {
                    for (int i = 0; i < buttonTextPtrs.Length; i++)
                    {
                        if (buttonTextPtrs[i] != IntPtr.Zero)
                            Marshal.FreeCoTaskMem(buttonTextPtrs[i]);
                    }
                }

                if (buttonsPtr != IntPtr.Zero)
                    Marshal.FreeHGlobal(buttonsPtr);

                GC.KeepAlive(callback);
            }
        }

        private static int OnTaskDialogCallback(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, IntPtr lpRefData)
        {
            if (msg != TdnHyperlinkClicked || lParam == IntPtr.Zero)
                return 0;

            string url = Marshal.PtrToStringUni(lParam);
            if (!PathValidationHelper.IsSafeUrl(url))
                return 0;

            try
            {
                Process.Start(url);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning("Failed to open TaskDialog hyperlink: " + ex.Message);
            }

            return 0;
        }

        private static IntPtr MapIcon(MessageBoxIcon icon)
        {
            switch (icon)
            {
                case MessageBoxIcon.Error:
                    return TdErrorIcon;
                case MessageBoxIcon.Warning:
                    return TdWarningIcon;
                case MessageBoxIcon.Information:
                    return TdInformationIcon;
                case MessageBoxIcon.Question:
                    return TdInformationIcon;
                default:
                    return IntPtr.Zero;
            }
        }

        private static void MapCommonButtons(
            MessageBoxButtons buttons,
            MessageBoxDefaultButton defaultButton,
            out int commonButtons,
            out int defaultButtonId)
        {
            switch (buttons)
            {
                case MessageBoxButtons.OK:
                    commonButtons = TdcbfOkButton;
                    defaultButtonId = IdOk;
                    break;
                case MessageBoxButtons.OKCancel:
                    commonButtons = TdcbfOkButton | TdcbfCancelButton;
                    defaultButtonId = defaultButton == MessageBoxDefaultButton.Button2 ? IdCancel : IdOk;
                    break;
                case MessageBoxButtons.YesNo:
                    commonButtons = TdcbfYesButton | TdcbfNoButton;
                    defaultButtonId = defaultButton == MessageBoxDefaultButton.Button2 ? IdNo : IdYes;
                    break;
                case MessageBoxButtons.YesNoCancel:
                    commonButtons = TdcbfYesButton | TdcbfNoButton | TdcbfCancelButton;
                    if (defaultButton == MessageBoxDefaultButton.Button2)
                        defaultButtonId = IdNo;
                    else if (defaultButton == MessageBoxDefaultButton.Button3)
                        defaultButtonId = IdCancel;
                    else
                        defaultButtonId = IdYes;
                    break;
                case MessageBoxButtons.RetryCancel:
                    commonButtons = TdcbfRetryButton | TdcbfCancelButton;
                    defaultButtonId = defaultButton == MessageBoxDefaultButton.Button2 ? IdCancel : IdRetry;
                    break;
                case MessageBoxButtons.AbortRetryIgnore:
                    // Task Dialog has no Abort/Ignore common buttons; Retry + Cancel is the closest safe set.
                    commonButtons = TdcbfRetryButton | TdcbfCancelButton;
                    defaultButtonId = defaultButton == MessageBoxDefaultButton.Button2 ? IdCancel : IdRetry;
                    break;
                default:
                    commonButtons = TdcbfOkButton;
                    defaultButtonId = IdOk;
                    break;
            }
        }

        private static DialogResult MapCommonButtonIdToDialogResult(int buttonId, MessageBoxButtons buttons)
        {
            switch (buttonId)
            {
                case IdOk:
                    return DialogResult.OK;
                case IdCancel:
                    return DialogResult.Cancel;
                case IdRetry:
                    return DialogResult.Retry;
                case IdYes:
                    return DialogResult.Yes;
                case IdNo:
                    return DialogResult.No;
                case IdClose:
                    return DialogResult.Cancel;
                default:
                    if (buttons == MessageBoxButtons.OK)
                        return DialogResult.OK;
                    return DialogResult.Cancel;
            }
        }
    }
}
