using System;
using System.IO;
using SmartGoldbergEmu.Helpers;
using Xunit;

namespace SmartGoldbergEmu.Tests.Helpers
{
    public sealed class PathValidationHelperTests
    {
        [Fact]
        public void IsPathWithinBase_rejects_path_outside_base()
        {
            string basePath = Path.Combine(Path.GetTempPath(), "sge-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(basePath);
            try
            {
                string outside = Path.GetFullPath(Path.Combine(basePath, "..", "outside-sge-" + Guid.NewGuid().ToString("N")));
                Assert.False(PathValidationHelper.IsPathWithinBase(basePath, outside));
            }
            finally
            {
                try { Directory.Delete(basePath, recursive: true); } catch { }
            }
        }

        [Fact]
        public void IsPathWithinBase_accepts_child_path()
        {
            string basePath = Path.Combine(Path.GetTempPath(), "sge-test-" + Guid.NewGuid().ToString("N"));
            string child = Path.Combine(basePath, "child");
            Directory.CreateDirectory(child);
            try
            {
                Assert.True(PathValidationHelper.IsPathWithinBase(basePath, child));
            }
            finally
            {
                try { Directory.Delete(basePath, recursive: true); } catch { }
            }
        }

        [Fact]
        public void TryResolveAndValidatePath_rejects_parent_traversal()
        {
            string basePath = Path.Combine(Path.GetTempPath(), "sge-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(basePath);
            try
            {
                Assert.False(PathValidationHelper.TryResolveAndValidatePath(basePath, "..\\Windows\\System32", out _));
            }
            finally
            {
                try { Directory.Delete(basePath, recursive: true); } catch { }
            }
        }

        [Fact]
        public void TryResolveAndValidatePath_accepts_relative_child()
        {
            string basePath = Path.Combine(Path.GetTempPath(), "sge-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(basePath);
            try
            {
                Assert.True(PathValidationHelper.TryResolveAndValidatePath(basePath, "sub\\file.txt", out string resolved));
                Assert.StartsWith(basePath, resolved, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                try { Directory.Delete(basePath, recursive: true); } catch { }
            }
        }

        [Theory]
        [InlineData("https://store.steampowered.com/app/1", true)]
        [InlineData("http://example.com", true)]
        [InlineData("file:///C:/temp/x", false)]
        [InlineData("javascript:alert(1)", false)]
        [InlineData("", false)]
        public void IsSafeUrl_allows_http_https_only(string url, bool expected)
        {
            Assert.Equal(expected, PathValidationHelper.IsSafeUrl(url));
        }

        [Theory]
        [InlineData("https://example.com", true)]
        [InlineData("http://example.com", false)]
        [InlineData("file:///C:/temp/x", false)]
        [InlineData("", false)]
        public void IsSafeHttpsUrl_rejects_non_https(string url, bool expected)
        {
            Assert.Equal(expected, PathValidationHelper.IsSafeHttpsUrl(url));
        }

        [Theory]
        [InlineData("https://github.com/owner/repo/releases/download/v1/a.zip", true)]
        [InlineData("https://objects.githubusercontent.com/github-production-release-asset-2e65be/1/a", true)]
        [InlineData("https://release-assets.githubusercontent.com/a.zip", true)]
        [InlineData("http://github.com/owner/repo/releases/download/v1/a.zip", false)]
        [InlineData("https://evil.example/a.zip", false)]
        [InlineData("https://github.com.evil.example/a.zip", false)]
        public void IsTrustedGitHubReleaseDownloadUrl_allows_github_asset_hosts_only(string url, bool expected)
        {
            Assert.Equal(expected, PathValidationHelper.IsTrustedGitHubReleaseDownloadUrl(url));
        }

        [Fact]
        public void SanitizeFileName_replaces_invalid_characters()
        {
            string result = PathValidationHelper.SanitizeFileName("bad<>|name");
            Assert.DoesNotContain("<", result);
            Assert.DoesNotContain(">", result);
            Assert.DoesNotContain("|", result);
        }
    }
}
