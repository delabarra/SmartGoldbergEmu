using System;
using System.Globalization;
using System.IO;
using System.Text;
using SmartGoldbergEmu.Constants;

namespace SmartGoldbergEmu.Helpers
{
    // Writes {loaderStem}.ini beside the ColdClient loader (upstream prefers stem.ini over ColdClientLoader.ini).
    public static class ColdClientLoaderIniWriter
    {
        public static string WriteLoaderIni(
            string loaderExePath,
            ulong appId,
            string gameExePath,
            string gameExeRunDir,
            string gameCommandLine,
            string steamClientDllPath,
            string steamClient64DllPath,
            bool forceInjectGameOverlayRenderer,
            string dllsToInjectFolder = null)
        {
            if (string.IsNullOrWhiteSpace(loaderExePath))
                throw new ArgumentException("Loader path is required.", nameof(loaderExePath));

            string loaderDir = Path.GetDirectoryName(loaderExePath);
            string stem = Path.GetFileNameWithoutExtension(loaderExePath);
            string iniPath = Path.Combine(loaderDir ?? string.Empty, stem + ".ini");
            string injectFolder = dllsToInjectFolder ?? string.Empty;

            var sb = new StringBuilder();
            // Upstream ColdClientLoader.ini uses # comments; warn before every ColdClient launch rewrite.
            sb.AppendLine("# Auto-managed by SmartGoldbergEmu. Do not edit — this file is overwritten on each ColdClient launch.");
            sb.AppendLine("[SteamClient]");
            sb.AppendLine("Exe=" + (gameExePath ?? string.Empty));
            sb.AppendLine("ExeRunDir=" + (gameExeRunDir ?? string.Empty));
            sb.AppendLine("ExeCommandLine=" + (gameCommandLine ?? string.Empty));
            sb.AppendLine("AppId=" + appId.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("SteamClientDll=" + (steamClientDllPath ?? string.Empty));
            sb.AppendLine("SteamClient64Dll=" + (steamClient64DllPath ?? string.Empty));
            sb.AppendLine();
            sb.AppendLine("[Injection]");
            sb.AppendLine("ForceInjectSteamClient=1");
            sb.AppendLine("ForceInjectGameOverlayRenderer=" + (forceInjectGameOverlayRenderer ? "1" : "0"));
            sb.AppendLine("DllsToInjectFolder=" + injectFolder);
            sb.AppendLine("IgnoreInjectionError=1");
            sb.AppendLine("IgnoreLoaderArchDifference=0");
            sb.AppendLine();
            sb.AppendLine("[Persistence]");
            sb.AppendLine("Mode=0");
            sb.AppendLine();
            sb.AppendLine("[Debug]");
            sb.AppendLine("ResumeByDebugger=0");

            Directory.CreateDirectory(loaderDir ?? ".");
            File.WriteAllText(iniPath, sb.ToString(), Encoding.UTF8);
            return iniPath;
        }

        // steamclient_extra_* mem-patch SteamStub at ColdClient startup. Empty when no stub or samples missing.
        public static string ResolveStubDrmInjectFolder(bool hasSteamStub)
        {
            if (!hasSteamStub)
                return string.Empty;

            string folder = PathConstants.GoldbergColdClientExtraDllsDirectory;
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                return string.Empty;

            bool hasSkipDll =
                File.Exists(Path.Combine(folder, GoldbergInstallLayout.ShippedSteamClientExtraDll32FileName))
                || File.Exists(Path.Combine(folder, GoldbergInstallLayout.ShippedSteamClientExtraDll64FileName));
            if (!hasSkipDll)
                return string.Empty;

            return Path.GetFullPath(folder);
        }

        public static string GetLoaderPathForGameArch(bool useX64) =>
            PathConstants.CombineGoldbergColdClientLoaderPath(useX64);
    }
}
