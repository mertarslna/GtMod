using System;
using System.Collections.Concurrent;

namespace GtProxyMod.State
{
    public class ProxyState
    {
        public bool IsClientConnected { get; set; }
        public bool IsServerConnected { get; set; }

        public string CurrentWorld { get; set; } = "None";
        public int LocalPlayerNetId { get; set; } = -1;
        public int LocalPlayerUserId { get; set; } = -1;
        public string LocalPlayerName { get; set; } = string.Empty;

        public float X { get; set; }
        public float Y { get; set; }

        // Mod Features
        public bool GhostMode { get; set; } = false;
        public bool PacketLogging { get; set; } = true;
        public bool MovementLogging { get; set; } = false;
        public bool InfinitePunch { get; set; } = false;

        // Sub-Server Redirection Handshake
        public string? PendingSubServerIp { get; set; }
        public ushort? PendingSubServerPort { get; set; }
        public int? PendingSubServerToken { get; set; }
        public int? PendingSubServerUserId { get; set; }

        public ConcurrentDictionary<int, string> WorldPlayers { get; } = new();

        public void Reset()
        {
            CurrentWorld = "None";
            LocalPlayerNetId = -1;
            WorldPlayers.Clear();
        }
    }
}
