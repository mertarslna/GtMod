using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ENet;
using GtProxyMod.Core;
using GtProxyMod.Network;
using GtProxyMod.Protocol;

namespace GtProxyMod.Features
{
    public class AutoclaveManager
    {
        private readonly ProxyEngine _engine;
        private CancellationTokenSource? _cts;
        private readonly object _lock = new();

        public bool IsEnabled { get; set; } = true;
        public bool IsRunning { get; private set; }
        public bool IsStitchesMode { get; set; } = false;

        public int TargetToolId { get; set; } = 1262;
        public int TileX { get; set; } = -1;
        public int TileY { get; set; } = -1;
        public int CyclesCompleted { get; private set; }

        private readonly SemaphoreSlim _dialogSemaphore = new(1, 1);
        private readonly Dictionary<int, int> _knownInventory = new();

        public static readonly Dictionary<int, (string Name, string[] Aliases)> SurgicalTools = new()
        {
            { 1258, ("Surgical Sponge", new[] { "sponge", "sünger", "sunger", "1258" }) },
            { 1260, ("Surgical Scalpel", new[] { "scalpel", "neşter", "nester", "1260" }) },
            { 1262, ("Surgical Anesthetic", new[] { "anesthetic", "anes", "anestezi", "1262" }) },
            { 1264, ("Surgical Antiseptic", new[] { "antiseptic", "anti", "antiseptik", "1264" }) },
            { 1266, ("Surgical Antibiotics", new[] { "antibiotics", "antibio", "antibiyotik", "1266" }) },
            { 1268, ("Surgical Splint", new[] { "splint", "atel", "1268" }) },
            { 1270, ("Surgical Stitches", new[] { "stitches", "stitch", "stit", "dikiş", "dikis", "1270" }) },
            { 4308, ("Surgical Pins", new[] { "pins", "pin", "pim", "pimler", "4308" }) },
            { 4310, ("Surgical Transfusion", new[] { "transfusion", "trans", "kan", "4310" }) },
            { 4312, ("Surgical Defibrillator", new[] { "defibrillator", "defib", "şok", "sok", "4312" }) },
            { 4314, ("Surgical Clamp", new[] { "clamp", "klemp", "mandall", "4314" }) },
            { 4316, ("Surgical Ultrasound", new[] { "ultrasound", "ultra", "ultrason", "4316" }) },
            { 4318, ("Surgical Lab Kit", new[] { "lab kit", "lab", "laboratuvar", "4318" }) },
        };

        public AutoclaveManager(ProxyEngine engine)
        {
            _engine = engine;
        }

        private static int GetHumanizedDelay(string actionType)
        {
            int baseDelay = actionType switch
            {
                "select_tool" => Random.Shared.Next(100, 250), // 300'e 500'dü
                "verify"      => Random.Shared.Next(125, 200), // 250'ye 400'dü ve shadow ban yedirmiyordu.
                _             => 0
            };

            // Çok nadir küçük bir sapma, sabit aralık oluşmasın diye
            if (Random.Shared.Next(0, 100) < 5)
            {
                baseDelay += Random.Shared.Next(100, 250);
            }

            return baseDelay;
        }

        public static bool TryFindTool(string input, out int toolId, out string toolName)
        {
            toolId = 0;
            toolName = string.Empty;
            if (string.IsNullOrWhiteSpace(input)) return false;

            var cleaned = input.Trim().ToLowerInvariant();
            if (int.TryParse(cleaned, out var id))
            {
                if (SurgicalTools.ContainsKey(id))
                {
                    toolId = id;
                    toolName = SurgicalTools[id].Name;
                    return true;
                }
                return false;
            }

            foreach (var (tid, info) in SurgicalTools)
            {
                if (info.Name.ToLowerInvariant().Contains(cleaned) ||
                    info.Aliases.Any(a => a.Equals(cleaned, StringComparison.OrdinalIgnoreCase)))
                {
                    toolId = tid;
                    toolName = info.Name;
                    return true;
                }
            }
            return false;
        }

        public static string GetToolName(int toolId)
        {
            return SurgicalTools.TryGetValue(toolId, out var info) ? info.Name : $"Item {toolId}";
        }

        public void StartLoop(int toolId)
        {
            lock (_lock)
            {
                _cts?.Cancel();
                _cts = new CancellationTokenSource();
                IsRunning = true;
                IsStitchesMode = false;
                CyclesCompleted = 0;
                TargetToolId = toolId;
            }

            var toolName = GetToolName(toolId);
            _engine.SendClientMessage($"`2[Autoclave]`o `$ {toolName} ({toolId})`` otomasyonu hazır! Lütfen makineye VURUN.");
            Logger.Proxy($"[Autoclave DEBUG] Tekli mod başlatıldı. Hedef: {toolId}. Makineye yumruk bekleniyor.");
        }

        public void StartStitchesMode()
        {
            lock (_lock)
            {
                _cts?.Cancel();
                _cts = new CancellationTokenSource();
                IsRunning = true;
                IsStitchesMode = true;
                IsEnabled = true;
                CyclesCompleted = 0;
                TargetToolId = -1;
            }

            _engine.SendClientMessage("`2[Autoclave]`o `$ STITCHES MODU AKTİF!`` Lütfen makineye VURUN.");
            Logger.Proxy($"[Autoclave DEBUG] Stitches modu başlatıldı. Makineye yumruk bekleniyor.");
        }

        public void Stop(string reason = "")
        {
            lock (_lock)
            {
                if (!IsRunning) return;
                _cts?.Cancel();
                _cts = null;
                IsRunning = false;
                IsStitchesMode = false;
            }

            var reasonStr = string.IsNullOrEmpty(reason) ? "" : $" ({reason})";
            _engine.SendClientMessage($"`4[Autoclave]`o Otomasyon durduruldu.{reasonStr}");
            Logger.Proxy($"[Autoclave DEBUG] Stop() çağrıldı. Sebep: {reasonStr}");
        }

        public void HandleInventoryFull()
        {
            Logger.Proxy($"[Autoclave DEBUG] HandleInventoryFull() tetiklendi. Çanta dolu.");
            Stop("Çantanız tamamen dolu! (Backpack space)");
        }

        public void HandleServerStringMessage(string message)
        {
            if (!IsRunning || string.IsNullOrWhiteSpace(message)) return;

            if (message.Contains("Sorry, you need more backpack space") ||
                message.Contains("You don't have enough backpack space") ||
                message.Contains("backpack is full"))
            {
                Logger.Proxy($"[Autoclave DEBUG] Sunucudan çanta dolu uyarısı alındı! Bot durduruluyor.");
                HandleInventoryFull();
            }
        }

        public bool HandleClientTextPacket(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;

            if (raw.Contains("dialog_name|autoclave"))
            {
                if (raw.Contains("buttonClicked|Cancel"))
                {
                    if (IsRunning) Stop("Arayüzden iptal edildi");
                    return false;
                }

                var toolMatch = Regex.Match(raw, @"buttonClicked\|tool(?<id>\d+)");
                if (toolMatch.Success && int.TryParse(toolMatch.Groups["id"].Value, out var clickedToolId))
                {
                    var xMatch = Regex.Match(raw, @"(?:^|[\r\n|])tilex\|(?<x>\d+)");
                    var yMatch = Regex.Match(raw, @"(?:^|[\r\n|])tiley\|(?<y>\d+)");
                    if (xMatch.Success && int.TryParse(xMatch.Groups["x"].Value, out var x)) TileX = x;
                    if (yMatch.Success && int.TryParse(yMatch.Groups["y"].Value, out var y)) TileY = y;

                    Logger.Proxy($"[Autoclave DEBUG] Oyuncu arayüzden Tool {clickedToolId} seçti. Koordinatlar güncellendi: X:{TileX} Y:{TileY}");

                    if (IsEnabled)
                    {
                        if (IsStitchesMode)
                        {
                            if (clickedToolId != 1270) TargetToolId = clickedToolId;
                        }
                        else
                        {
                            TargetToolId = clickedToolId;
                            StartLoop(clickedToolId);
                        }
                    }
                }
            }
            return false;
        }

        public bool HandleServerDialogRequest(string dialog)
        {
            if (string.IsNullOrWhiteSpace(dialog) || !IsRunning) return false;

            // 1. Verify (Doğrulama) Penceresi Yakalama
            if (dialog.Contains("end_dialog|autoclave") && dialog.Contains("add_button|verify|"))
            {
                Logger.Proxy($"[Autoclave DEBUG] Verify diyaloğu SUNUCUDAN GELDİ.");

                var itemMatch = Regex.Match(dialog, @"embed_data\|itemID\|(?<id>\d+)");
                var xMatch = Regex.Match(dialog, @"embed_data\|tilex\|(?<x>\d+)");
                var yMatch = Regex.Match(dialog, @"embed_data\|tiley\|(?<y>\d+)");

                if (xMatch.Success && int.TryParse(xMatch.Groups["x"].Value, out var x)) TileX = x;
                if (yMatch.Success && int.TryParse(yMatch.Groups["y"].Value, out var y)) TileY = y;

                if (itemMatch.Success && int.TryParse(itemMatch.Groups["id"].Value, out int verifyItemId))
                {
                    if (IsStitchesMode && verifyItemId == 1270)
                    {
                        Stop("Stitches dönüştürülemez.");
                        return false;
                    }

                    if (verifyItemId == TargetToolId || IsStitchesMode)
                    {
                        TargetToolId = verifyItemId;
                        var ct = _cts?.Token ?? CancellationToken.None;

                        Task.Run(async () =>
                        {
                            Logger.Proxy($"[Autoclave DEBUG] Semaphore bekleniyor (Verify için)...");

                            bool acquired;
                            try { acquired = await _dialogSemaphore.WaitAsync(3000, ct); }
                            catch (OperationCanceledException) { return; }

                            if (!acquired)
                            {
                                Stop("Verify diyaloğu zaman aşımına uğradı");
                                return;
                            }

                            try
                            {
                                int delay = GetHumanizedDelay("verify");
                                Logger.Proxy($"[Autoclave DEBUG] Verify işlemi için {delay}ms bekleniyor...");
                                await Task.Delay(delay, ct);
                                if (!IsRunning || ct.IsCancellationRequested) return;

                                var verifyPayload = $"action|dialog_return\ndialog_name|autoclave\ntilex|{TileX}|\ntiley|{TileY}|\nitemID|{TargetToolId}|\nbuttonClicked|verify\n";
                                Logger.Proxy($"[Autoclave DEBUG] Verify paketi GÖNDERİLİYOR:\n{verifyPayload.Replace("\n", "\\n")}");

                                _engine.SendToServer(PacketBuilder.BuildGenericText(verifyPayload), PacketFlags.Reliable, skipThrottle: true);
                            }
                            catch (OperationCanceledException) { }
                            catch (Exception ex)
                            {
                                Logger.Proxy($"[Autoclave DEBUG] Verify Exception: {ex.Message}");
                            }
                            finally
                            {
                                _dialogSemaphore.Release();
                                Logger.Proxy($"[Autoclave DEBUG] Semaphore serbest bırakıldı (Verify).");
                            }
                        }, ct);

                        return true; // Diyaloğu gizle
                    }
                }
            }

            // 2. Autoclave Ana Seçim Menüsü Yakalama
            if (dialog.Contains("end_dialog|autoclave") && dialog.Contains("With this device, you can exchange 20"))
            {
                Logger.Proxy($"[Autoclave DEBUG] Ana Menü diyaloğu SUNUCUDAN GELDİ.");

                var xMatch = Regex.Match(dialog, @"embed_data\|tilex\|(?<x>\d+)");
                var yMatch = Regex.Match(dialog, @"embed_data\|tiley\|(?<y>\d+)");
                if (xMatch.Success && int.TryParse(xMatch.Groups["x"].Value, out var x)) TileX = x;
                if (yMatch.Success && int.TryParse(yMatch.Groups["y"].Value, out var y)) TileY = y;

                var toolMatches = Regex.Matches(dialog, @"add_button_with_icon\|tool(?<id>\d+)\|[^|]*\|[^|]*\|\d+\|(?<count>\d+)\|");

                _knownInventory.Clear();
                foreach (Match m in toolMatches)
                {
                    if (int.TryParse(m.Groups["id"].Value, out var tId) && int.TryParse(m.Groups["count"].Value, out var tCount))
                    {
                        _knownInventory[tId] = tCount;
                    }
                }

                int chosenToolId = -1;

                if (IsStitchesMode)
                {
                    var bestTool = _knownInventory
                        .Where(k => k.Key != 1270 && k.Value >= 20)
                        .OrderByDescending(k => k.Value)
                        .FirstOrDefault();

                    chosenToolId = bestTool.Key > 0 ? bestTool.Key : -1;
                }
                else
                {
                    if (_knownInventory.TryGetValue(TargetToolId, out int remainingCount) && remainingCount >= 20)
                    {
                        chosenToolId = TargetToolId;
                    }
                }

                if (chosenToolId != -1)
                {
                    TargetToolId = chosenToolId;
                    CyclesCompleted++;
                    Logger.Proxy($"[Autoclave DEBUG] Dönüştürülecek Alet: {TargetToolId} | Döngü: {CyclesCompleted}");

                    var ct = _cts?.Token ?? CancellationToken.None;
                    int toolToSelect = chosenToolId; // Task.Run içinde güvenli kullanım için yerel kopya

                    Task.Run(async () =>
                    {
                        Logger.Proxy($"[Autoclave DEBUG] Semaphore bekleniyor (Ana Menü için)...");

                        bool acquired;
                        try { acquired = await _dialogSemaphore.WaitAsync(3000, ct); }
                        catch (OperationCanceledException) { return; }

                        if (!acquired)
                        {
                            Stop("Ana Menü diyaloğu zaman aşımına uğradı");
                            return;
                        }

                        try
                        {
                            int delay = GetHumanizedDelay("select_tool");
                            Logger.Proxy($"[Autoclave DEBUG] Alet seçimi için {delay}ms bekleniyor...");
                            await Task.Delay(delay, ct);
                            if (!IsRunning || ct.IsCancellationRequested) return;

                            var selectPayload = $"action|dialog_return\ndialog_name|autoclave\ntilex|{TileX}|\ntiley|{TileY}|\nbuttonClicked|tool{toolToSelect}\n";
                            Logger.Proxy($"[Autoclave DEBUG] Ana Menü Seçim paketi GÖNDERİLİYOR:\n{selectPayload.Replace("\n", "\\n")}");

                            _engine.SendToServer(PacketBuilder.BuildGenericText(selectPayload), PacketFlags.Reliable, skipThrottle: true);
                        }
                        catch (OperationCanceledException) { }
                        catch (Exception ex)
                        {
                            Logger.Proxy($"[Autoclave DEBUG] Ana Menü Exception: {ex.Message}");
                        }
                        finally
                        {
                            _dialogSemaphore.Release();
                            Logger.Proxy($"[Autoclave DEBUG] Semaphore serbest bırakıldı (Ana Menü).");
                        }
                    }, ct);

                    return true; // Diyaloğu gizle
                }
                else
                {
                    int lastTarget = TargetToolId;
                    Stop($"Dönüştürülecek alet kalmadı veya stok < 20. TargetID: {lastTarget}");

                    var cancelPayload = $"action|dialog_return\ndialog_name|autoclave\ntilex|{TileX}|\ntiley|{TileY}|\nbuttonClicked|Cancel\n";
                    Logger.Proxy($"[Autoclave DEBUG] Cancel paketi GÖNDERİLİYOR:\n{cancelPayload.Replace("\n", "\\n")}");
                    _engine.SendToServer(PacketBuilder.BuildGenericText(cancelPayload), PacketFlags.Reliable, skipThrottle: true);

                    return true; // Diyaloğu oyuncuya gösterme
                }
            }

            return false;
        }
    }
}