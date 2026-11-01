using System;
using System.Threading;
using System.Threading.Tasks;
using GtProxyMod.Core;
using GtProxyMod.Network;
using GtProxyMod.Protocol;

namespace GtProxyMod.Features
{
    public class AutoSpamManager : IDisposable
    {
        private readonly ProxyEngine _engine;
        private CancellationTokenSource? _cts;
        private readonly object _lock = new();

        public bool IsRunning { get; private set; }
        public string Message { get; set; } = "BUY SURGICAL TOOLS HERE! /trade me";
        public int IntervalSeconds { get; set; } = 6; // Growtopia sohbet flood limiti genelde 4-5 saniyedir

        public AutoSpamManager(ProxyEngine engine)
        {
            _engine = engine;
        }

        public void Start(string? customMessage = null, int? intervalSeconds = null)
        {
            lock (_lock)
            {
                if (!string.IsNullOrWhiteSpace(customMessage))
                {
                    Message = customMessage.Trim();
                }

                if (intervalSeconds.HasValue && intervalSeconds.Value >= 4)
                {
                    IntervalSeconds = intervalSeconds.Value;
                }

                _cts?.Cancel();
                _cts = new CancellationTokenSource();
                var token = _cts.Token;
                IsRunning = true;

                Task.Run(() => SpamLoopAsync(token), token);
            }

            _engine.SendClientMessage($"`2[AutoSpam]`o `$ Başlatıldı!`` Aralık: `9{IntervalSeconds}sn`o | Mesaj: `{Message}`");
            Logger.Proxy($"[AutoSpam] Başlatıldı. Mesaj: '{Message}' (Aralık: {IntervalSeconds}s)");
        }

        public void Stop()
        {
            lock (_lock)
            {
                if (!IsRunning) return;
                _cts?.Cancel();
                _cts = null;
                IsRunning = false;
            }

            _engine.SendClientMessage("`4[AutoSpam]`o Otomatik mesaj gönderimi durduruldu.");
            Logger.Proxy("[AutoSpam] Durduruldu.");
        }

        private async Task SpamLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && IsRunning)
            {
                try
                {
                    if (_engine.State.IsServerConnected && !string.IsNullOrWhiteSpace(Message))
                    {
                        // Growtopia sohbet paketi: action|input\ntext|mesaj\n
                        var packet = PacketBuilder.BuildGenericText($"action|input\ntext|{Message}\n");
                        _engine.SendToServer(packet);
                    }

                    // Shadow ban ve spam kick yememek için minimum 4.2 saniye ve dinamik jitter ekle (+-350ms)
                    int jitter = Random.Shared.Next(-300, 450);
                    int delayMs = Math.Max(4200, (IntervalSeconds * 1000) + jitter);

                    await Task.Delay(delayMs, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Warning($"[AutoSpam] Hata: {ex.Message}");
                    await Task.Delay(2000, token);
                }
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}

