using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using SmartGoldbergEmu.Constants;

namespace SmartGoldbergEmu.Helpers
{
    public static class ApplicationVersionHelper
    {
        public static string GetWindowTitle()
        {
            return ApplicationConstants.WindowTitle + " - " + GetDisplayVersion();
        }

        // Stable: MAJOR.MINOR or MAJOR.MINOR.PATCH. Preview: same + (build N, preview).
        public static string GetDisplayVersion()
        {
            if (!TryParseVersionComponents(out int major, out int minor, out int patch, out int build, out string previewLabel))
                return "unknown";

            return FormatDisplayVersion(major, minor, patch, build, previewLabel);
        }

        // Full MAJOR.MINOR.PATCH for release tags and update comparison (patch always present).
        public static string GetVersionForComparison()
        {
            if (!TryParseVersionComponents(out int major, out int minor, out int patch, out int _, out string _))
                return string.Empty;

            return major + "." + minor + "." + patch;
        }

        public static bool TryGetBuildNumber(out int buildNumber)
        {
            buildNumber = 0;
            if (!TryParseVersionComponents(out int _, out int _, out int _, out int build, out string _))
                return false;

            if (build <= 0)
                return false;

            buildNumber = build;
            return true;
        }

        public static string FormatVersionLabel(int major, int minor, int patch)
        {
            var label = new StringBuilder();
            label.Append(major).Append('.').Append(minor);
            if (patch > 0)
                label.Append('.').Append(patch);

            return label.ToString();
        }

        public static string FormatDisplayVersion(int major, int minor, int patch, int build, string previewLabel)
        {
            string label = FormatVersionLabel(major, minor, patch);
            if (!string.IsNullOrEmpty(previewLabel) && build > 0)
                return label + " (build " + build + ", preview)";

            return label;
        }

        private static bool TryParseVersionComponents(out int major, out int minor, out int patch, out int build, out string previewLabel)
        {
            major = minor = patch = build = 0;
            previewLabel = null;
            string raw = GetRawProductVersion();
            if (!string.IsNullOrEmpty(raw))
                return TryParseProductVersion(raw, out major, out minor, out patch, out build, out previewLabel);

            var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
            if (assemblyVersion == null)
                return false;

            major = assemblyVersion.Major;
            minor = assemblyVersion.Minor;
            patch = assemblyVersion.Build >= 0 ? assemblyVersion.Build : 0;
            return true;
        }

        internal static bool TryParseProductVersion(string raw, out int major, out int minor, out int patch, out int build, out string previewLabel)
        {
            major = minor = patch = build = 0;
            previewLabel = null;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            string original = raw.Trim();
            raw = original;
            if (raw.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                raw = raw.Substring(1);

            if (ContainsPreviewPrerelease(original))
                previewLabel = "preview";

            int plus = raw.IndexOf('+');
            if (plus >= 0)
            {
                string metadata = raw.Substring(plus + 1).Trim();
                raw = raw.Substring(0, plus).Trim();
                if (TryParseBuildMetadata(metadata, out int metadataBuild))
                    build = metadataBuild;
            }

            int dash = raw.IndexOf('-');
            if (dash >= 0)
            {
                string prerelease = raw.Substring(dash + 1).Trim();
                raw = raw.Substring(0, dash).Trim();
                if (IsPreviewPrereleaseToken(prerelease))
                    previewLabel = "preview";
            }

            string[] parts = raw.Split('.');
            if (parts.Length < 2)
                return false;

            if (!int.TryParse(parts[0], out major) || !int.TryParse(parts[1], out minor))
                return false;

            patch = 0;
            if (parts.Length >= 3 && !int.TryParse(parts[2], out patch))
                return false;

            return true;
        }

        private static bool TryParseBuildMetadata(string metadata, out int build)
        {
            build = 0;
            if (string.IsNullOrWhiteSpace(metadata))
                return false;

            const string buildPrefix = "build.";
            if (!metadata.StartsWith(buildPrefix, StringComparison.OrdinalIgnoreCase))
                return false;

            string afterBuild = metadata.Substring(buildPrefix.Length);
            int digitLength = 0;
            while (digitLength < afterBuild.Length && char.IsDigit(afterBuild[digitLength]))
                digitLength++;

            if (digitLength == 0)
                return false;

            return int.TryParse(afterBuild.Substring(0, digitLength), out build);
        }

        private static bool ContainsPreviewPrerelease(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            const string marker = "-preview";
            for (int i = 0; i <= value.Length - marker.Length; i++)
            {
                if (!value.Substring(i, marker.Length).Equals(marker, StringComparison.OrdinalIgnoreCase))
                    continue;

                int after = i + marker.Length;
                if (after >= value.Length || value[after] == '+' || value[after] == '-')
                    return true;
            }

            return false;
        }

        private static bool IsPreviewPrereleaseToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;

            return token.Equals("preview", StringComparison.OrdinalIgnoreCase)
                || token.StartsWith("preview.", StringComparison.OrdinalIgnoreCase)
                || token.StartsWith("preview-", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetRawProductVersion()
        {
            string fromAttribute = GetInformationalVersionFromAssembly();
            if (!string.IsNullOrEmpty(fromAttribute))
                return fromAttribute;

            try
            {
                string exeName = Path.GetFileName(Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(exeName))
                    exeName = PathConstants.LauncherMainExecutableFileName;

                string exePath = Path.Combine(PathConstants.LauncherInstallDirectory, exeName);
                if (!File.Exists(exePath))
                    return null;

                string productVersion = FileVersionInfo.GetVersionInfo(exePath).ProductVersion;
                return string.IsNullOrWhiteSpace(productVersion) ? null : productVersion.Trim();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string GetInformationalVersionFromAssembly()
        {
            var attribute = (AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(
                Assembly.GetExecutingAssembly(),
                typeof(AssemblyInformationalVersionAttribute));

            return string.IsNullOrWhiteSpace(attribute?.InformationalVersion)
                ? null
                : attribute.InformationalVersion.Trim();
        }
    }
}
