using System.Runtime.InteropServices;

namespace SmartGoldbergEmu.LanKit
{
    // Layout matches Steam FriendGameInfo_t with VALVE_CALLBACK_PACK_SMALL (Windows).
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct FriendGameInfoNative
    {
        public ulong GameId;
        public uint GameIp;
        public ushort GamePort;
        public ushort QueryPort;
        public ulong SteamIdLobby;
    }
}
