using System;
using System.Collections.Generic;
using System.IO;
using AppDataKit;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Services;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    public sealed class GameImageServiceListViewTests
    {
        [Fact]
        public void ListView_store_banner_uses_header_not_library_header()
        {
            string resources = CreateResourcesDirectory();
            File.WriteAllText(Path.Combine(resources, PathConstants.SteamGameResourcesHeaderImageFileName), string.Empty);
            File.WriteAllText(Path.Combine(resources, PathConstants.SteamGameResourcesLibraryHeaderImageFileName), string.Empty);
            File.WriteAllText(Path.Combine(resources, PathConstants.SteamGameResourcesLibraryHeader2xImageFileName), string.Empty);

            var path = GameImageService.ResolveStrictListViewImagePath(
                resources,
                new[] { PathConstants.SteamGameResourcesHeaderImageFileName });

            Assert.EndsWith(
                PathConstants.SteamGameResourcesHeaderImageFileName,
                path,
                StringComparison.OrdinalIgnoreCase);
            Cleanup(resources);
        }

        [Fact]
        public void ListView_store_banner_missing_header_returns_null_even_when_library_header_exists()
        {
            string resources = CreateResourcesDirectory();
            File.WriteAllText(Path.Combine(resources, PathConstants.SteamGameResourcesLibraryHeaderImageFileName), string.Empty);
            File.WriteAllText(Path.Combine(resources, "library_600x900.jpg"), string.Empty);

            var path = GameImageService.ResolveStrictListViewImagePath(
                resources,
                new[] { PathConstants.SteamGameResourcesHeaderImageFileName });

            Assert.Null(path);
            Cleanup(resources);
        }

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
        public void ListView_library_cover_prefers_2x_when_both_exist()
        {
            string resources = CreateResourcesDirectory();
            File.WriteAllText(Path.Combine(resources, "library_capsule.jpg"), string.Empty);
            File.WriteAllText(Path.Combine(resources, "library_capsule_2x.jpg"), string.Empty);

            var path = GameImageService.ResolveStrictListViewImagePath(
                resources,
                new[]
                {
                    PathConstants.SteamGameResourcesLibraryCapsule2xImageFileName,
                    PathConstants.SteamGameResourcesLibraryCapsuleImageFileName
                });

            Assert.EndsWith(
                PathConstants.SteamGameResourcesLibraryCapsule2xImageFileName,
                path,
                StringComparison.OrdinalIgnoreCase);
            Cleanup(resources);
        }

        [Fact]
        public void ListView_logos_prefer_library_logo_2x_over_logo_png()
        {
            string resources = CreateResourcesDirectory();
            File.WriteAllText(Path.Combine(resources, PathConstants.SteamGameResourcesLibraryLogoImageFileName), string.Empty);
            File.WriteAllText(Path.Combine(resources, PathConstants.SteamGameResourcesLibraryLogoPics2xImageFileName), string.Empty);

            var path = GameImageService.ResolveStrictListViewImagePath(
                resources,
                new[]
                {
                    PathConstants.SteamGameResourcesLibraryLogoPics2xImageFileName,
                    PathConstants.SteamGameResourcesLibraryLogoPicsImageFileName,
                    PathConstants.SteamGameResourcesLibraryLogo2xImageFileName,
                    PathConstants.SteamGameResourcesLibraryLogoImageFileName
                });

            Assert.EndsWith(
                PathConstants.SteamGameResourcesLibraryLogoPics2xImageFileName,
                path,
                StringComparison.OrdinalIgnoreCase);
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

        [Fact]
        public void AllocateUniqueResourceFileName_keeps_canonical_name_then_hash_prefix()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            const string english = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/library_capsule.jpg";
            const string other = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb/library_capsule.jpg";

            string first = GameImageService.AllocateUniqueResourceFileName(english, seen);
            string second = GameImageService.AllocateUniqueResourceFileName(other, seen);

            Assert.Equal("library_capsule.jpg", first);
            Assert.Equal("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb_library_capsule.jpg", second);
        }

        [Fact]
        public void AllocateUniqueResourceFileName_duplicate_bare_name_returns_null()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Assert.Equal("header.jpg", GameImageService.AllocateUniqueResourceFileName("header.jpg", seen));
            Assert.Null(GameImageService.AllocateUniqueResourceFileName("header.jpg", seen));
        }

        [Fact]
        public void TryExtractLocalizedRelativePath_prefers_settings_language_then_english()
        {
            var node = new AppInfoKeyValue("image");
            node.Children.Add(new AppInfoKeyValue(SteamPicsKeyNames.English, "en/library_capsule.jpg"));
            node.Children.Add(new AppInfoKeyValue("japanese", "ja/library_capsule.jpg"));
            node.Children.Add(new AppInfoKeyValue("schinese", "zh/library_capsule.jpg"));

            Assert.Equal(
                "ja/library_capsule.jpg",
                GameImageService.TryExtractLocalizedRelativePath(node, "japanese"));
            Assert.Equal(
                "en/library_capsule.jpg",
                GameImageService.TryExtractLocalizedRelativePath(node, "koreana"));
            Assert.Equal(
                "en/library_capsule.jpg",
                GameImageService.TryExtractLocalizedRelativePath(node, SteamPicsKeyNames.English));
        }

        [Fact]
        public void ListView_cover_candidates_prefer_hash_prefixed_localized_file_before_bare_name()
        {
            const string japaneseHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            const string englishHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var pics = CreateLibraryCapsulePics(
                japaneseRelative: japaneseHash + "/library_capsule_2x.jpg",
                englishRelative: englishHash + "/library_capsule_2x.jpg");

            string[] japanese = GameImageService.BuildListViewLibraryCoverPreferredFileNames(pics, "japanese");
            Assert.Equal(japaneseHash + "_library_capsule_2x.jpg", japanese[0]);
            Assert.Equal("library_capsule_2x.jpg", japanese[1]);

            string[] english = GameImageService.BuildListViewLibraryCoverPreferredFileNames(
                pics, SteamPicsKeyNames.English);
            Assert.Equal(englishHash + "_library_capsule_2x.jpg", english[0]);
            Assert.Equal("library_capsule_2x.jpg", english[1]);
        }

        [Fact]
        public void ListView_cover_resolves_hash_prefixed_file_when_bare_name_is_another_language()
        {
            const string japaneseHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            const string englishHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            string resources = CreateResourcesDirectory();
            // Canonical slot still holds the previously preferred Japanese download.
            File.WriteAllText(Path.Combine(resources, "library_capsule_2x.jpg"), "ja");
            File.WriteAllText(Path.Combine(resources, englishHash + "_library_capsule_2x.jpg"), "en");

            var pics = CreateLibraryCapsulePics(
                japaneseRelative: japaneseHash + "/library_capsule_2x.jpg",
                englishRelative: englishHash + "/library_capsule_2x.jpg");
            string[] preferred = GameImageService.BuildListViewLibraryCoverPreferredFileNames(
                pics, SteamPicsKeyNames.English);

            var path = GameImageService.ResolveStrictListViewImagePath(resources, preferred);

            Assert.EndsWith(englishHash + "_library_capsule_2x.jpg", path, StringComparison.OrdinalIgnoreCase);
            Cleanup(resources);
        }

        [Fact]
        public void ListView_cover_resolves_bare_name_when_preferred_language_owns_canonical_slot()
        {
            const string japaneseHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            string resources = CreateResourcesDirectory();
            File.WriteAllText(Path.Combine(resources, "library_capsule_2x.jpg"), "ja");
            File.WriteAllText(
                Path.Combine(resources, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa_library_capsule_2x.jpg"),
                "en");

            var pics = CreateLibraryCapsulePics(
                japaneseRelative: japaneseHash + "/library_capsule_2x.jpg",
                englishRelative: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/library_capsule_2x.jpg");
            string[] preferred = GameImageService.BuildListViewLibraryCoverPreferredFileNames(pics, "japanese");

            var path = GameImageService.ResolveStrictListViewImagePath(resources, preferred);

            Assert.EndsWith("library_capsule_2x.jpg", path, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("_library_capsule_2x.jpg", Path.GetFileName(path), StringComparison.OrdinalIgnoreCase);
            Cleanup(resources);
        }

        private static AppInfoKeyValue CreateLibraryCapsulePics(string japaneseRelative, string englishRelative)
        {
            var image2x = new AppInfoKeyValue(SteamPicsKeyNames.Image2x);
            image2x.Children.Add(new AppInfoKeyValue("japanese", japaneseRelative));
            image2x.Children.Add(new AppInfoKeyValue(SteamPicsKeyNames.English, englishRelative));

            var capsule = new AppInfoKeyValue(SteamPicsKeyNames.LibraryCapsule);
            capsule.Children.Add(image2x);

            var libraryAssetsFull = new AppInfoKeyValue(SteamPicsKeyNames.LibraryAssetsFull);
            libraryAssetsFull.Children.Add(capsule);

            var common = new AppInfoKeyValue(PathConstants.SteamAppsCommonDirectoryName);
            common.Children.Add(libraryAssetsFull);

            var root = new AppInfoKeyValue("app");
            root.Children.Add(common);
            return root;
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
