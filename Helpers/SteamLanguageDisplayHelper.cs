using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartGoldbergEmu.Helpers
{
    /// <summary>
    /// Helper for Steam language display names and API codes.
    /// Format: "Native Name - (English Name)" for display; API uses codes like "english", "schinese".
    /// </summary>
    public static class SteamLanguageDisplayHelper
    {
        public const string UseGlobalSettingOption = "Use Global Setting";

        private static readonly Dictionary<string, string> NativeToCode = new Dictionary<string, string>
        {
            { "العربية", "arabic" },
            { "български език", "bulgarian" },
            { "简体中文", "schinese" },
            { "繁體中文", "tchinese" },
            { "Hrvatski", "croatian" },
            { "čeština", "czech" },
            { "Dansk", "danish" },
            { "Nederlands", "dutch" },
            { "English", "english" },
            { "Suomi", "finnish" },
            { "Français", "french" },
            { "Deutsch", "german" },
            { "Ελληνικά", "greek" },
            { "Magyar", "hungarian" },
            { "Bahasa Indonesia", "indonesian" },
            { "Italiano", "italian" },
            { "日本語", "japanese" },
            { "한국어", "koreana" },
            { "Norsk", "norwegian" },
            { "Polski", "polish" },
            { "Português", "portuguese" },
            { "Português-Brasil", "brazilian" },
            { "Română", "romanian" },
            { "Русский", "russian" },
            { "Español-España", "spanish" },
            { "Español-Latinoamérica", "latam" },
            { "Svenska", "swedish" },
            { "ไทย", "thai" },
            { "Türkçe", "turkish" },
            { "Українська", "ukrainian" },
            { "Tiếng Việt", "vietnamese" }
        };

        private static readonly Dictionary<string, string> CodeToDisplay = new Dictionary<string, string>
        {
            { "arabic", "العربية - (Arabic)" },
            { "bulgarian", "български език - (Bulgarian)" },
            { "schinese", "简体中文 - (Chinese (Simplified))" },
            { "tchinese", "繁體中文 - (Chinese (Traditional))" },
            { "croatian", "Hrvatski - (Croatian)" },
            { "czech", "čeština - (Czech)" },
            { "danish", "Dansk - (Danish)" },
            { "dutch", "Nederlands - (Dutch)" },
            { "english", "English - (English)" },
            { "finnish", "Suomi - (Finnish)" },
            { "french", "Français - (French)" },
            { "german", "Deutsch - (German)" },
            { "greek", "Ελληνικά - (Greek)" },
            { "hungarian", "Magyar - (Hungarian)" },
            { "indonesian", "Bahasa Indonesia - (Indonesian)" },
            { "italian", "Italiano - (Italian)" },
            { "japanese", "日本語 - (Japanese)" },
            { "koreana", "한국어 - (Korean)" },
            { "norwegian", "Norsk - (Norwegian)" },
            { "polish", "Polski - (Polish)" },
            { "portuguese", "Português - (Portuguese)" },
            { "brazilian", "Português-Brasil - (Portuguese-Brazil)" },
            { "romanian", "Română - (Romanian)" },
            { "russian", "Русский - (Russian)" },
            { "spanish", "Español-España - (Spanish-Spain)" },
            { "latam", "Español-Latinoamérica - (Spanish-Latin America)" },
            { "swedish", "Svenska - (Swedish)" },
            { "thai", "ไทย - (Thai)" },
            { "turkish", "Türkçe - (Turkish)" },
            { "ukrainian", "Українська - (Ukrainian)" },
            { "vietnamese", "Tiếng Việt - (Vietnamese)" }
        };

        private static readonly Dictionary<string, string> SimpleDisplayToCode = new Dictionary<string, string>
        {
            { "English", "english" },
            { "French", "french" },
            { "German", "german" },
            { "Spanish", "spanish" },
            { "Italian", "italian" },
            { "Portuguese", "portuguese" },
            { "Russian", "russian" },
            { "Japanese", "japanese" },
            { "Korean", "koreana" },
            { "Simplified Chinese", "schinese" },
            { "Traditional Chinese", "tchinese" },
            { "Polish", "polish" },
            { "Dutch", "dutch" },
            { "Czech", "czech" },
            { "Hungarian", "hungarian" },
            { "Romanian", "romanian" },
            { "Turkish", "turkish" },
            { "Brazilian Portuguese", "brazilian" },
            { "Swedish", "swedish" },
            { "Norwegian", "norwegian" },
            { "Danish", "danish" },
            { "Finnish", "finnish" },
            { "Greek", "greek" },
            { "Thai", "thai" },
            { "Vietnamese", "vietnamese" },
            { "Arabic", "arabic" },
            { "Ukrainian", "ukrainian" }
        };

        private static readonly Dictionary<string, string> CodeToSimpleDisplay = new Dictionary<string, string>
        {
            { "arabic", "Arabic" },
            { "bulgarian", "Bulgarian" },
            { "schinese", "Simplified Chinese" },
            { "tchinese", "Traditional Chinese" },
            { "croatian", "Croatian" },
            { "czech", "Czech" },
            { "danish", "Danish" },
            { "dutch", "Dutch" },
            { "english", "English" },
            { "finnish", "Finnish" },
            { "french", "French" },
            { "german", "German" },
            { "greek", "Greek" },
            { "hungarian", "Hungarian" },
            { "indonesian", "Indonesian" },
            { "italian", "Italian" },
            { "japanese", "Japanese" },
            { "koreana", "Korean" },
            { "norwegian", "Norwegian" },
            { "polish", "Polish" },
            { "portuguese", "Portuguese" },
            { "brazilian", "Brazilian Portuguese" },
            { "romanian", "Romanian" },
            { "russian", "Russian" },
            { "spanish", "Spanish" },
            { "latam", "Spanish-Latin America" },
            { "swedish", "Swedish" },
            { "thai", "Thai" },
            { "turkish", "Turkish" },
            { "ukrainian", "Ukrainian" },
            { "vietnamese", "Vietnamese" }
        };

        /// <summary>
        /// Supported languages for dropdown display. Format: "Native Name - (English Name)".
        /// </summary>
        public static readonly string[] SupportedLanguages =
        {
            "العربية - (Arabic)",
            "български език - (Bulgarian)",
            "简体中文 - (Chinese (Simplified))",
            "繁體中文 - (Chinese (Traditional))",
            "Hrvatski - (Croatian)",
            "čeština - (Czech)",
            "Dansk - (Danish)",
            "Nederlands - (Dutch)",
            "English - (English)",
            "Suomi - (Finnish)",
            "Français - (French)",
            "Deutsch - (German)",
            "Ελληνικά - (Greek)",
            "Magyar - (Hungarian)",
            "Bahasa Indonesia - (Indonesian)",
            "Italiano - (Italian)",
            "日本語 - (Japanese)",
            "한국어 - (Korean)",
            "Norsk - (Norwegian)",
            "Polski - (Polish)",
            "Português - (Portuguese)",
            "Português-Brasil - (Portuguese-Brazil)",
            "Română - (Romanian)",
            "Русский - (Russian)",
            "Español-España - (Spanish-Spain)",
            "Español-Latinoamérica - (Spanish-Latin America)",
            "Svenska - (Swedish)",
            "ไทย - (Thai)",
            "Türkçe - (Turkish)",
            "Українська - (Ukrainian)",
            "Tiếng Việt - (Vietnamese)"
        };

        /// <summary>
        /// Converts display name to Steam API language code.
        /// </summary>
        public static string ToLanguageCode(string displayName)
        {
            if (string.IsNullOrEmpty(displayName))
                return "english";
            string nativeName = displayName.Contains(" - (") ? displayName.Substring(0, displayName.IndexOf(" - (")).Trim() : displayName;
            return NativeToCode.TryGetValue(nativeName, out string code) ? code : "english";
        }

        /// <summary>
        /// Converts Steam API language code to display name.
        /// </summary>
        public static string ToDisplayName(string code)
        {
            if (string.IsNullOrEmpty(code))
                return "English - (English)";
            return CodeToDisplay.TryGetValue(code.ToLowerInvariant(), out string displayName) ? displayName : "English - (English)";
        }

        public static string ToLanguageCodeFromSimpleDisplay(string displayName)
        {
            if (string.IsNullOrEmpty(displayName) || displayName == UseGlobalSettingOption)
                return "english";
            return SimpleDisplayToCode.TryGetValue(displayName, out string code) ? code : displayName.ToLowerInvariant();
        }

        public static string ToSimpleDisplayName(string code)
        {
            if (string.IsNullOrEmpty(code))
                return UseGlobalSettingOption;
            return CodeToSimpleDisplay.TryGetValue(code.ToLowerInvariant(), out string displayName) ? displayName : code;
        }

        public static List<string> ParseSupportedLanguageDisplayList(string supportedLanguages, string globalLanguagePlaceholder)
        {
            if (string.IsNullOrEmpty(supportedLanguages))
                return new List<string>();

            var languages = supportedLanguages
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim().ToLowerInvariant())
                .Where(l => !string.IsNullOrEmpty(l))
                .Select(ToSimpleDisplayName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(l => l)
                .ToList();

            if (!string.IsNullOrEmpty(globalLanguagePlaceholder))
            {
                languages = languages
                    .Where(l => !l.Equals(globalLanguagePlaceholder, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return languages;
        }
    }
}
