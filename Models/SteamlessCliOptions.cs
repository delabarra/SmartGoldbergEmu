using System.Collections.Generic;
using System.Text;
using SmartGoldbergEmu.Constants;

namespace SmartGoldbergEmu.Models
{
    /// <summary>
    /// Selected Steamless.CLI.exe switches (always includes --quiet).
    /// </summary>
    public sealed class SteamlessCliOptions
    {
        public static SteamlessCliOptions Default => CreateDefault();

        public static SteamlessCliOptions CreateDefault()
        {
            return new SteamlessCliOptions
            {
                RealignSections = true,
                RecalculateChecksum = true
            };
        }

        public bool KeepBindSection { get; set; }
        public bool KeepDosStub { get; set; }
        public bool DumpStubPayload { get; set; }
        public bool DumpSteamDrmpDll { get; set; }
        public bool RealignSections { get; set; }
        public bool RecalculateChecksum { get; set; }
        public bool UseExperimental { get; set; }

        public string BuildArguments(string executablePath)
        {
            var flags = new List<string> { PathConstants.SteamlessCliQuietFlag };
            if (KeepBindSection)
                flags.Add(PathConstants.SteamlessCliKeepBindFlag);
            if (KeepDosStub)
                flags.Add(PathConstants.SteamlessCliKeepStubFlag);
            if (DumpStubPayload)
                flags.Add(PathConstants.SteamlessCliDumpPayloadFlag);
            if (DumpSteamDrmpDll)
                flags.Add(PathConstants.SteamlessCliDumpDrmpFlag);
            if (RealignSections)
                flags.Add(PathConstants.SteamlessCliRealignFlag);
            if (RecalculateChecksum)
                flags.Add(PathConstants.SteamlessCliRecalcChecksumFlag);
            if (UseExperimental)
                flags.Add(PathConstants.SteamlessCliExperimentalFlag);

            var sb = new StringBuilder();
            for (int i = 0; i < flags.Count; i++)
            {
                if (i > 0)
                    sb.Append(' ');
                sb.Append(flags[i]);
            }

            sb.Append(" \"").Append(executablePath).Append('"');
            return sb.ToString();
        }
    }
}
