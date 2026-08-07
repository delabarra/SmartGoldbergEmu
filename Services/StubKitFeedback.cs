using System;
using System.Windows.Forms;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    // User-facing text for built-in SteamStub removal (StubKit).
    public static class StubKitFeedback
    {
        public const string DialogTitle = "SteamStub";

        public static string Progress(string gameName)
        {
            return "SteamStub: " + FormatGameName(gameName) + "...";
        }

        public static string CheckingProgress(string gameName)
        {
            return "Checking SteamStub: " + FormatGameName(gameName) + "...";
        }

        public static string RestoreProgress(string gameName)
        {
            return "SteamStub restore: " + FormatGameName(gameName) + "...";
        }

        public static string OfferRemoveQuestion(string gameName, string executableFileName)
        {
            string name = FormatGameName(gameName);
            string fileName = string.IsNullOrWhiteSpace(executableFileName)
                ? null
                : executableFileName.Trim();

            string message = "SteamStub was detected on " + name + ".";
            if (!string.IsNullOrEmpty(fileName))
                message += Environment.NewLine + "File: " + fileName;

            return message
                + Environment.NewLine
                + Environment.NewLine
                + "Would you like to remove it?"
                + Environment.NewLine
                + Environment.NewLine
                + "Note:"
                + Environment.NewLine
                + "Useful if you get \"Application load error #:0000065432\".";
        }

        public static string ResultMessage(StubKitApplyOutcome outcome, string gameName)
        {
            string name = FormatGameName(gameName);
            switch (outcome)
            {
                case StubKitApplyOutcome.Success:
                    return "SteamStub removed for " + name + ".";
                case StubKitApplyOutcome.Restored:
                    return "Original executable restored for " + name + ".";
                case StubKitApplyOutcome.NoStubFound:
                    return "No SteamStub found on " + name + ".";
                case StubKitApplyOutcome.CannotRemove:
                    return "SteamStub on " + name + " was detected but cannot be removed by this build.";
                case StubKitApplyOutcome.UnpackFailed:
                    return "Could not unpack SteamStub on " + name + ".";
                case StubKitApplyOutcome.FileReplaceFailed:
                    return "SteamStub was unpacked for " + name + " but the executable could not be replaced. Close the game and try again.";
                case StubKitApplyOutcome.BackupMissing:
                    return "Could not restore " + name + ". The original backup executable was not found.";
                case StubKitApplyOutcome.RestoreFailed:
                    return "Could not restore the original executable for " + name + ". Close the game and try again.";
                case StubKitApplyOutcome.ExecutablePathInvalid:
                    return "Could not change SteamStub on " + name + ". Set a valid executable in Properties.";
                case StubKitApplyOutcome.Unexpected:
                    return "An unexpected error occurred while processing SteamStub on " + name + ".";
                default:
                    return "SteamStub finished for " + name + ".";
            }
        }

        public static MessageBoxIcon IconForOutcome(StubKitApplyOutcome outcome)
        {
            switch (outcome)
            {
                case StubKitApplyOutcome.Success:
                case StubKitApplyOutcome.Restored:
                    return MessageBoxIcon.Information;
                case StubKitApplyOutcome.NoStubFound:
                case StubKitApplyOutcome.ExecutablePathInvalid:
                case StubKitApplyOutcome.BackupMissing:
                    return MessageBoxIcon.Warning;
                default:
                    return MessageBoxIcon.Error;
            }
        }

        public static TaskReportKind StatusKindForOutcome(StubKitApplyOutcome outcome)
        {
            switch (outcome)
            {
                case StubKitApplyOutcome.Success:
                case StubKitApplyOutcome.Restored:
                    return TaskReportKind.Info;
                case StubKitApplyOutcome.ExecutablePathInvalid:
                case StubKitApplyOutcome.NoStubFound:
                case StubKitApplyOutcome.BackupMissing:
                    return TaskReportKind.Warning;
                default:
                    return TaskReportKind.Error;
            }
        }

        public static string FormatGameName(string gameName)
        {
            return string.IsNullOrWhiteSpace(gameName) ? "game" : gameName.Trim();
        }
    }
}
