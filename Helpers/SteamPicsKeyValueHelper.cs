using System;
using System.Collections.Generic;
using System.Linq;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;
using SteamKit;

namespace SmartGoldbergEmu.Helpers
{
    public static class SteamPicsKeyValueHelper
    {
        public static KeyValue FindChild(KeyValue parent, string name)
        {
            if (parent?.Children == null || string.IsNullOrEmpty(name))
                return null;
            foreach (KeyValue c in parent.Children)
            {
                if (c != null && string.Equals(c.Name, name, StringComparison.Ordinal))
                    return c;
            }
            return null;
        }

        // PICS roots may be wrapped in an appinfo node.
        public static KeyValue ResolveAppInfoTarget(KeyValue root)
        {
            if (root == null)
                return null;
            KeyValue appinfo = FindChild(root, SteamPicsKeyNames.AppInfo);
            if (appinfo?.Children != null && appinfo.Children.Count > 0)
                return appinfo;
            return root;
        }

        public static bool TryGetAppDisplayInfo(KeyValue root, out string name, out string type)
        {
            name = null;
            type = null;
            KeyValue target = ResolveAppInfoTarget(root);
            if (target?.Children == null)
                return false;
            KeyValue common = FindChild(target, PathConstants.SteamAppsCommonDirectoryName);
            if (common?.Children == null)
                return false;
            KeyValue nameNode = FindChild(common, SteamPicsKeyNames.Name);
            if (nameNode == null || string.IsNullOrWhiteSpace(nameNode.Value))
                return false;
            name = nameNode.Value.Trim();
            KeyValue typeNode = FindChild(common, SteamPicsKeyNames.Type);
            if (typeNode != null && !string.IsNullOrWhiteSpace(typeNode.Value))
                type = typeNode.Value.Trim();
            return true;
        }

        public static bool TryGetAppType(KeyValue root, out string type)
        {
            type = null;
            KeyValue target = ResolveAppInfoTarget(root);
            if (target?.Children == null)
                return false;
            KeyValue common = FindChild(target, PathConstants.SteamAppsCommonDirectoryName);
            KeyValue typeNode = FindChild(common, SteamPicsKeyNames.Type);
            if (typeNode == null || string.IsNullOrWhiteSpace(typeNode.Value))
                return false;
            type = typeNode.Value.Trim();
            return true;
        }

        public static void PopulateMetadataFromAppRoot(KeyValue root, OnlineAppData metadata)
        {
            if (root == null || metadata == null)
                return;

            KeyValue target = ResolveAppInfoTarget(root);
            if (target?.Children == null)
                return;

            KeyValue common = FindChild(target, PathConstants.SteamAppsCommonDirectoryName);
            if (common?.Children != null)
            {
                KeyValue nameNode = FindChild(common, SteamPicsKeyNames.Name);
                if (nameNode != null && !string.IsNullOrEmpty(nameNode.Value))
                    metadata.Name = nameNode.Value.Trim();

                KeyValue typeNode = FindChild(common, SteamPicsKeyNames.Type);
                if (typeNode != null && !string.IsNullOrWhiteSpace(typeNode.Value))
                    metadata.Type = typeNode.Value.Trim();

                var languageList = new List<string>();
                KeyValue supportedLangNode = FindChild(common, SteamPicsKeyNames.SupportedLanguages);
                if (supportedLangNode != null)
                {
                    if (supportedLangNode.Children != null && supportedLangNode.Children.Count > 0)
                    {
                        foreach (KeyValue lang in supportedLangNode.Children)
                        {
                            if (lang == null || string.IsNullOrEmpty(lang.Name))
                                continue;
                            bool isSupported = true;
                            if (lang.Children != null)
                            {
                                KeyValue supportedNode = FindChild(lang, SteamPicsKeyNames.Supported);
                                if (supportedNode != null && !string.IsNullOrEmpty(supportedNode.Value))
                                {
                                    string supportedValue = supportedNode.Value;
                                    isSupported = supportedValue == "1" || string.Equals(supportedValue, "true", StringComparison.OrdinalIgnoreCase);
                                }
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
                    KeyValue langNode2 = FindChild(common, SteamPicsKeyNames.Languages);
                    if (langNode2?.Children != null)
                    {
                        foreach (KeyValue lang in langNode2.Children)
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
        public static bool TryGetSteamInstallDirFolderName(KeyValue root, out string installDirFolderName)
        {
            installDirFolderName = null;
            KeyValue target = ResolveAppInfoTarget(root);
            if (target?.Children == null)
                return false;
            KeyValue config = FindChild(target, SteamPicsKeyNames.Config);
            if (config?.Children == null)
                return false;
            KeyValue installdir = FindChild(config, SteamPicsKeyNames.InstallDir);
            if (installdir == null || string.IsNullOrWhiteSpace(installdir.Value))
                return false;
            installDirFolderName = installdir.Value.Trim();
            return installDirFolderName.Length > 0;
        }

        public static void CollectDlcIdsFromAppRoot(KeyValue root, IList<long> dlcIds)
        {
            if (root == null || dlcIds == null)
                return;
            KeyValue target = ResolveAppInfoTarget(root);
            if (target?.Children == null)
                return;
            KeyValue common = FindChild(target, PathConstants.SteamAppsCommonDirectoryName);
            if (common?.Children != null)
            {
                KeyValue dlc = FindChild(common, SteamPicsKeyNames.Dlc);
                if (dlc != null)
                    ExtractDlcIdsFromDlcKeyValue(dlc, dlcIds);
            }
            KeyValue extended = FindChild(target, SteamPicsKeyNames.Extended);
            if (extended?.Children != null)
            {
                KeyValue extDlc = FindChild(extended, SteamPicsKeyNames.Dlc);
                if (extDlc != null)
                    ExtractDlcIdsFromDlcKeyValue(extDlc, dlcIds);
                KeyValue list = FindChild(extended, SteamPicsKeyNames.ListOfDlc);
                if (list != null)
                    ExtractDlcIdsFromDlcKeyValue(list, dlcIds);
            }
        }

        public static void ExtractDlcIdsFromDlcKeyValue(KeyValue dlcNode, IList<long> dlcIds)
        {
            if (dlcNode == null || dlcIds == null)
                return;
            var seenIds = new HashSet<long>();
            foreach (long id in dlcIds)
                seenIds.Add(id);

            if (dlcNode.Children != null && dlcNode.Children.Count > 0)
            {
                foreach (KeyValue child in dlcNode.Children)
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
