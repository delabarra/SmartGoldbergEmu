using System.IO;
using Xunit;
using SmartGoldbergEmu.ExtractKit;
using SmartGoldbergEmu.ExtractKit.Internal;

namespace SmartGoldbergEmu.Tests.ExtractKit
{
    public sealed class EntryPathTests
    {
        private static readonly string Root = Path.Combine(Path.GetTempPath(), "sge-entry-path-tests", "dest");

        private static string UnderRoot(params string[] parts)
        {
            string path = Path.GetFullPath(Root);
            foreach (string part in parts)
                path = Path.Combine(path, part);
            return path;
        }

        [Fact]
        public void ResolveDestinationPath_allows_nested_entry()
        {
            Assert.Equal(UnderRoot("a", "b", "c.txt"), EntryPath.ResolveDestinationPath(Root, "a/b/c.txt"));
            Assert.Equal(UnderRoot("a", "b", "c.txt"), EntryPath.ResolveDestinationPath(Root, @"a\b\c.txt"));
        }

        [Fact]
        public void ResolveDestinationPath_allows_root_with_trailing_separator()
        {
            string root = Root + Path.DirectorySeparatorChar;
            Assert.Equal(UnderRoot("x.dll"), EntryPath.ResolveDestinationPath(root, "x.dll"));
        }

        [Fact]
        public void ResolveDestinationPath_allows_dot_dot_that_stays_inside_root()
        {
            Assert.Equal(UnderRoot("b.txt"), EntryPath.ResolveDestinationPath(Root, "a/../b.txt"));
        }

        [Fact]
        public void ResolveDestinationPath_strips_leading_slash_and_dot_like_Normalize()
        {
            Assert.Equal(UnderRoot("abs"), EntryPath.ResolveDestinationPath(Root, "/abs"));
            Assert.Equal(UnderRoot("a.txt"), EntryPath.ResolveDestinationPath(Root, "./a.txt"));
        }

        [Theory]
        [InlineData("../x")]
        [InlineData("a/../../x")]
        [InlineData(@"..\..\x.exe")]
        [InlineData("../dest-evil/x")]
        [InlineData("C:/x")]
        [InlineData(@"C:\x")]
        [InlineData("C:x")]
        [InlineData("//server/share/x")]
        [InlineData(@"\\server\share\x")]
        [InlineData("..")]
        [InlineData("a/..")]
        [InlineData("a/../")]
        [InlineData("")]
        [InlineData(null)]
        public void ResolveDestinationPath_rejects_paths_outside_root(string entryPath)
        {
            Assert.Throws<ExtractKitException>(() => EntryPath.ResolveDestinationPath(Root, entryPath));
        }
    }
}
