using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GtProxyMod.Core;
using GtProxyMod.Network;
using GtProxyMod.Protocol;

namespace GtProxyMod.Features
{
    public class AutoFarmManager
    {
        private readonly ProxyEngine _engine;
        private CancellationTokenSource? _cts;

        // Sunucudan gelen son tile durumu: (x, y) -> itemId
        private readonly ConcurrentDictionary<(int, int), int> _tileState = new();

        private const int FistId = 18;
        private const int MaxPlaceAttempts = 5;
        private const int MaxBreakHits = 15;

        public bool IsRunning { get; private set; }
        public int ActiveBlockId { get; private set; }

        public AutoFarmManager(ProxyEngine engine)
        {
            _engine = engine;
        }

        public void Toggle(int blockId)
        {
            if (IsRunning && ActiveBlockId == blockId)
            {
                Stop("Kullanıcı tarafından kapatıldı");
            }
            else
            {
                if (IsRunning) Stop("Yeni ID ile yeniden başlatılıyor");
                Start(blockId);
            }
        }

        public void Start(int blockId)
        {
            if (!_engine.State.IsServerConnected)
            {
                _engine.SendClientMessage("`4[Hata]`o Sunucuya bağlı değilsiniz!");
                return;
            }

            if (_engine.State.LocalPlayerNetId == 0 || _engine.State.X == 0)
            {
                _engine.SendClientMessage("`4[AutoFarm]`o Konum veya NetID alınamadı. Oyunda 1 adım hareket edin!");
                return;
            }

            _tileState.Clear();

            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            IsRunning = true;
            ActiveBlockId = blockId;
            _engine.SendClientMessage($"`2[#AUTOFARM]`o Başlatıldı! Blok/Seed ID: `{blockId}`");

            Task.Run(() => FarmLoop(blockId, token));
        }

        public void Stop(string reason = "")
        {
            if (!IsRunning) return;

            IsRunning = false;
            ActiveBlockId = 0;
            _cts?.Cancel();
            _engine.SendClientMessage($"`4[#AUTOFARM]`o Durduruldu. ({reason})");
        }

        // ProxyEngine, sunucudan tile paketi alınca bunu çağırır
        public void OnServerTilePacket(int x, int y, int itemId)
        {
            Logger.Proxy($"[SERVER TILE] ({x},{y}) item={itemId}"); // test bitince silebilirsin
            _tileState[(x, y)] = itemId;
        }

        private async Task<bool> WaitForTile(int x, int y, Func<int, bool> condition, int timeoutMs, CancellationToken token)
        {
            int waited = 0;
            while (waited < timeoutMs)
            {
                if (_tileState.TryGetValue((x, y), out int v) && condition(v))
                    return true;

                await Task.Delay(20, token);
                waited += 20;
            }
            return false;
        }

        private async Task<bool> PlaceVerified(int tx, int ty, int blockId, float px, float py, CancellationToken token)
        {
            for (int attempt = 0; attempt < MaxPlaceAttempts; attempt++)
            {
                token.ThrowIfCancellationRequested();
                _tileState.TryRemove((tx, ty), out _);

                SendTileChangeRequest(tx, ty, blockId, px, py);

                if (await WaitForTile(tx, ty, v => v == blockId, 700, token))
                    return true;

                await Task.Delay(250, token);
            }
            return false;
        }

        private async Task<bool> BreakVerified(int tx, int ty, float px, float py, CancellationToken token)
        {
            _tileState.TryRemove((tx, ty), out _);

            for (int hit = 0; hit < MaxBreakHits; hit++)
            {
                token.ThrowIfCancellationRequested();

                SendTileChangeRequest(tx, ty, FistId, px, py);

                if (await WaitForTile(tx, ty, v => v == FistId, 220, token))
                    return true;
            }
            return false;
        }

        private async Task FarmLoop(int blockId, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && _engine.State.IsServerConnected)
                {
                    float px = _engine.State.X;
                    float py = _engine.State.Y;
                    int tileX = (int)(px / 32);
                    int tileY = (int)(py / 32);

                    int[] targets = { tileX + 1, tileX + 2 };

                    // 1. İKİSİNİ DE KOY (doğrulayarak)
                    var placed = new List<int>();
                    foreach (int tx in targets)
                    {
                        if (await PlaceVerified(tx, tileY, blockId, px, py, token))
                            placed.Add(tx);
                        else
                            Logger.Warning($"[AUTOFARM] ({tx},{tileY}) konamadı.");
                    }

                    if (placed.Count == 0)
                    {
                        _engine.SendClientMessage("`4[#AUTOFARM]`o Hiç blok konamadı (envanter/konum?).");
                        Stop("Blok konamıyor");
                        break;
                    }

                    // 2. KONANLARIN HEPSİNİ KIR (doğrulayarak)
                    foreach (int tx in placed)
                    {
                        if (!await BreakVerified(tx, tileY, px, py, token))
                            Logger.Warning($"[AUTOFARM] ({tx},{tileY}) kırılamadı.");
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Logger.Error($"AutoFarm Hatası: {ex.Message}");
            }
            finally
            {
                if (IsRunning)
                {
                    IsRunning = false;
                    ActiveBlockId = 0;
                    if (!_engine.State.IsServerConnected)
                        _engine.SendClientMessage("`4[#AUTOFARM]`o Bağlantı koptu, durduruldu.");
                }
            }
        }

        private void SendTileChangeRequest(int tileX, int tileY, int itemId, float playerX, float playerY)
        {
            var header = new TankPacketHeader
            {
                Type = (byte)TankPacketType.TileChangeReq,
                NetId = _engine.State.LocalPlayerNetId,
                IntData = itemId,
                VecX = playerX,
                VecY = playerY,
                IntX = tileX,
                IntY = tileY
            };

            byte[] packet = new TankPacket(header).ToBytes();

            Logger.Proxy($"[AUTOFARM] Item={itemId}, Tile=({tileX},{tileY}), Length={packet.Length}");

            _engine.SendToServer(packet, skipThrottle: true);
        }
    }
}