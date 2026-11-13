using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace SmartGoldbergEmu.Helpers
{
    // PE Authenticode via WinVerifyTrust (no UI, no revocation network).
    public static class AuthenticodeHelper
    {
        private static readonly Guid ActionGenericVerifyV2 =
            new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

        private const uint WtdUiNone = 2;
        private const uint WtdRevokeNone = 0;
        private const uint WtdChoiceFile = 1;
        private const uint WtdCacheOnlyUrlRetrieval = 0x00001000;

        // True when WinVerifyTrust accepts the Authenticode signature on the file.
        public static bool IsTrustedSignedFile(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            IntPtr filePathPtr = IntPtr.Zero;
            IntPtr fileInfoPtr = IntPtr.Zero;
            try
            {
                filePathPtr = Marshal.StringToCoTaskMemUni(path);

                var fileInfo = new WinTrustFileInfo
                {
                    cbStruct = (uint)Marshal.SizeOf(typeof(WinTrustFileInfo)),
                    pcwszFilePath = filePathPtr,
                    hFile = IntPtr.Zero,
                    pgKnownSubject = IntPtr.Zero
                };
                fileInfoPtr = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(WinTrustFileInfo)));
                Marshal.StructureToPtr(fileInfo, fileInfoPtr, false);

                var data = new WinTrustData
                {
                    cbStruct = (uint)Marshal.SizeOf(typeof(WinTrustData)),
                    pPolicyCallbackData = IntPtr.Zero,
                    pSIPClientData = IntPtr.Zero,
                    dwUIChoice = WtdUiNone,
                    fdwRevocationChecks = WtdRevokeNone,
                    dwUnionChoice = WtdChoiceFile,
                    pFile = fileInfoPtr,
                    dwStateAction = 0,
                    hWVTStateData = IntPtr.Zero,
                    pwszURLReference = IntPtr.Zero,
                    dwProvFlags = WtdCacheOnlyUrlRetrieval,
                    dwUIContext = 0,
                    pSignatureSettings = IntPtr.Zero
                };

                Guid action = ActionGenericVerifyV2;
                int status = WinVerifyTrust(InvalidHandleValue, ref action, ref data);
                return status == 0;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (fileInfoPtr != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(fileInfoPtr);
                if (filePathPtr != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(filePathPtr);
            }
        }

        // Signer certificate subject (e.g. CN=Valve Corp., O=Valve Corp., …), or null.
        public static bool TryGetSignerSubject(string path, out string subject)
        {
            subject = null;
            if (string.IsNullOrEmpty(path))
                return false;

            try
            {
                X509Certificate cert = X509Certificate.CreateFromSignedFile(path);
                using (var cert2 = new X509Certificate2(cert))
                {
                    subject = cert2.Subject;
                    return !string.IsNullOrEmpty(subject);
                }
            }
            catch
            {
                return false;
            }
        }

        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

        [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = false, CharSet = CharSet.Unicode)]
        private static extern int WinVerifyTrust(
            [In] IntPtr hwnd,
            [In] ref Guid pgActionID,
            [In] ref WinTrustData pWVTData);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WinTrustFileInfo
        {
            public uint cbStruct;
            public IntPtr pcwszFilePath;
            public IntPtr hFile;
            public IntPtr pgKnownSubject;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WinTrustData
        {
            public uint cbStruct;
            public IntPtr pPolicyCallbackData;
            public IntPtr pSIPClientData;
            public uint dwUIChoice;
            public uint fdwRevocationChecks;
            public uint dwUnionChoice;
            public IntPtr pFile;
            public uint dwStateAction;
            public IntPtr hWVTStateData;
            public IntPtr pwszURLReference;
            public uint dwProvFlags;
            public uint dwUIContext;
            public IntPtr pSignatureSettings;
        }
    }
}
