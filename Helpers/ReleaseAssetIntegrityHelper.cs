using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using SmartGoldbergEmu.JsonKit;

namespace SmartGoldbergEmu.Helpers
{
    // GitHub release asset digests (sha256:…) and post-download verification for auto-updates.
    public static class ReleaseAssetIntegrityHelper
    {
        public const string Sha256DigestPrefix = "sha256:";

        public static bool TryParseGitHubAssetSha256Digest(string digest, out string sha256Hex)
        {
            sha256Hex = null;
            if (string.IsNullOrWhiteSpace(digest))
                return false;

            string trimmed = digest.Trim();
            if (!trimmed.StartsWith(Sha256DigestPrefix, StringComparison.OrdinalIgnoreCase))
                return false;

            string hex = trimmed.Substring(Sha256DigestPrefix.Length).Trim();
            if (hex.Length != 64)
                return false;

            for (int i = 0; i < hex.Length; i++)
            {
                char c = hex[i];
                bool isHex = (c >= '0' && c <= '9')
                    || (c >= 'a' && c <= 'f')
                    || (c >= 'A' && c <= 'F');
                if (!isHex)
                    return false;
            }

            sha256Hex = hex.ToLowerInvariant();
            return true;
        }

        public static string TryReadAssetSha256Hex(JsonObject asset)
        {
            if (asset == null)
                return null;

            if (!TryParseGitHubAssetSha256Digest(asset["digest"]?.ToString(), out string hex))
                return null;

            return hex;
        }

        public static void EnsureTrustedGitHubHttpsDownloadUrl(string url)
        {
            if (PathValidationHelper.IsTrustedGitHubReleaseDownloadUrl(url))
                return;

            throw new InvalidOperationException(
                "Update download URL must be an HTTPS GitHub release asset URL.");
        }

        public static string TryComputeSha256Hex(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return null;

            try
            {
                using (SHA256 checksum = SHA256.Create())
                using (FileStream fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    byte[] hashBytes = checksum.ComputeHash(fileStream);
                    var sb = new StringBuilder(hashBytes.Length * 2);
                    foreach (byte b in hashBytes)
                        sb.Append(b.ToString("x2"));
                    return sb.ToString();
                }
            }
            catch
            {
                return null;
            }
        }

        public static void VerifyFileSha256OrThrow(string filePath, string expectedSha256Hex)
        {
            if (string.IsNullOrWhiteSpace(expectedSha256Hex))
                throw new InvalidOperationException("Update archive SHA-256 digest is missing.");

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                throw new InvalidOperationException("Downloaded update archive is missing.");

            string actual = TryComputeSha256Hex(filePath);
            if (string.IsNullOrEmpty(actual))
                throw new InvalidOperationException("Could not compute SHA-256 of the downloaded update archive.");

            if (!string.Equals(actual, expectedSha256Hex.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Downloaded update archive SHA-256 does not match the GitHub release digest.");
            }
        }
    }
}
