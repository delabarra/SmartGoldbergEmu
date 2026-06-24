using System;
using System.Collections.Generic;
using Microsoft.Win32;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    public class RegistryService : IRegistryService
    {
        private readonly string _registryKeyPath;

        private const string DefaultRegistryKeyPath = @"Software\" + PathConstants.LauncherPerUserFolderName;
        private const string ApiKeyValueName = "ApiKey";
        private const string SteamIdProfilesSubKeyName = "SteamIdProfiles";

        public RegistryService() : this(DefaultRegistryKeyPath)
        {
        }

        public RegistryService(string registryKeyPath)
        {
            _registryKeyPath = registryKeyPath ?? DefaultRegistryKeyPath;
        }

        public string GetSteamApiKey()
        {
            return ReadString(_registryKeyPath, ApiKeyValueName, "Failed to read Steam API key from registry");
        }

        public ValidationResult SetSteamApiKey(string apiKey)
        {
            try
            {
                apiKey = apiKey?.Replace(" ", string.Empty).Trim() ?? string.Empty;
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(_registryKeyPath, true))
                {
                    if (key == null)
                        return ValidationResult.Failure("Failed to access Windows Registry.");
                    if (string.IsNullOrEmpty(apiKey))
                        TryDeleteValue(key, ApiKeyValueName);
                    else
                        key.SetValue(ApiKeyValueName, apiKey, RegistryValueKind.String);
                    return ValidationResult.Success();
                }
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure($"Failed to save Steam API key to registry: {ex.Message}");
            }
        }

        public bool HasSteamApiKey()
        {
            return !string.IsNullOrEmpty(GetSteamApiKey());
        }

        public ValidationResult RemoveSteamApiKey()
        {
            return SetSteamApiKey(string.Empty);
        }

        public void DeleteRegistryKey()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKey(_registryKeyPath, false);
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to delete registry key: {ex.Message}");
            }
        }

        public ValidationResult SetBase64Token(ulong appId, string ticket, string altSteamId)
        {
            try
            {
                if (appId == 0)
                    return ValidationResult.Failure("App ID cannot be zero.");
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(Base64Path(appId), true))
                {
                    if (key == null)
                        return ValidationResult.Failure("Failed to access Windows Registry.");
                    SetTrimmedOrDelete(key, "ticket", ticket);
                    SetTrimmedOrDelete(key, "alt_steamid", altSteamId);
                    return ValidationResult.Success();
                }
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure($"Failed to save base64 token to registry: {ex.Message}");
            }
        }

        public string GetBase64Ticket(ulong appId)
        {
            if (appId == 0)
                return string.Empty;
            return ReadString(Base64Path(appId), "ticket", "Failed to read base64 ticket from registry");
        }

        public string GetBase64AltSteamId(ulong appId)
        {
            if (appId == 0)
                return string.Empty;
            return ReadString(Base64Path(appId), "alt_steamid", "Failed to read alt_steamid from registry");
        }

        private string Base64Path(ulong appId)
        {
            return $@"{_registryKeyPath}\Base64\{appId}";
        }

        private string SteamIdProfilesPath => $@"{_registryKeyPath}\{SteamIdProfilesSubKeyName}";

        public Dictionary<string, string> LoadSteamIdProfiles()
        {
            var profiles = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                using (RegistryKey parent = Registry.CurrentUser.OpenSubKey(SteamIdProfilesPath, false))
                {
                    if (parent == null)
                        return profiles;

                    foreach (string steamId in parent.GetSubKeyNames())
                    {
                        if (string.IsNullOrWhiteSpace(steamId))
                            continue;

                        using (RegistryKey entry = parent.OpenSubKey(steamId, false))
                        {
                            if (entry == null)
                                continue;

                            string name = entry.GetValue(string.Empty) as string;
                            if (string.IsNullOrWhiteSpace(name))
                                continue;

                            profiles[steamId.Trim()] = name.Trim();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to load SteamID profiles from registry: {ex.Message}");
            }

            return profiles;
        }

        public ValidationResult SaveSteamIdProfiles(Dictionary<string, string> profiles)
        {
            if (profiles == null)
                return ValidationResult.Failure("Profiles cannot be null");

            try
            {
                using (RegistryKey parent = Registry.CurrentUser.CreateSubKey(SteamIdProfilesPath, true))
                {
                    if (parent == null)
                        return ValidationResult.Failure("Failed to access Windows Registry.");

                    var keep = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var pair in profiles)
                    {
                        string steamId = pair.Key != null ? pair.Key.Trim() : string.Empty;
                        string name = pair.Value != null ? pair.Value.Trim() : string.Empty;
                        if (string.IsNullOrEmpty(steamId) || string.IsNullOrEmpty(name))
                            continue;

                        keep.Add(steamId);
                        WriteSteamIdProfileEntry(parent, steamId, name);
                    }

                    foreach (string existing in parent.GetSubKeyNames())
                    {
                        if (!keep.Contains(existing))
                            TryDeleteSubKey(parent, existing);
                    }
                }

                return ValidationResult.Success();
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure($"Failed to save SteamID profiles to registry: {ex.Message}");
            }
        }

        public ValidationResult UpsertSteamIdProfile(string steamId, string name)
        {
            steamId = steamId != null ? steamId.Trim() : string.Empty;
            name = name != null ? name.Trim() : string.Empty;

            if (string.IsNullOrEmpty(steamId))
                return ValidationResult.Failure("SteamID cannot be empty");
            if (string.IsNullOrEmpty(name))
                return ValidationResult.Failure("Profile name cannot be empty");

            try
            {
                using (RegistryKey parent = Registry.CurrentUser.CreateSubKey(SteamIdProfilesPath, true))
                {
                    if (parent == null)
                        return ValidationResult.Failure("Failed to access Windows Registry.");

                    WriteSteamIdProfileEntry(parent, steamId, name);
                }

                return ValidationResult.Success();
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure($"Failed to save SteamID profile to registry: {ex.Message}");
            }
        }

        public ValidationResult RemoveSteamIdProfile(string steamId)
        {
            steamId = steamId != null ? steamId.Trim() : string.Empty;
            if (string.IsNullOrEmpty(steamId))
                return ValidationResult.Failure("SteamID cannot be empty");

            try
            {
                using (RegistryKey parent = Registry.CurrentUser.OpenSubKey(SteamIdProfilesPath, true))
                {
                    if (parent == null)
                        return ValidationResult.Success();

                    TryDeleteSubKey(parent, steamId);
                }

                return ValidationResult.Success();
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure($"Failed to remove SteamID profile from registry: {ex.Message}");
            }
        }

        private static void WriteSteamIdProfileEntry(RegistryKey parent, string steamId, string name)
        {
            using (RegistryKey entry = parent.CreateSubKey(steamId, true))
            {
                if (entry == null)
                    throw new InvalidOperationException("Failed to create SteamID profile registry entry.");
                entry.SetValue(string.Empty, name, RegistryValueKind.String);
            }
        }

        private static void TryDeleteSubKey(RegistryKey parent, string subKeyName)
        {
            try
            {
                parent.DeleteSubKeyTree(subKeyName, false);
            }
            catch
            {
            }
        }

        private static string ReadString(string subPath, string valueName, string logPrefix)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(subPath, false))
                {
                    if (key == null)
                        return string.Empty;
                    object value = key.GetValue(valueName);
                    return value == null ? string.Empty : value.ToString();
                }
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"{logPrefix}: {ex.Message}");
                return string.Empty;
            }
        }

        private static void TryDeleteValue(RegistryKey key, string valueName)
        {
            try
            {
                key.DeleteValue(valueName, false);
            }
            catch
            {
            }
        }

        private static void SetTrimmedOrDelete(RegistryKey key, string valueName, string value)
        {
            if (string.IsNullOrEmpty(value))
                TryDeleteValue(key, valueName);
            else
                key.SetValue(valueName, value.Trim(), RegistryValueKind.String);
        }

    }
}
