using System;
using System.Collections.Generic;
using System.Linq;
using AppDataKit;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Helpers
{
    // AppDataKit counterpart to SteamPicsKeyValueHelper, which reads SteamKit KeyValue trees.
    public static class AppInfoKeyValueHelper
    {
        public static AppInfoKeyValue FindChild(AppInfoKeyValue parent, string name)
        {
            return parent?.GetChild(name);
        }

        // PICS-recovered roots may be wrapped in an appinfo node.
        public static AppInfoKeyValue ResolveAppInfoTarget(AppInfoKeyValue root)
        {
            if (root == null)
                return null;
            AppInfoKeyValue appinfo = FindChild(root, SteamPicsKeyNames.AppInfo);
            if (appinfo != null && appinfo.Children.Count > 0)
                return appinfo;
            return root;
        }

        public static bool TryGetAppDisplayInfo(AppInfoKeyValue root, out string name, out string type)
        {
            name = null;
            type = null;
            AppInfoKeyValue target = ResolveAppInfoTarget(root);
            if (target == null)
                return false;
            AppInfoKeyValue common = FindChild(target, PathConstants.SteamAppsCommonDirectoryName);
            if (common == null)
                return false;
            AppInfoKeyValue nameNode = FindChild(common, SteamPicsKeyNames.Name);
            if (nameNode == null || string.IsNullOrWhiteSpace(nameNode.Value))
                return false;
            name = nameNode.Value.Trim();
            AppInfoKeyValue typeNode = FindChild(common, SteamPicsKeyNames.Type);
            if (typeNode != null && !string.IsNullOrWhiteSpace(typeNode.Value))
                type = typeNode.Value.Trim();
            return true;
        }

        public static void PopulateMetadataFromAppRoot(AppInfoKeyValue root, OnlineAppData metadata)
        {
            if (root == null || metadata == null)
                return;

            AppInfoKeyValue target = ResolveAppInfoTarget(root);
            if (target == null)
                return;

            AppInfoKeyValue common = FindChild(target, PathConstants.SteamAppsCommonDirectoryName);
            if (common != null)
            {
                AppInfoKeyValue nameNode = FindChild(common, SteamPicsKeyNames.Name);
                if (nameNode != null && !string.IsNullOrEmpty(nameNode.Value))
                    metadata.Name = nameNode.Value.Trim();

                AppInfoKeyValue typeNode = FindChild(common, SteamPicsKeyNames.Type);
                if (typeNode != null && !string.IsNullOrWhiteSpace(typeNode.Value))
                    metadata.Type = typeNode.Value.Trim();

                var languageList = new List<string>();
                AppInfoKeyValue supportedLangNode = FindChild(common, SteamPicsKeyNames.SupportedLanguages);
                if (supportedLangNode != null)
                {
                    if (supportedLangNode.Children != null && supportedLangNode.Children.Count > 0)
                    {
                        foreach (AppInfoKeyValue lang in supportedLangNode.Children)
                        {
                            if (lang == null || string.IsNullOrEmpty(lang.Name))
                                continue;
                            bool isSupported = true;
                            AppInfoKeyValue supportedNode = FindChild(lang, SteamPicsKeyNames.Supported);
                            if (supportedNode != null && !string.IsNullOrEmpty(supportedNode.Value))
                            {
                                string supportedValue = supportedNode.Value;
                                isSupported = supportedValue == "1" || string.Equals(supportedValue, "true", StringComparison.OrdinalIgnoreCase);
                            }
                            if (isSupported)
                                languageList.Add(lang.Name);
                        }
                    }
                    else if (!string.IsNullOrEmpty(supportedLangNode.Value))
                    {
                        List<string> languages = supportedLangNode.Value.Split(',')
                            .Select(lang => lang.Trim())
                            .Where(lang => !string.IsNullOrEmpty(lang))
                            .ToList();
                        languageList.AddRange(languages);
                    }
                }
                else
                {
                    AppInfoKeyValue langNode2 = FindChild(common, SteamPicsKeyNames.Languages);
                    if (langNode2?.Children != null)
                    {
                        foreach (AppInfoKeyValue lang in langNode2.Children)
                        {
                            if (lang != null && !string.IsNullOrEmpty(lang.Name))
                                languageList.Add(lang.Name);
                        }
                    }
                }

                if (languageList.Count > 0)
                    metadata.SupportedLanguages = string.Join(",", languageList);
            }

            var dlcIds = new List<long>();
            CollectDlcIdsFromAppRoot(root, dlcIds);
            metadata.DlcIds = dlcIds.Count > 0 ? dlcIds : new List<long>();

            metadata.Success = true;

            if (TryGetSteamInstallDirFolderName(root, out string installDirFolder))
                metadata.InstallDir = installDirFolder;
        }

        // config/installdir is the folder name under steamapps/common.
        public static bool TryGetSteamInstallDirFolderName(AppInfoKeyValue root, out string installDirFolderName)
        {
            installDirFolderName = null;
            AppInfoKeyValue target = ResolveAppInfoTarget(root);
            if (target == null)
                return false;
            AppInfoKeyValue config = FindChild(target, SteamPicsKeyNames.Config);
            if (config == null)
                return false;
            AppInfoKeyValue installdir = FindChild(config, SteamPicsKeyNames.InstallDir);
            if (installdir == null || string.IsNullOrWhiteSpace(installdir.Value))
                return false;
            installDirFolderName = installdir.Value.Trim();
            return installDirFolderName.Length > 0;
        }

        public static void CollectDlcIdsFromAppRoot(AppInfoKeyValue root, IList<long> dlcIds)
        {
            if (root == null || dlcIds == null)
                return;
            AppInfoKeyValue target = ResolveAppInfoTarget(root);
            if (target == null)
                return;
            AppInfoKeyValue common = FindChild(target, PathConstants.SteamAppsCommonDirectoryName);
            if (common != null)
            {
                AppInfoKeyValue dlc = FindChild(common, SteamPicsKeyNames.Dlc);
                if (dlc != null)
                    ExtractDlcIdsFromDlcKeyValue(dlc, dlcIds);
            }
            AppInfoKeyValue extended = FindChild(target, SteamPicsKeyNames.Extended);
            if (extended != null)
            {
                AppInfoKeyValue extDlc = FindChild(extended, SteamPicsKeyNames.Dlc);
                if (extDlc != null)
                    ExtractDlcIdsFromDlcKeyValue(extDlc, dlcIds);
                AppInfoKeyValue list = FindChild(extended, SteamPicsKeyNames.ListOfDlc);
                if (list != null)
                    ExtractDlcIdsFromDlcKeyValue(list, dlcIds);
            }
        }

        public static void ExtractDlcIdsFromDlcKeyValue(AppInfoKeyValue dlcNode, IList<long> dlcIds)
        {
            if (dlcNode == null || dlcIds == null)
                return;
            var seenIds = new HashSet<long>();
            foreach (long id in dlcIds)
                seenIds.Add(id);

            if (dlcNode.Children != null && dlcNode.Children.Count > 0)
            {
                foreach (AppInfoKeyValue child in dlcNode.Children)
                {
                    if (child == null || string.IsNullOrEmpty(child.Name))
                        continue;
                    if (long.TryParse(child.Name, out long dlcId) && dlcId > 0 && seenIds.Add(dlcId))
                        dlcIds.Add(dlcId);
                }
            }
            else if (!string.IsNullOrEmpty(dlcNode.Value))
            {
                string value = dlcNode.Value;
                string[] parts = value.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string part in parts)
                {
                    string trimmed = part.Trim();
                    if (long.TryParse(trimmed, out long dlcId) && dlcId > 0 && seenIds.Add(dlcId))
                        dlcIds.Add(dlcId);
                }
            }
        }
    }
}
