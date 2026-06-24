using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Constants
{
    public static class LauncherReleaseConstants
    {
        // Launcher Update and release zips: delabarra/SmartGoldbergEmu (https://github.com/delabarra/SmartGoldbergEmu/releases).
        public const string GitHubOwner = "delabarra";
        public const string GitHubRepo = "SmartGoldbergEmu";

        public const string ReleasesApiUrlFormat =
            "https://api.github.com/repos/{0}/{1}/releases/latest";

        public const string ReleasesWebUrlFormat =
            "https://github.com/{0}/{1}/releases";

        public const string ReleaseTagWebUrlFormat =
            "https://github.com/{0}/{1}/releases/tag/v{2}";

        public const string RepositoryWebUrlFormat =
            "https://github.com/{0}/{1}";

        public const string ReleaseZipNamePrefix = "SmartGoldbergEmu-";

        public const string IniSectionApplication = "application";
        public const string IniKeyLauncherUpdateApiUrl = "launcher_update_api_url";

        public static bool IsReleaseRepositoryConfigured()
        {
            return !string.IsNullOrWhiteSpace(GitHubOwner) && !string.IsNullOrWhiteSpace(GitHubRepo);
        }

        public static string GetRepositoryWebUrl()
        {
            return string.Format(RepositoryWebUrlFormat, GitHubOwner, GitHubRepo);
        }

        public static bool TryGetReleasesWebUrl(out string webUrl)
        {
            if (!IsReleaseRepositoryConfigured())
            {
                webUrl = null;
                return false;
            }

            webUrl = string.Format(ReleasesWebUrlFormat, GitHubOwner, GitHubRepo);
            return true;
        }

        public static bool TryGetReleaseTagWebUrl(string version, out string webUrl)
        {
            webUrl = null;
            if (!IsReleaseRepositoryConfigured() || string.IsNullOrWhiteSpace(version))
                return false;

            version = version.Trim();
            if (version.StartsWith("v", System.StringComparison.OrdinalIgnoreCase))
                version = version.Substring(1);

            int plus = version.IndexOf('+');
            if (plus >= 0)
                version = version.Substring(0, plus).Trim();

            if (string.IsNullOrWhiteSpace(version))
                return false;

            webUrl = string.Format(ReleaseTagWebUrlFormat, GitHubOwner, GitHubRepo, version);
            return true;
        }

        public static bool TryGetReleasesApiUrl(out string apiUrl)
        {
            try
            {
                string overrideUrl = ServiceLocator.AppDataService.GetLauncherUpdateReleasesApiUrlOverride();
                if (!string.IsNullOrWhiteSpace(overrideUrl) && PathValidationHelper.IsSafeUrl(overrideUrl))
                {
                    apiUrl = overrideUrl.Trim();
                    return true;
                }
            }
            catch
            {
            }

            if (!IsReleaseRepositoryConfigured())
            {
                apiUrl = null;
                return false;
            }

            apiUrl = string.Format(ReleasesApiUrlFormat, GitHubOwner, GitHubRepo);
            return true;
        }
    }
}
