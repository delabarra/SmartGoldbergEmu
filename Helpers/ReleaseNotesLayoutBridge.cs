using System;
using System.Runtime.InteropServices;

namespace SmartGoldbergEmu.Helpers
{
    [ComVisible(true)]
    public class ReleaseNotesLayoutBridge
    {
        private readonly Action _onLayoutChanged;

        public ReleaseNotesLayoutBridge(Action onLayoutChanged)
        {
            _onLayoutChanged = onLayoutChanged;
        }

        public void NotifyLayoutChanged()
        {
            _onLayoutChanged?.Invoke();
        }
    }
}
