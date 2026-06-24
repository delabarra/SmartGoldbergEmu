using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    public class SteamApiKeyService
    {
        private const int ApiKeyLength = 32;
        private const string ValidationUrl = ApplicationConstants.SteamUserStatsSchemaApiUrlFormat;
        private const string ValidationLanguage = "english";
        private const string ValidationProbeAppId = "480";

        private readonly IRegistryService _registryService;

        public SteamApiKeyService() : this(null)
        {
        }

        public SteamApiKeyService(IRegistryService registryService)
        {
            _registryService = registryService ?? ServiceLocator.RegistryService;
        }

        public static bool IsValidApiKeyFormat(string apiKey)
        {
            return !string.IsNullOrWhiteSpace(apiKey)
                && apiKey.Length == ApiKeyLength
                && apiKey.IndexOf(' ') < 0
                && IsUpperAlphaNum(apiKey);
        }

        public string GetApiKey()
        {
            try
            {
                return _registryService.GetSteamApiKey() ?? string.Empty;
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to read Steam API key: {ex.Message}");
                return string.Empty;
            }
        }

        public bool HasApiKey() => !string.IsNullOrEmpty(GetApiKey());

        public bool HasValidFormat() => IsValidApiKeyFormat(GetApiKey());

        public bool TryGetValidFormatKey(out string apiKey)
        {
            apiKey = GetApiKey();
            return IsValidApiKeyFormat(apiKey);
        }

        public ValidationResult ValidateStoredKey()
        {
            string apiKey = GetApiKey();
            return string.IsNullOrEmpty(apiKey)
                ? ValidationResult.Failure("No API key is configured.")
                : ValidateApiKey(apiKey);
        }

        public ValidationResult ValidateKey(string apiKey) => ValidateApiKey(apiKey);

        public bool IsValidFormat(string apiKey) => IsValidApiKeyFormat(apiKey);

        public ValidationResult SetApiKey(string apiKey)
        {
            try
            {
                apiKey = apiKey?.Replace(" ", string.Empty).Trim() ?? string.Empty;

                if (!string.IsNullOrEmpty(apiKey) && !IsValidApiKeyFormat(apiKey))
                    return ValidationResult.Failure("API key must be exactly 32 alphanumeric uppercase characters (A-Z, 0-9).");

                var result = _registryService.SetSteamApiKey(apiKey);
                if (!result.IsValid)
                    return result;

                TryDeleteLegacyFile();
                return ValidationResult.Success();
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure($"Failed to set Steam API key: {ex.Message}");
            }
        }

        public ValidationResult RemoveApiKey() => SetApiKey(string.Empty);

        public ApiKeyStatus GetStatus()
        {
            string apiKey = GetApiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                return new ApiKeyStatus
                {
                    HasKey = false,
                    HasValidFormat = false,
                    IsValid = false,
                    ErrorMessage = "No API key is configured."
                };
            }

            if (!IsValidApiKeyFormat(apiKey))
            {
                return new ApiKeyStatus
                {
                    HasKey = true,
                    HasValidFormat = false,
                    IsValid = false,
                    ErrorMessage = "API key has invalid format (must be exactly 32 characters)."
                };
            }

            var vr = ValidateApiKey(apiKey);
            return new ApiKeyStatus
            {
                HasKey = true,
                HasValidFormat = true,
                IsValid = vr.IsValid,
                ErrorMessage = vr.ErrorMessage
            };
        }

        private static ValidationResult ValidateApiKey(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return ValidationResult.Failure("API key cannot be empty.");
            if (apiKey.Length != ApiKeyLength)
                return ValidationResult.Failure($"API key must be exactly {ApiKeyLength} characters long.");
            if (apiKey.IndexOf(' ') >= 0 || !IsUpperAlphaNum(apiKey))
                return ValidationResult.Failure("API key must contain only uppercase letters (A-Z) and numbers (0-9).");
            return ValidateWithSteamApi(apiKey);
        }

        private static bool IsUpperAlphaNum(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if ((c < 'A' || c > 'Z') && (c < '0' || c > '9'))
                    return false;
            }
            return true;
        }

        private static ValidationResult ValidateWithSteamApi(string apiKey)
        {
            try
            {
                // Avoid WinForms deadlock: sync callers (e.g. MainForm ctor) must not block the UI thread while HTTP
                // completions are posted to WindowsFormsSynchronizationContext.
                return ValidateWithSteamApiAsync(apiKey).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                string message = ex.Message ?? "Unknown error.";
                string escaped = Uri.EscapeDataString(apiKey);
                message = message
                    .Replace(apiKey, "***")
                    .Replace(escaped, "***");
                return ValidationResult.Failure($"Network error during validation: {message}");
            }
        }

        private static async Task<ValidationResult> ValidateWithSteamApiAsync(string apiKey)
        {
            string url = string.Format(
                ValidationUrl,
                ValidationLanguage,
                Uri.EscapeDataString(apiKey),
                ValidationProbeAppId);
            using (var http = HttpServiceFactory.Create(TimeSpan.FromSeconds(5)))
            {
                using (var response = await http.GetAsync(url).ConfigureAwait(false))
                {
                    if (response.StatusCode == HttpStatusCode.Forbidden)
                        return ValidationResult.Failure("API key is invalid or has been revoked.");
                    if (response.IsSuccessStatusCode)
                        return ValidationResult.Success();
                    return ValidationResult.Failure("API key validation failed: Unexpected response from Steam.");
                }
            }
        }

        private void TryDeleteLegacyFile()
        {
            try
            {
                string path = PathConstants.LegacyApiKeyFilePath;
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to delete legacy {PathConstants.LegacyApiKeyFileName}: {ex.Message}");
            }
        }
    }
}
