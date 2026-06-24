using System;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    // User-facing text for Steamless (launcher wording, not raw CLI output).
    public static class SteamlessFeedback
    {
        public const string DialogTitle = "Steamless";

        public static string Progress(string gameName)
        {
            return "Steamless: " + FormatGameName(gameName) + "...";
        }

        public static string StatusMessage(SteamlessApplyOutcome outcome, string gameName)
        {
            string name = FormatGameName(gameName);
            switch (outcome)
            {
                case SteamlessApplyOutcome.Success:
                    return "Steamless finished for " + name + ".";
                case SteamlessApplyOutcome.AlreadyApplied:
                    return "Steamless was already applied to " + name + ".";
                case SteamlessApplyOutcome.CannotProcess:
                    return "Steamless could not process " + name + ". The executable may not use SteamStub, or another app may be using the file.";
                case SteamlessApplyOutcome.UnpackedFileMissing:
                    return "Steamless did not finish for " + name + ". The unpacked file was not created.";
                case SteamlessApplyOutcome.FileReplaceFailed:
                    return "Steamless unpacked " + name + " but could not replace the executable. Close the game and try again.";
                case SteamlessApplyOutcome.ExecutablePathInvalid:
                    return "Steamless could not run on " + name + ". Set a valid executable in Properties.";
                default:
                    return null;
            }
        }

        public static string PopupMessage(SteamlessApplyOutcome outcome, string gameName, string logDetail)
        {
            switch (outcome)
            {
                case SteamlessApplyOutcome.NotInstalled:
                    return NotInstalledPopupBody();
                case SteamlessApplyOutcome.Unexpected:
                    return UnexpectedPopupBody(gameName, logDetail);
                default:
                    return null;
            }
        }

        public static TaskReportKind StatusKindForOutcome(SteamlessApplyOutcome outcome)
        {
            switch (outcome)
            {
                case SteamlessApplyOutcome.Success:
                    return TaskReportKind.Info;
                case SteamlessApplyOutcome.AlreadyApplied:
                    return TaskReportKind.Warning;
                case SteamlessApplyOutcome.ExecutablePathInvalid:
                    return TaskReportKind.Warning;
                default:
                    return TaskReportKind.Error;
            }
        }

        public static bool UsePopupForOutcome(SteamlessApplyOutcome outcome)
        {
            return outcome == SteamlessApplyOutcome.NotInstalled
                || outcome == SteamlessApplyOutcome.Unexpected;
        }

        public static string NotConfiguredDisclaimerBody()
        {
            return "SmartGoldbergEmu does not include Steamless."
                + Environment.NewLine + Environment.NewLine
                + "You may install Steamless separately and choose Steamless.CLI.exe when prompted (the folder must include a Plugins folder).";
        }

        public static string NotInstalledPopupBody()
        {
            return "The saved Steamless path is missing or invalid. Use Steamless again to choose Steamless.CLI.exe.";
        }

        public static string InvalidSteamlessInstallBody()
        {
            return "The selected file is not a valid Steamless install. Choose Steamless.CLI.exe from the folder where you extracted Steamless (it must include a Plugins folder).";
        }

        private static string UnexpectedPopupBody(string gameName, string logDetail)
        {
            string name = FormatGameName(gameName);
            if (string.IsNullOrWhiteSpace(logDetail))
                return "An unexpected error occurred during Steamless for " + name + ".";

            return "An unexpected error occurred during Steamless for " + name + "." + Environment.NewLine + Environment.NewLine + logDetail;
        }

        public static string FormatGameName(string gameName)
        {
            return string.IsNullOrWhiteSpace(gameName) ? "game" : gameName.Trim();
        }

        public static string CleanCliLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            string text = line.Trim();
            while (text.Length > 0 && text[0] == '[')
            {
                int close = text.IndexOf(']');
                if (close < 0)
                    break;
                text = text.Substring(close + 1).TrimStart();
            }

            return text.Length > 0 ? text : null;
        }
    }
}
