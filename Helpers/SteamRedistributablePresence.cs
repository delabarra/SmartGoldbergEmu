using System;
using System.IO;
using Microsoft.Win32;
using SmartGoldbergEmu.Constants;

namespace SmartGoldbergEmu.Helpers
{
    // Lightweight "is it really on this PC?" probes for common Steam redistributables.
    // Used when HasRun says installed so uninstalls still surface as missing.
    public static class SteamRedistributablePresence
    {
        private enum CpuArch
        {
            // Depot-level checks: both arches on 64-bit OS, x86 only on 32-bit OS.
            AllRequired,
            X86,
            X64
        }

        // true = treat as installed (or we cannot check). false = known missing.
        public static bool IsPresentOrUnknown(
            uint? depotId,
            string hasRunKey,
            string processPath,
            string processEntryName = null)
        {
            bool present;
            if (!TryProbePresence(depotId, hasRunKey, processPath, processEntryName, out present))
                return true;
            return present;
        }

        // true only when a known probe family is actually on the machine (not PhysX/XNA/unknown).
        // Used to skip downloading / prompting when the runtime is already satisfied.
        public static bool IsConfidentlyPresent(
            uint? depotId,
            string hasRunKey,
            string processPath,
            string processEntryName = null)
        {
            bool present;
            if (!TryProbePresence(depotId, hasRunKey, processPath, processEntryName, out present))
                return false;
            return present;
        }

        // false = no probe for this redist (caller keeps HasRun-only / download behavior).
        private static bool TryProbePresence(
            uint? depotId,
            string hasRunKey,
            string processPath,
            string processEntryName,
            out bool present)
        {
            present = false;
            string family;
            string version;
            if (!TryResolveFamilyVersion(depotId, hasRunKey, processPath, out family, out version))
                return false;

            CpuArch arch = ResolveArch(processEntryName);

            if (string.Equals(family, "OpenAL", StringComparison.OrdinalIgnoreCase))
            {
                present = HasSystemFile("OpenAL32.dll", arch) || HasSystemFile("wrap_oal.dll", arch);
                return true;
            }

            if (string.Equals(family, "DirectX", StringComparison.OrdinalIgnoreCase))
            {
                present = HasSystemFile("d3dx9_43.dll", arch);
                return true;
            }

            if (string.Equals(family, "DotNet", StringComparison.OrdinalIgnoreCase)
                || string.Equals(family, ".NET", StringComparison.OrdinalIgnoreCase))
            {
                present = IsDotNetPresent(version);
                return true;
            }

            if (string.Equals(family, "vcredist", StringComparison.OrdinalIgnoreCase))
            {
                present = IsVisualCppPresent(version, arch);
                return true;
            }

            // PhysX / XNA / unknown: no reliable probe.
            return false;
        }

        private static CpuArch ResolveArch(string processEntryName)
        {
            if (string.IsNullOrWhiteSpace(processEntryName))
                return CpuArch.AllRequired;

            string name = processEntryName.Trim();
            if (name.Equals("x86", StringComparison.OrdinalIgnoreCase)
                || name.Equals("win32", StringComparison.OrdinalIgnoreCase))
                return CpuArch.X86;
            if (name.Equals("x64", StringComparison.OrdinalIgnoreCase)
                || name.Equals("win64", StringComparison.OrdinalIgnoreCase))
                return CpuArch.X64;

            // Named installer entries (game-local scripts) — either arch satisfies.
            return CpuArch.AllRequired;
        }

        private static bool TryResolveFamilyVersion(
            uint? depotId,
            string hasRunKey,
            string processPath,
            out string family,
            out string version)
        {
            family = null;
            version = null;

            if (depotId.HasValue
                && SteamCommonRedistributableConstants.TryGetDepot(depotId.Value, out var info)
                && !string.IsNullOrWhiteSpace(info.RelativeInstallScriptPath))
            {
                if (TryParseCommonRedistRelative(info.RelativeInstallScriptPath, out family, out version))
                    return true;
            }

            if (TryParseCommonRedistFromHasRunKey(hasRunKey, out family, out version))
                return true;

            if (TryParseCommonRedistRelative(processPath, out family, out version))
                return true;

            return false;
        }

        private static bool TryParseCommonRedistFromHasRunKey(string hasRunKey, out string family, out string version)
        {
            family = null;
            version = null;
            if (string.IsNullOrWhiteSpace(hasRunKey))
                return false;

            string key = hasRunKey.Replace('/', '\\');
            const string marker = @"\CommonRedist\";
            int idx = key.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                return false;

            string tail = key.Substring(idx + marker.Length).Trim('\\');
            string[] parts = tail.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return false;

            family = parts[0];
            version = parts.Length > 1 ? parts[1] : null;
            return true;
        }

        private static bool TryParseCommonRedistRelative(string path, out string family, out string version)
        {
            family = null;
            version = null;
            if (string.IsNullOrWhiteSpace(path))
                return false;

            string normalized = path.Replace('/', '\\');
            const string marker = @"\_CommonRedist\";
            int idx = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            string tail;
            if (idx >= 0)
            {
                tail = normalized.Substring(idx + marker.Length).Trim('\\');
            }
            else if (normalized.StartsWith(PathConstants.SteamCommonRedistFolderName + "\\", StringComparison.OrdinalIgnoreCase)
                     || normalized.StartsWith(PathConstants.SteamCommonRedistFolderName + "/", StringComparison.OrdinalIgnoreCase))
            {
                tail = normalized.Substring(PathConstants.SteamCommonRedistFolderName.Length).Trim('\\', '/');
            }
            else
                return false;

            string[] parts = tail.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return false;

            family = parts[0];
            version = parts.Length > 1 ? parts[1] : null;
            return true;
        }

        private static string GetWindowsDirectory()
        {
            string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (string.IsNullOrEmpty(windir))
                windir = Environment.GetEnvironmentVariable("WINDIR") ?? @"C:\Windows";
            return windir;
        }

        // Arch-aware System32 / SysWOW64 lookup (64-bit process: System32 = x64, SysWOW64 = x86).
        private static bool HasSystemFile(string fileName, CpuArch arch)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            try
            {
                string windir = GetWindowsDirectory();
                if (arch == CpuArch.X64)
                {
                    return Environment.Is64BitOperatingSystem
                        && File.Exists(Path.Combine(windir, "System32", fileName));
                }

                if (arch == CpuArch.X86)
                    return HasSystemFileX86(windir, fileName);

                // Depot-level: both arches on 64-bit OS; System32 (x86) on 32-bit OS.
                if (!Environment.Is64BitOperatingSystem)
                    return File.Exists(Path.Combine(windir, "System32", fileName));

                return File.Exists(Path.Combine(windir, "System32", fileName))
                    && HasSystemFileX86(windir, fileName);
            }
            catch
            {
            }

            return false;
        }

        private static bool HasSystemFileX86(string windir, string fileName)
        {
            if (Environment.Is64BitOperatingSystem)
                return File.Exists(Path.Combine(windir, "SysWOW64", fileName));
            return File.Exists(Path.Combine(windir, "System32", fileName));
        }

        private static bool IsDotNetPresent(string version)
        {
            string v = (version ?? string.Empty).Trim();
            if (v.StartsWith("3.5", StringComparison.OrdinalIgnoreCase))
                return IsNdpInstalled(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v3.5");

            // 4.0 Client Profile and full 4.x broadly share the v4\Full tree on modern Windows.
            if (v.StartsWith("4.", StringComparison.OrdinalIgnoreCase))
                return IsNdpInstalled(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full");

            return true;
        }

        private static bool IsNdpInstalled(string subKey)
        {
            try
            {
                foreach (RegistryView view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
                {
                    using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                    using (RegistryKey key = baseKey.OpenSubKey(subKey, false))
                    {
                        if (key == null)
                            continue;
                        object install = key.GetValue("Install");
                        if (install is int && (int)install == 1)
                            return true;
                        if (install != null && string.Equals(install.ToString(), "1", StringComparison.Ordinal))
                            return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool IsVisualCppPresent(string version, CpuArch arch)
        {
            string year = (version ?? string.Empty).Trim();
            if (year == "2005")
                return IsVisualCppArchSatisfied(arch, a => HasWinSxSCrt("Microsoft.VC80.CRT", a));
            if (year == "2008")
                return IsVisualCppArchSatisfied(arch, a => HasWinSxSCrt("Microsoft.VC90.CRT", a));
            if (year == "2010")
                return IsVisualCppArchSatisfied(
                    arch,
                    a => HasVcRuntimeRegistry(@"SOFTWARE\Microsoft\VisualStudio\10.0\VC\VCRedist", a)
                        || HasSystemFile("msvcr100.dll", a)
                        || HasSystemFile("msvcp100.dll", a));
            if (year == "2012")
                return IsVisualCppArchSatisfied(
                    arch,
                    a => HasVcRuntimesRegistry(@"SOFTWARE\Microsoft\VisualStudio\11.0\VC\Runtimes", a, minMinor: 0)
                        || HasSystemFile("msvcr110.dll", a)
                        || HasSystemFile("msvcp110.dll", a));
            if (year == "2013")
                return IsVisualCppArchSatisfied(
                    arch,
                    a => HasVcRuntimesRegistry(@"SOFTWARE\Microsoft\VisualStudio\12.0\VC\Runtimes", a, minMinor: 0)
                        || HasSystemFile("msvcr120.dll", a)
                        || HasSystemFile("msvcp120.dll", a));

            // 2015–2022 share VS 14.0 VC\Runtimes; Minor distinguishes toolset years.
            if (year == "2015")
                return IsVisualCppArchSatisfied(arch, a => IsUniversalCrtPresent(a, minMinor: 0));
            if (year == "2017")
                return IsVisualCppArchSatisfied(arch, a => IsUniversalCrtPresent(a, minMinor: 10));
            if (year == "2019")
                return IsVisualCppArchSatisfied(arch, a => IsUniversalCrtPresent(a, minMinor: 20));
            if (year == "2022")
                return IsVisualCppArchSatisfied(arch, a => IsUniversalCrtPresent(a, minMinor: 30));

            return true;
        }

        private static bool IsVisualCppArchSatisfied(CpuArch arch, Func<CpuArch, bool> probeOneArch)
        {
            if (arch == CpuArch.X86 || arch == CpuArch.X64)
                return probeOneArch(arch);

            // Depot-level: both arches on 64-bit OS; x86 only on 32-bit OS.
            if (!Environment.Is64BitOperatingSystem)
                return probeOneArch(CpuArch.X86);

            return probeOneArch(CpuArch.X86) && probeOneArch(CpuArch.X64);
        }

        private static bool IsUniversalCrtPresent(CpuArch arch, int minMinor)
        {
            if (HasVcRuntimesRegistry(
                    @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes",
                    arch,
                    minMinor))
                return true;

            // DLL fallback cannot distinguish 2015 vs 2022; only accept when minMinor is 0 (2015).
            if (minMinor > 0)
                return false;
            return HasSystemFile("vcruntime140.dll", arch) || HasSystemFile("msvcp140.dll", arch);
        }

        // VC 2005/2008 CRT lives under WinSxS (not System32 as a naked file).
        private static bool HasWinSxSCrt(string assemblyPrefix, CpuArch arch)
        {
            try
            {
                string winsxs = Path.Combine(GetWindowsDirectory(), "WinSxS");
                if (!Directory.Exists(winsxs))
                    return false;

                string archToken = arch == CpuArch.X64 ? "amd64_" : "x86_";
                // e.g. x86_microsoft.vc80.crt_...
                string search = archToken + assemblyPrefix.ToLowerInvariant();
                foreach (string dir in Directory.EnumerateDirectories(winsxs, search + "*"))
                {
                    if (!string.IsNullOrEmpty(dir))
                        return true;
                }
            }
            catch
            {
            }

            return false;
        }

        // Older VCRedist keys: ...\VC\VCRedist\x86 with Installed=1.
        private static bool HasVcRuntimeRegistry(string relativeRoot, CpuArch arch)
        {
            string archName = arch == CpuArch.X64 ? "x64" : "x86";
            string subKey = relativeRoot.TrimEnd('\\') + "\\" + archName;
            try
            {
                foreach (RegistryView view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
                {
                    using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                    using (RegistryKey key = baseKey.OpenSubKey(subKey, false))
                    {
                        if (key == null)
                            continue;
                        if (IsRegistryInstalledFlag(key))
                            return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        // VC 2012+: ...\VC\Runtimes\x86 with Installed=1 and Minor (14.x toolset years).
        private static bool HasVcRuntimesRegistry(string relativeRoot, CpuArch arch, int minMinor)
        {
            string archName = arch == CpuArch.X64 ? "x64" : "x86";
            string subKey = relativeRoot.TrimEnd('\\') + "\\" + archName;
            try
            {
                foreach (RegistryView view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
                {
                    using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                    using (RegistryKey key = baseKey.OpenSubKey(subKey, false))
                    {
                        if (key == null)
                            continue;
                        if (!IsRegistryInstalledFlag(key))
                            continue;
                        if (minMinor <= 0)
                            return true;
                        if (TryReadRegistryInt(key, "Minor", out int minor) && minor >= minMinor)
                            return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool IsRegistryInstalledFlag(RegistryKey key)
        {
            object install = key.GetValue("Installed");
            if (install is int && (int)install == 1)
                return true;
            if (install != null && string.Equals(install.ToString(), "1", StringComparison.Ordinal))
                return true;
            return false;
        }

        private static bool TryReadRegistryInt(RegistryKey key, string name, out int value)
        {
            value = 0;
            object raw = key.GetValue(name);
            if (raw is int)
            {
                value = (int)raw;
                return true;
            }
            if (raw != null && int.TryParse(raw.ToString(), out value))
                return true;
            return false;
        }
    }
}
