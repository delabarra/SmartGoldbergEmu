using System;
using System.Net;
using SmartGoldbergEmu.JsonKit;

namespace SmartGoldbergEmu.Models
{
    public sealed class LanChatPacketEventArgs : EventArgs
    {
        public LanChatPacketEventArgs(JsonObject root, IPEndPoint remote)
        {
            Root = root;
            Remote = remote;
        }

        public JsonObject Root { get; }
        public IPEndPoint Remote { get; }
    }
}
