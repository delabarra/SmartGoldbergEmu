using System;
using System.IO;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Services;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    public sealed class GameImageServiceListViewTests
    {
        [Fact]
        public void ListView_store_banner_uses_header_only()
        {
            string resources = CreateResourcesDirectory();
            File.WriteAllText(Path.Combine(resources, "library_600x900.jpg"), string.Empty);
            File.WriteAllText(Path.Combine(resources, PathConstants.SteamGameResourcesHeaderImageFileName), string.Empty);

            var path = GameImageService.ResolveStrictListViewImagePath(
                resources,
                new[] { PathConstants.SteamGameResourcesHeaderImageFileName });

            Assert.EndsWith(PathConstants.SteamGameResourcesHeaderImageFileName, path, StringComparison.OrdinalIgnoreCase);
            Cleanup(resources);
        }

        [Fact]
        public void ListView_store_banner_missing_header_returns_null_even_when_other_assets_exist()
        {
            string resources = CreateResourcesDirectory();
            File.WriteAllText(Path.Combine(resources, "library_600x900.jpg"), string.Empty);
            File.WriteAllText(Path.Combine(resources, "logo.png"), string.Empty);

            var path = GameImageService.ResolveStrictListViewImagePath(
                resources,
                new[] { PathConstants.SteamGameResourcesHeaderImageFileName });

            Assert.Null(path);
            Cleanup(resources);
        }

        [Fact]
        public void ListView_store_banner_accepts_pics_header_image_filename()
        {
            string resources = CreateResourcesDirectory();
            File.WriteAllText(Path.Combine(resources, "header.jpg"), string.Empty);

            var path = GameImageService.ResolveStrictListViewImagePath(
                resources,
                new[] { "header.jpg", PathConstants.SteamGameResourcesHeaderImageFileName });

            Assert.EndsWith("header.jpg", path, StringComparison.OrdinalIgnoreCase);
            Cleanup(resources);
        }

        [Fact]
        public void ListView_library_cover_accepts_pics_library_capsule_2x_filename()
        {
            string resources = CreateResourcesDirectory();
            File.WriteAllText(Path.Combine(resources, "library_capsule_2x.jpg"), string.Empty);

            var path = GameImageService.ResolveStrictListViewImagePath(
                resources,
                new[] { "library_capsule_2x.jpg", "library_600x900_2x.jpg", "library_capsule.jpg" });

            Assert.EndsWith("library_capsule_2x.jpg", path, StringComparison.OrdinalIgnoreCase);
            Cleanup(resources);
        }

        [Fact]
        public void ListView_library_cover_ignores_small_capsule_substitutes()
        {
            string resources = CreateResourcesDirectory();
            File.WriteAllText(Path.Combine(resources, PathConstants.SteamGameResourcesSmallCapsuleImageFileName), string.Empty);
            File.WriteAllText(Path.Combine(resources, "library_capsule.jpg"), string.Empty);

            var path = GameImageService.ResolveStrictListViewImagePath(
                resources,
                new[]
                {
                    "library_capsule_2x.jpg",
                    "library_600x900_2x.jpg",
                    PathConstants.SteamGameResourcesLegacyLibraryCapsuleImageFileName
                });

            Assert.Null(path);
            Cleanup(resources);
        }

        [Fact]
        public void ListView_logos_accept_pics_library_logo_filename_on_disk()
        {
            string resources = CreateResourcesDirectory();
            File.WriteAllText(Path.Combine(resources, "library_logo_2x.png"), string.Empty);

            var path = GameImageService.ResolveStrictListViewImagePath(
                resources,
                new[] { "library_logo_2x.png", "logo_2x.png", PathConstants.SteamGameResourcesLibraryLogoImageFileName });

            Assert.EndsWith("library_logo_2x.png", path, StringComparison.OrdinalIgnoreCase);
            Cleanup(resources);
        }

        [Fact]
        public void ListView_logos_do_not_use_library_cover_files()
        {
            string resources = CreateResourcesDirectory();
            File.WriteAllText(Path.Combine(resources, "library_600x900.jpg"), string.Empty);

            var path = GameImageService.ResolveStrictListViewImagePath(
                resources,
                new[]
                {
                    "logo_2x.png",
                    PathConstants.SteamGameResourcesLibraryLogoImageFileName
                });

            Assert.Null(path);
            Cleanup(resources);
        }

        private static string CreateResourcesDirectory()
        {
            string resources = Path.Combine(
                Path.GetTempPath(),
                "sge-listview-" + Guid.NewGuid().ToString("N"),
                PathConstants.GamesPerAppResourcesFolderName);
            Directory.CreateDirectory(resources);
            return resources;
        }

        private static void Cleanup(string resourcesDirectory)
        {
            try
            {
                string parent = Directory.GetParent(resourcesDirectory)?.FullName;
                if (!string.IsNullOrEmpty(parent))
                    Directory.Delete(parent, recursive: true);
            }
            catch
            {
            }
        }
    }
}
