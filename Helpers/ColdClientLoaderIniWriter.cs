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
            bool forceInjectGameOverlayRenderer)
        {
            if (string.IsNullOrWhiteSpace(loaderExePath))
                throw new ArgumentException("Loader path is required.", nameof(loaderExePath));

            string loaderDir = Path.GetDirectoryName(loaderExePath);
            string stem = Path.GetFileNameWithoutExtension(loaderExePath);
            string iniPath = Path.Combine(loaderDir ?? string.Empty, stem + ".ini");

            var sb = new StringBuilder();
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
            sb.AppendLine("DllsToInjectFolder=");
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

        public static string GetLoaderPathForGameArch(bool useX64) =>
            PathConstants.CombineGoldbergColdClientLoaderPath(useX64);
    }
}
