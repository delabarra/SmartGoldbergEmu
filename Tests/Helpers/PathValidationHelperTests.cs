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
