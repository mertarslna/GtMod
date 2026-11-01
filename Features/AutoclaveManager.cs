using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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

        private static int GetHumanizedDelay()
        {
            return Random.Shared.Next(450, 650);
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

        public bool IsWithinReach(int targetTileX, int targetTileY)
        {
            if (targetTileX < 0 || targetTileY < 0) return false;

            // Growtopia'da karakter konumu piksel cinsindendir (1 karo = 32 piksel)
            float playerTileX = _engine.State.X / 32f;
            float playerTileY = _engine.State.Y / 32f;

            float dx = playerTileX - targetTileX;
            float dy = playerTileY - targetTileY;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            // Resmi sunucu erişim limiti normalde 3-4 karodur. 4.5 karo üstü doğrudan ban/flag sebebidir.
            return distance <= 4.2;
        }

        public void StartLoop(int toolId)
        {
            if (TileX >= 0 && TileY >= 0 && !IsWithinReach(TileX, TileY))
            {
                _engine.SendClientMessage("`4[Autoclave Hata]`o Makineye çok uzaksınız! Lütfen makinenin yanına geçin (Shadow Ban Koruması).");
                return;
            }

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
            _engine.SendClientMessage($"`2[Autoclave]`o `$ {toolName} ({toolId})`` seçildi! Otomasyon başladı.");
            _engine.SendClientOverlay($"Autoclave: {toolName}");
            Logger.Proxy($"[Autoclave] {toolName} ({toolId}) otomasyonu başlatıldı.");

            // Pozisyon biliniyorsa doğrudan makineye vurarak başlat
            if (TileX >= 0 && TileY >= 0)
            {
                var punchPacket = PacketBuilder.BuildTileChange(TileX, TileY, itemNetId: 18);
                _engine.SendToServer(punchPacket.ToBytes());
            }
        }

        public void StartStitchesMode()
        {
            if (TileX >= 0 && TileY >= 0 && !IsWithinReach(TileX, TileY))
            {
                _engine.SendClientMessage("`4[Autoclave Hata]`o Makineye çok uzaksınız! Lütfen makinenin yanına geçin (Shadow Ban Koruması).");
                return;
            }

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

            _engine.SendClientMessage("`2[Autoclave]`o `$ STITCHES MODU AKTİF!``");
            _engine.SendClientOverlay("Autoclave: STITCHES MODU");

            if (TileX >= 0 && TileY >= 0)
            {
                var punchPacket = PacketBuilder.BuildTileChange(TileX, TileY, itemNetId: 18);
                _engine.SendToServer(punchPacket.ToBytes());
            }
        }

        public void Stop(string reason = "")
        {
            lock (_lock)
            {
                _cts?.Cancel();
                _cts = null;
                IsRunning = false;
                IsStitchesMode = false;
            }

            if (TileX >= 0 && TileY >= 0)
            {
                try
                {
                    var cancelPayload = $"action|dialog_return\ndialog_name|autoclave\ntilex|{TileX}|\ntiley|{TileY}|\nbuttonClicked|Cancel\n\n";
                    _engine.SendToServer(PacketBuilder.BuildGenericText(cancelPayload));
                }
                catch { }
            }

            var reasonStr = string.IsNullOrEmpty(reason) ? "" : $" ({reason})";
            _engine.SendClientMessage($"`4[Autoclave]`o Otomasyon durduruldu.{reasonStr}");
            _engine.SendClientOverlay("Autoclave: DURDURULDU");
        }

        public void HandleInventoryFull()
        {
            Stop("Çanta tamamen doldu (Backpack space)");
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
                    if (SurgicalTools.ContainsKey(clickedToolId))
                    {
                        var xMatch = Regex.Match(raw, @"(?:^|[\r\n|])tilex\|(?<x>\d+)");
                        var yMatch = Regex.Match(raw, @"(?:^|[\r\n|])tiley\|(?<y>\d+)");
                        if (xMatch.Success && int.TryParse(xMatch.Groups["x"].Value, out var x)) TileX = x;
                        if (yMatch.Success && int.TryParse(yMatch.Groups["y"].Value, out var y)) TileY = y;

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
            }
            return false;
        }

        public bool HandleServerDialogRequest(string dialog)
        {
            if (string.IsNullOrWhiteSpace(dialog)) return false;

            // 1. Verify (Doğrulama) Penceresi Yakalama
            if (IsRunning && dialog.Contains("end_dialog|autoclave") && dialog.Contains("add_button|verify|"))
            {
                var itemMatch = Regex.Match(dialog, @"embed_data\|itemID\|(?<id>\d+)");
                var xMatch = Regex.Match(dialog, @"embed_data\|tilex\|(?<x>\d+)");
                var yMatch = Regex.Match(dialog, @"embed_data\|tiley\|(?<y>\d+)");
                if (xMatch.Success && int.TryParse(xMatch.Groups["x"].Value, out var x)) TileX = x;
                if (yMatch.Success && int.TryParse(yMatch.Groups["y"].Value, out var y)) TileY = y;

                if (itemMatch.Success && int.TryParse(itemMatch.Groups["id"].Value, out int verifyItemId))
                {
                    if (IsStitchesMode && verifyItemId == 1270)
                    {
                        Stop("Stitches dönüştürülemez");
                        return true;
                    }

                    if (verifyItemId == TargetToolId || IsStitchesMode)
                    {
                        TargetToolId = verifyItemId;
                        var ct = _cts?.Token ?? CancellationToken.None;

                        Task.Run(async () =>
                        {
                            if (!await _dialogSemaphore.WaitAsync(0)) return;
                            try
                            {
                                await Task.Delay(GetHumanizedDelay(), ct);
                                if (!IsRunning || ct.IsCancellationRequested) return;

                                var verifyPayload = $"action|dialog_return\ndialog_name|autoclave\ntilex|{TileX}|\ntiley|{TileY}|\nitemID|{TargetToolId}|\nbuttonClicked|verify\n\n";
                                _engine.SendToServer(PacketBuilder.BuildGenericText(verifyPayload));
                            }
                            catch { }
                            finally
                            {
                                _dialogSemaphore.Release();
                            }
                        }, ct);

                        return true;
                    }
                }
            }

            // 2. Autoclave Seçim Menüsü Yakalama & Basit Clave Döngüsü
            if (dialog.Contains("end_dialog|autoclave") && dialog.Contains("With this device, you can exchange 20"))
            {
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

                if (!IsRunning) return false;

                // --- STITCHES MODU (1270 hariç 20+ olan aletlerden en yüksek sayıda olanı dönüştür) ---
                if (IsStitchesMode)
                {
                    // 1270 (Stitches) hariç ve adedi en az 20 olan aletleri miktarına göre çoktan aza sırala
                    var bestTool = _knownInventory
                        .Where(k => k.Key != 1270 && k.Value >= 20)
                        .OrderByDescending(k => k.Value)
                        .FirstOrDefault();

                    int chosenToolId = bestTool.Key > 0 ? bestTool.Key : -1;

                    if (chosenToolId != -1)
                    {
                        TargetToolId = chosenToolId;
                        CyclesCompleted++;
                        var ct = _cts?.Token ?? CancellationToken.None;

                        Task.Run(async () =>
                        {
                            if (!await _dialogSemaphore.WaitAsync(0)) return;
                            try
                            {
                                await Task.Delay(GetHumanizedDelay(), ct);
                                if (!IsRunning || ct.IsCancellationRequested) return;

                                var selectPayload = $"action|dialog_return\ndialog_name|autoclave\ntilex|{TileX}|\ntiley|{TileY}|\nbuttonClicked|tool{chosenToolId}\n\n";
                                _engine.SendToServer(PacketBuilder.BuildGenericText(selectPayload));
                            }
                            catch { }
                            finally
                            {
                                _dialogSemaphore.Release();
                            }
                        }, ct);

                        return true;
                    }
                    else
                    {
                        Stop("Dönüştürülecek alet kalmadı (Stok < 20)");
                        return true;
                    }
                }

                // --- TEK ALET MODU (Sadece seçilen alet 20+ ise dönüştür) ---
                if (_knownInventory.TryGetValue(TargetToolId, out int remainingCount) && remainingCount >= 20)
                {
                    CyclesCompleted++;
                    var ct = _cts?.Token ?? CancellationToken.None;

                    Task.Run(async () =>
                    {
                        if (!await _dialogSemaphore.WaitAsync(0)) return;
                        try
                        {
                            await Task.Delay(GetHumanizedDelay(), ct);
                            if (!IsRunning || ct.IsCancellationRequested) return;

                            var selectPayload = $"action|dialog_return\ndialog_name|autoclave\ntilex|{TileX}|\ntiley|{TileY}|\nbuttonClicked|tool{TargetToolId}\n\n";
                            _engine.SendToServer(PacketBuilder.BuildGenericText(selectPayload));
                        }
                        catch { }
                        finally
                        {
                            _dialogSemaphore.Release();
                        }
                    }, ct);

                    return true;
                }
                else
                {
                    Stop($"{GetToolName(TargetToolId)} stoğu bitti (Stok < 20)");
                    return true;
                }
            }

            return false;
        }
    }
}
