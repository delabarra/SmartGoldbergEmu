using System;

namespace SmartGoldbergEmu.StubKit
{
    public sealed class UnpackOptions
    {
        public bool KeepBind;
        // Zero IMAGE_DIRECTORY_ENTRY_SECURITY (Authenticode). Default true.
        public bool ClearSecurity = true;

        public static UnpackOptions Default => new UnpackOptions();
    }

    public enum BindAction
    {
        Dropped,
        KeptRequested,
        KeptImportsInBind,
        DroppedImportsRelocated
    }

    // Post-decrypt PE fixes: security dir, .bind drop/keep, import relocate.
    internal static class BindFinish
    {
        public static BindAction Apply(PeImage pe, PeImage.Section bind, UnpackOptions options)
        {
            if (options == null)
                options = new UnpackOptions();
            if (options.ClearSecurity)
                pe.ClearSecurityDirectory();

            var action = BindAction.Dropped;
            if (bind != null)
            {
                if (options.KeepBind)
                {
                    pe.RemoveTlsCallbacksInSection(bind);
                    action = BindAction.KeptRequested;
                }
                else
                {
                    uint impRva, impSize;
                    pe.GetDataDirectory(1, out impRva, out impSize);
                    bool importsInBind = pe.SectionContainsRva(bind, impRva);
                    if (importsInBind)
                    {
                        if (pe.TryRelocateImportDirectoryOutOfSection(bind))
                        {
                            pe.RemoveBindSection(bind);
                            action = BindAction.DroppedImportsRelocated;
                        }
                        else
                        {
                            pe.RemoveTlsCallbacksInSection(bind);
                            action = BindAction.KeptImportsInBind;
                        }
                    }
                    else
                    {
                        pe.RemoveBindSection(bind);
                        action = BindAction.Dropped;
                    }
                }
            }

            pe.WriteHeaders();
            return action;
        }

        public static string Describe(BindAction action)
        {
            switch (action)
            {
                case BindAction.KeptRequested:
                    return ".bind kept";
                case BindAction.KeptImportsInBind:
                    return ".bind kept (import directory lives there)";
                case BindAction.DroppedImportsRelocated:
                    return "imports relocated + .bind dropped";
                default:
                    return ".bind dropped";
            }
        }
    }
}
