using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ENet;
using GtProxyMod.Commands;
using GtProxyMod.Core;
using GtProxyMod.Features;
using GtProxyMod.Protocol;
using GtProxyMod.State;

namespace GtProxyMod.Network
{
    public class ProxyEngine : IDisposable
    {
        public ProxyConfig Config { get; }
        public ProxyState State { get; }
        public CommandManager Commands { get; }
        public AutoclaveManager Autoclave { get; }
        public AutoSpamManager AutoSpam { get; }

        public string ActiveTargetIp { get; private set; } = "213.179.209.168";
        public ushort ActiveTargetPort { get; private set; } = 17203;

        private Host? _serverHost; // Receives connections from Growtopia Client
        private Host? _clientHost; // Connects to official Growtopia Server
        private Peer _clientPeer;
        private Peer _serverPeer;

        private CancellationTokenSource? _cts;
        private Task? _pollTask;
        private readonly object _sendLock = new();

        // Flood & Shadow Ban Protection Fields
        private DateTime _lastItemActivateTime = DateTime.MinValue;
        private DateTime _lastTileChangeTime = DateTime.MinValue;
        private DateTime _lastDialogReturnTime = DateTime.MinValue;
        private readonly object _throttleLock = new();

        public ProxyEngine(ProxyConfig config, ProxyState state, CommandManager commands)
        {
            Config = config;
            State = state;
            Commands = commands;
            Autoclave = new AutoclaveManager(this);
            AutoSpam = new AutoSpamManager(this);
            ActiveTargetIp = config.TargetHost;
            ActiveTargetPort = config.TargetPort;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();

            // 1. Initialize ENet Server Host to accept local client connection
            var address = new Address { Port = Config.ListenPort };

            _serverHost = new Host();
            _serverHost.Create(address, 32, 2);
            _serverHost.EnableGrowtopiaServer();

            // 2. Initialize ENet Client Host to communicate with official server
            _clientHost = new Host();
            _clientHost.Create(null, 32, 2);
            _clientHost.EnableGrowtopiaClient();

            Logger.Success($"Proxy ENet Dinleyicisi 0.0.0.0:{Config.ListenPort} üzerinde hazır (Range Coder + CRC32 + NewPacket aktif).");
            Logger.Info($"Varsayılan hedef Growtopia sunucusu: {ActiveTargetIp}:{ActiveTargetPort}");

            _pollTask = Task.Run(() => NetworkLoop(_cts.Token));
        }

        private void NetworkLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _serverHost != null && _clientHost != null)
            {
                bool polledWork = false;

                lock (_sendLock)
                {
                    // 1. Poll Server Host (From Client)
                    while (_serverHost.Service(0, out Event serverEvent) > 0)
                    {
                        polledWork = true;
                        HandleClientEvent(serverEvent);
                    }

                    // 2. Poll Client Host (From Server)
                    while (_clientHost.Service(0, out Event clientEvent) > 0)
                    {
                        polledWork = true;
                        HandleServerEvent(clientEvent);
                    }
                }

                if (!polledWork)
                {
                    Thread.Sleep(1);
                }
            }
        }

        #region Client Event Handling (Client -> Proxy)

        private void HandleClientEvent(Event netEvent)
        {
            switch (netEvent.Type)
            {
                case EventType.Connect:
                    _clientPeer = netEvent.Peer;
                    _clientPeer.Timeout(0, 15000, 30000);
                    State.IsClientConnected = true;
                    Logger.Success($"Growtopia İstemcisi bağlandı! (IP: {_clientPeer.IP}:{_clientPeer.Port})");

                    // Connect to target Growtopia Server
                    ConnectToTargetServer();
                    break;

                case EventType.Receive:
                    if (netEvent.Peer.NativeData != _clientPeer.NativeData)
                    {
                        netEvent.Packet.Dispose();
                        return;
                    }

                    byte[] data = new byte[netEvent.Packet.Length];
                    netEvent.Packet.CopyTo(data);
                    netEvent.Packet.Dispose();

                    HandleClientPacket(data);
                    break;

                case EventType.Disconnect:
                    if (netEvent.Peer.NativeData != _clientPeer.NativeData)
                    {
                        // Stale disconnect from previous peer during server transition
                        return;
                    }

                    Logger.Warning("Growtopia İstemcisi bağlantıyı kesti.");
                    State.IsClientConnected = false;
                    _clientPeer = default;
                    DisconnectFromServer();
                    break;
            }
        }

        private void ConnectToTargetServer()
        {
            // Clean up any existing server connection before connecting to a new target
            if (State.IsServerConnected || _serverPeer.NativeData != IntPtr.Zero)
            {
                try
                {
                    _serverPeer.DisconnectNow(0);
                }
                catch { }
                State.IsServerConnected = false;
            }

            if (State.PendingSubServerIp != null && State.PendingSubServerPort != null)
            {
                ActiveTargetIp = State.PendingSubServerIp;
                ActiveTargetPort = State.PendingSubServerPort.Value;
                State.PendingSubServerIp = null;
                State.PendingSubServerPort = null;
                Logger.Proxy($"Alt sunucuya yönlendiriliyor (Sub-Server): {ActiveTargetIp}:{ActiveTargetPort}");
            }
            else
            {
                ActiveTargetIp = Config.TargetHost;
                ActiveTargetPort = Config.TargetPort;
                Logger.Proxy($"Ana sunucuya bağlanılıyor (Master Server): {ActiveTargetIp}:{ActiveTargetPort}");
            }

            var targetAddress = new Address();
            targetAddress.SetHost(ActiveTargetIp);
            targetAddress.Port = ActiveTargetPort;

            _serverPeer = _clientHost!.Connect(targetAddress, 2, 0);
        }

        private void HandleClientPacket(byte[] data)
        {
            if (data.Length < 4) return;

            uint msgType = BitConverter.ToUInt32(data, 0);
            var netType = (NetMessageType)msgType;

            switch (netType)
            {
                case NetMessageType.GenericText:
                case NetMessageType.GameMessage:
                    var textPacket = TextPacket.FromBytes(data);
                    if (textPacket != null)
                    {
                        var raw = textPacket.RawText;

                        // Check login / name
                        var requestedName = textPacket.Get("requestedName");
                        if (!string.IsNullOrEmpty(requestedName))
                        {
                            State.LocalPlayerName = requestedName;
                            Logger.Info($"Giriş yapan oyuncu adı: {requestedName}");
                        }

                        // Check Chat Command
                        if (raw.Contains("action|input"))
                        {
                            var chatText = textPacket.Get("text");
                            if (string.IsNullOrEmpty(chatText))
                            {
                                var match = System.Text.RegularExpressions.Regex.Match(raw, @"(?:^|[\r\n|])text\|([^\r\n]+)");
                                if (match.Success)
                                    chatText = match.Groups[1].Value.Trim();
                            }

                            if (!string.IsNullOrEmpty(chatText) && Commands.HandleChatInput(chatText, this, State))
                            {
                                // Command was intercepted and handled locally! Do NOT forward to real server!
                                return;
                            }
                        }

                        // Autoclave Client Interceptor
                        if (Autoclave.HandleClientTextPacket(raw))
                        {
                            return;
                        }

                        if (Config.LogPackets && Config.LogTextPackets)
                        {
                            var preview = raw.Replace("\n", " \\n ").Trim();
                            if (preview.Length > 120) preview = preview.Substring(0, 117) + "...";
                            Logger.Client($"[{netType}] {preview}");
                        }
                    }
                    break;

                case NetMessageType.GamePacket:
                    var tank = TankPacket.FromBytes(data);
                    if (tank != null)
                    {
                        if (tank.Type == TankPacketType.State)
                        {
                            State.X = tank.Header.VecX;
                            State.Y = tank.Header.VecY;

                            // Ghost mode interceptor
                            if (State.GhostMode)
                            {
                                return;
                            }

                            if (Config.LogMovement)
                            {
                                Logger.Client($"[State/Movement] Pos: ({State.X:F0}, {State.Y:F0})");
                            }
                        }
                        else
                        {
                            if (Config.LogPackets)
                            {
                                Logger.Client($"[TankPacket] Type: {tank.Type} (NetID: {tank.Header.NetId})");
                            }
                        }
                    }
                    break;

                    case NetMessageType.Track:
                    case NetMessageType.ClientLogResponse:
                        Logger.Warning($"[CLIENT -> SERVER] [{netType} Telemetri Raporu Filtrelendi] Uzunluk: {data.Length} bayt");
                        return; // Sunucuya rapor/izleme gitmesini engelle

                    default:
                    if (Config.LogPackets)
                    {
                        Logger.Client($"[{netType}] Uzunluk: {data.Length} bayt");
                    }
                    break;
            }

            // Forward to server
            SendToServer(data);
        }

        #endregion

        #region Server Event Handling (Server -> Proxy)

        private void HandleServerEvent(Event netEvent)
        {
            switch (netEvent.Type)
            {
                case EventType.Connect:
                    _serverPeer = netEvent.Peer;
                    _serverPeer.Timeout(0, 15000, 30000);
                    State.IsServerConnected = true;
                    Logger.Success($"Resmi Growtopia sunucusuna başarıyla bağlandı! ({ActiveTargetIp}:{ActiveTargetPort})");
                    break;

                case EventType.Receive:
                    if (netEvent.Peer.NativeData != _serverPeer.NativeData)
                    {
                        netEvent.Packet.Dispose();
                        return;
                    }

                    byte[] data = new byte[netEvent.Packet.Length];
                    netEvent.Packet.CopyTo(data);
                    netEvent.Packet.Dispose();

                    HandleServerPacket(data);
                    break;

                case EventType.Disconnect:
                    if (netEvent.Peer.NativeData != _serverPeer.NativeData)
                    {
                        // Stale disconnect from older server peer during sub-server routing
                        return;
                    }

                    Logger.Warning($"Growtopia sunucusu bağlantıyı kesti ({ActiveTargetIp}:{ActiveTargetPort}).");
                    State.IsServerConnected = false;
                    _serverPeer = default;
                    break;
            }
        }

        private void HandleServerPacket(byte[] data)
        {
            if (data.Length < 4) return;

            uint msgType = BitConverter.ToUInt32(data, 0);
            var netType = (NetMessageType)msgType;

            switch (netType)
            {
                case NetMessageType.GamePacket:
                    var tank = TankPacket.FromBytes(data);
                    if (tank != null)
                    {
                        if (tank.Type == TankPacketType.CallFunction)
                        {
                            var vl = VariantList.Decode(tank.ExtendedData);
                            if (vl != null)
                            {
                                var func = vl.FunctionName;

                                // Sub-Server Redirection (CRUCIAL for official Growtopia!)
                                if (func.Equals("OnSendToServer", StringComparison.OrdinalIgnoreCase))
                                {
                                    int subPort = vl.Get(1)?.AsInt() ?? (int)Config.ListenPort;
                                    int token = vl.Get(2)?.AsInt() ?? 0;
                                    int userId = vl.Get(3)?.AsInt() ?? 0;
                                    string serverString = vl.Get(4)?.AsString() ?? string.Empty;

                                    var parts = serverString.Split('|');
                                    string targetIp = parts.Length > 0 ? parts[0] : Config.TargetHost;

                                    Logger.Success($"[Sub-Server Yönlendirmesi Yakalandı!] Hedef: {targetIp}:{subPort} (Token: {token})");

                                    // Save sub-server info for when client reconnects
                                    State.PendingSubServerIp = targetIp;
                                    State.PendingSubServerPort = (ushort)subPort;
                                    State.PendingSubServerToken = token;
                                    State.PendingSubServerUserId = userId;

                                    // Modify the VariantList to redirect client to proxy
                                    parts[0] = "127.0.0.1";
                                    string modifiedServerString = string.Join('|', parts);

                                    vl.Set(1, (int)Config.ListenPort);
                                    vl.Set(4, modifiedServerString);

                                    byte[] newExtData = vl.Encode();
                                    var modifiedTank = new TankPacket(tank.Header, newExtData);
                                    byte[] modifiedBytes = modifiedTank.ToBytes();

                                    Logger.Proxy($"OnSendToServer -> İstemciye yönlendirme: 127.0.0.1:{Config.ListenPort} (orijinal header korundu, ext boyut: {newExtData.Length})");

                                    SendToClient(modifiedBytes);
                                    return; // Handled!
                                }
                                else if (func.Equals("OnSpawn", StringComparison.OrdinalIgnoreCase))
                                {
                                    var spawnText = vl.Get(1)?.AsString() ?? string.Empty;
                                    var tp = new TextPacket(spawnText);
                                    var netIdStr = tp.Get("netID");
                                    var nameStr = tp.Get("name");
                                    if (int.TryParse(netIdStr, out var netId))
                                    {
                                        if (spawnText.Contains("type|local") || nameStr.Equals(State.LocalPlayerName, StringComparison.OrdinalIgnoreCase))
                                        {
                                            State.LocalPlayerNetId = netId;
                                            Logger.Success($"Yerel Oyuncu NetID tespit edildi: {netId}");
                                        }
                                        else if (!string.IsNullOrEmpty(nameStr))
                                        {
                                            State.WorldPlayers[netId] = nameStr;
                                        }
                                    }
                                }
                                else if (func.Equals("OnDialogRequest", StringComparison.OrdinalIgnoreCase))
                                {
                                    var dialogContent = vl.Get(1)?.AsString() ?? string.Empty;
                                    if (Autoclave.HandleServerDialogRequest(dialogContent))
                                    {
                                        return; // Intercepted and consumed by Autoclave! Do not forward to client.
                                    }
                                }
                                else if (func.Equals("OnConsoleMessage", StringComparison.OrdinalIgnoreCase) || func.Equals("OnTalkBubble", StringComparison.OrdinalIgnoreCase))
                                {
                                    var msg1 = vl.Get(1)?.AsString() ?? string.Empty;
                                    var msg2 = vl.Get(2)?.AsString() ?? string.Empty;
                                    var combined = msg1 + " " + msg2;

                                    if (combined.Contains("backpack space", StringComparison.OrdinalIgnoreCase) ||
                                        combined.Contains("need more backpack", StringComparison.OrdinalIgnoreCase) ||
                                        combined.Contains("inventory is full", StringComparison.OrdinalIgnoreCase) ||
                                        combined.Contains("backpack is full", StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (Autoclave.IsRunning)
                                        {
                                            Autoclave.HandleInventoryFull();
                                        }
                                    }
                                }
                                else if (func.Equals("OnTextOverlay", StringComparison.OrdinalIgnoreCase))
                                {
                                    var msg = vl.Get(1)?.AsString() ?? string.Empty;
                                    if (msg.Contains("backpack space", StringComparison.OrdinalIgnoreCase) ||
                                        msg.Contains("need more backpack", StringComparison.OrdinalIgnoreCase) ||
                                        msg.Contains("inventory is full", StringComparison.OrdinalIgnoreCase) ||
                                        msg.Contains("backpack is full", StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (Autoclave.IsRunning)
                                        {
                                            Autoclave.HandleInventoryFull();
                                        }
                                    }
                                }

                                if (Config.LogPackets && Config.LogVariantList)
                                {
                                    Logger.Server($"[VariantList] {func} -> {vl}");
                                }
                            }
                        }
                        else if (tank.Type == TankPacketType.SendMapData)
                        {
                            ParseWorldData(tank.ExtendedData);
                        }
                        else if (tank.Type != TankPacketType.State && Config.LogPackets)
                        {
                            Logger.Server($"[TankPacket] Type: {tank.Type}");
                        }
                    }
                    break;

                case NetMessageType.GenericText:
                case NetMessageType.GameMessage:
                    if (Config.LogPackets && Config.LogTextPackets)
                    {
                        var tp = TextPacket.FromBytes(data);
                        if (tp != null)
                        {
                            var preview = tp.RawText.Replace("\n", " \\n ").Trim();
                            if (preview.Length > 120) preview = preview.Substring(0, 117) + "...";
                            Logger.Server($"[{netType}] {preview}");
                        }
                    }
                    break;

                    case NetMessageType.Track:
                    case NetMessageType.ClientLogRequest:
                        Logger.Warning($"[SERVER -> PROXY] [{netType} Anti-Cheat Paketi Engellendi!] Uzunluk: {data.Length} bayt");
                        return; // İstemciye bu telemetri sorgusunu iletme!

                    default:
                    if (Config.LogPackets)
                    {
                        Logger.Server($"[{netType}] {data.Length} bayt");
                    }
                    break;
            }

            // Forward to client
            SendToClient(data);
        }

        private void ParseWorldData(byte[] extData)
        {
            if (extData == null || extData.Length < 10) return;

            try
            {
                short nameLen = BitConverter.ToInt16(extData, 6);
                if (nameLen > 0 && nameLen < 64 && extData.Length >= 8 + nameLen)
                {
                    string worldName = Encoding.UTF8.GetString(extData, 8, nameLen);
                    State.CurrentWorld = worldName;
                    State.WorldPlayers.Clear();
                    Autoclave.Stop("Dünya değişti");
                    AutoSpam.Stop();
                    Logger.Success($"Dünyaya giriş yapıldı: {worldName}");
                    SendClientOverlay($"Dünya: {worldName}");
                }
            }
            catch { }
        }

        #endregion

        #region Communication Helpers

        public void SendToClient(byte[] data, PacketFlags flags = PacketFlags.Reliable)
        {
            if (!State.IsClientConnected || _clientPeer.NativeData == IntPtr.Zero)
                return;

            lock (_sendLock)
            {
                try
                {
                    var packet = default(Packet);
                    packet.Create(data, flags);
                    _clientPeer.Send(0, ref packet);
                    _serverHost?.Flush();
                }
                catch { }
            }
        }

        private bool ShouldThrottlePacket(byte[] data)
        {
            if (data == null || data.Length < 4) return false;

            uint msgType = BitConverter.ToUInt32(data, 0);
            var now = DateTime.UtcNow;

           // 1. TankPacket Koruması (Yumruk / Tile Değişimi)
            if ((NetMessageType)msgType == NetMessageType.GamePacket && data.Length >= 5)
            {
                var packetType = (TankPacketType)data[4];

                lock (_throttleLock)
                {
                    if (packetType == TankPacketType.TileChangeReq)
                    {
                        if ((now - _lastTileChangeTime).TotalMilliseconds < Random.Shared.Next(180,250))
                            return true; // Seri vurma paketini filtrele
                        _lastTileChangeTime = now;
                    }
                }
            }

            // 2. Yalnızca dialog_return paketlerini sınırla.
            // action|drop normal oyuncu etkileşimidir; dialog zamanlayıcısıyla
            // aynı sayaçta tutulursa drop işlemi autoclave yanıtını, autoclave
            // yanıtı da drop işlemini yanlışlıkla engelleyebilir.
            else if ((NetMessageType)msgType == NetMessageType.GenericText ||
                     (NetMessageType)msgType == NetMessageType.GameMessage)
            {
                string text = Encoding.UTF8.GetString(data);
                if (text.Contains("action|dialog_return", StringComparison.Ordinal))
                {
                    lock (_throttleLock)
                    {
                        if ((now - _lastDialogReturnTime).TotalMilliseconds < Random.Shared.Next(400, 600))
                        {
                            return true;
                        }

                        _lastDialogReturnTime = now;
                    }
                }
            }

            return false;
        }

        public void SendToServer(byte[] data, PacketFlags flags = PacketFlags.Reliable)
        {
            if (!State.IsServerConnected || _serverPeer.NativeData == IntPtr.Zero)
                return;

            // Rate Limit Koruması: Shadow ban tetikleyecek seri paketleri filtrele
            if (ShouldThrottlePacket(data))
            {
                return;
            }

            lock (_sendLock)
            {
                try
                {
                    var packet = default(Packet);
                    packet.Create(data, flags);
                    _serverPeer.Send(0, ref packet);
                    _clientHost?.Flush();
                }
                catch { }
            }
        }

        public void SendClientMessage(string message)
        {
            var packet = PacketBuilder.BuildConsoleMessage(message);
            SendToClient(packet.ToBytes());
        }

        public void SendClientOverlay(string text)
        {
            var packet = PacketBuilder.BuildTextOverlay(text);
            SendToClient(packet.ToBytes());
        }

        public void SendClientDialog(string dialog)
        {
            var packet = PacketBuilder.BuildDialog(dialog);
            SendToClient(packet.ToBytes());
        }

        private void DisconnectFromServer()
        {
            if (_serverPeer.NativeData != IntPtr.Zero)
            {
                try
                {
                    _serverPeer.DisconnectNow(0);
                }
                catch { }
            }
            State.IsServerConnected = false;
        }

        #endregion

        public void Dispose()
        {
            _cts?.Cancel();
            try { _pollTask?.Wait(500); } catch { }

            _serverHost?.Dispose();
            _clientHost?.Dispose();
        }
    }
}