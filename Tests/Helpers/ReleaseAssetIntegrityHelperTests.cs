using System;
using System.IO;
using System.Text;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.JsonKit;
using Xunit;

namespace SmartGoldbergEmu.Tests.Helpers
{
    public sealed class ReleaseAssetIntegrityHelperTests
    {
        [Theory]
        [InlineData("sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", true)]
        [InlineData("SHA256:0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true)]
        [InlineData("sha256:short", false)]
        [InlineData("md5:0123456789abcdef0123456789abcdef", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void TryParseGitHubAssetSha256Digest_accepts_sha256_prefix(string digest, bool expected)
        {
            bool ok = ReleaseAssetIntegrityHelper.TryParseGitHubAssetSha256Digest(digest, out string hex);
            Assert.Equal(expected, ok);
            if (expected)
                Assert.Equal(64, hex.Length);
            else
                Assert.Null(hex);
        }

        [Fact]
        public void TryReadAssetSha256Hex_reads_digest_property()
        {
            var asset = JsonObject.Parse(
                "{\"name\":\"a.zip\",\"digest\":\"sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\"}");
            Assert.Equal(
                "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                ReleaseAssetIntegrityHelper.TryReadAssetSha256Hex(asset));
        }

        [Fact]
        public void VerifyFileSha256OrThrow_accepts_matching_file()
        {
            string path = Path.Combine(Path.GetTempPath(), "sge-hash-" + Guid.NewGuid().ToString("N") + ".bin");
            try
            {
                File.WriteAllBytes(path, Encoding.UTF8.GetBytes("smartgoldbergemu-integrity"));
                string hex = ReleaseAssetIntegrityHelper.TryComputeSha256Hex(path);
                Assert.False(string.IsNullOrEmpty(hex));
                ReleaseAssetIntegrityHelper.VerifyFileSha256OrThrow(path, hex);
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        }

        [Fact]
        public void VerifyFileSha256OrThrow_rejects_mismatch()
        {
            string path = Path.Combine(Path.GetTempPath(), "sge-hash-" + Guid.NewGuid().ToString("N") + ".bin");
            try
            {
                File.WriteAllBytes(path, Encoding.UTF8.GetBytes("payload"));
                Assert.Throws<InvalidOperationException>(() =>
                    ReleaseAssetIntegrityHelper.VerifyFileSha256OrThrow(
                        path,
                        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"));
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        }

        [Fact]
        public void EnsureTrustedGitHubHttpsDownloadUrl_rejects_non_github_https()
        {
            Assert.Throws<InvalidOperationException>(() =>
                ReleaseAssetIntegrityHelper.EnsureTrustedGitHubHttpsDownloadUrl("https://evil.example/a.zip"));
        }
    }
}
