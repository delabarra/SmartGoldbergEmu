using System;
using System.Security;
using Microsoft.Win32;
using SmartGoldbergEmu.Constants;

namespace SmartGoldbergEmu.Services
{
    public static class UriProtocolRegistryService
    {
        private static string ProtocolProgIdKeyPath =>
            $@"{ApplicationConstants.UriProtocolCurrentUserClassesRegistryRoot}\{ApplicationConstants.UriProtocolScheme}";

        private static string ProtocolShellOpenCommandKeyPath =>
            $@"{ProtocolProgIdKeyPath}\{ApplicationConstants.UriProtocolRegistryShellOpenCommandSubKey}";

        public static bool IsProtocolRegistered()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(ProtocolProgIdKeyPath))
                {
                    if (key == null)
                        return false;

                    using (RegistryKey commandKey = Registry.CurrentUser.OpenSubKey(ProtocolShellOpenCommandKeyPath))
                    {
                        if (commandKey == null)
                            return false;

                        string registeredCommand = commandKey.GetValue("")?.ToString() ?? string.Empty;
                        string currentExePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        return registeredCommand.Contains(currentExePath);
                    }
                }
            }
            catch (SecurityException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool RegisterProtocol()
        {
            try
            {
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;

                using (RegistryKey protocolKey = Registry.CurrentUser.CreateSubKey(ProtocolProgIdKeyPath))
                {
                    if (protocolKey == null)
                        return false;

                    protocolKey.SetValue("", ApplicationConstants.UriProtocolRegistryUrlClassPrefix + ApplicationConstants.UriProtocolRegistrationFriendlyDescription);
                    protocolKey.SetValue(ApplicationConstants.UriProtocolRegistryUrlProtocolMarkerValueName, "");

                    using (RegistryKey iconKey = protocolKey.CreateSubKey(ApplicationConstants.UriProtocolRegistryDefaultIconSubKey))
                    {
                        iconKey?.SetValue("", $"\"{exePath}\",0");
                    }

                    using (RegistryKey shellKey = protocolKey.CreateSubKey(ApplicationConstants.UriProtocolRegistryShellOpenCommandSubKey))
                    {
                        shellKey?.SetValue("", $"\"{exePath}\" \"%1\"");
                    }
                }

                return true;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (SecurityException)
            {
                throw;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool UnregisterProtocol()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(ProtocolProgIdKeyPath, false);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (SecurityException)
            {
                throw;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}

