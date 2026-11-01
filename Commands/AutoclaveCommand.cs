using System.Text;
using GtProxyMod.Core;
using GtProxyMod.Features;
using GtProxyMod.Network;
using GtProxyMod.Protocol;
using GtProxyMod.State;

namespace GtProxyMod.Commands
{
    public class AutoclaveCommand : ICommand
    {
        public string Name => "autoclave";
        public string Description => "Cerrahi aletleri otomatik autoclave yapar";
        public string Usage => "/autoclave [stit | alet | on/off | stop]";
        public string[] Aliases => new[] { "ac", "auto" };

        public void Execute(string[] args, ProxyEngine engine, ProxyState state, bool isFromClient)
        {
            var ac = engine.Autoclave;

            if (args.Length == 0)
            {
                ShowStatusAndHelp(engine, isFromClient);
                return;
            }

            var subCmd = args[0].ToLowerInvariant();

            switch (subCmd)
            {
                case "stit":
                case "stitch":
                case "stitches":
                case "dikiş":
                case "dikis":
                    ac.StartStitchesMode();
                    break;

                case "on":
                case "1":
                case "enable":
                    ac.IsEnabled = true;
                    SendMsg(engine, isFromClient, "`2[Autoclave]`o Özellik `2AÇILDI`o. Autoclave makinesini açıp dönüştürmek istediğiniz alete tıklayın veya `2/ac stit`o yazın.");
                    break;

                case "off":
                case "0":
                case "disable":
                    ac.IsEnabled = false;
                    ac.Stop("Kullanıcı kapattı");
                    SendMsg(engine, isFromClient, "`4[Autoclave]`o Özellik `4KAPATILDI`o.");
                    break;

                case "stop":
                case "dur":
                case "cancel":
                    ac.Stop("Kullanıcı tarafından durduruldu");
                    break;

                case "start":
                case "run":
                case "başla":
                case "basla":
                    if (ac.TileX >= 0 && ac.TileY >= 0)
                    {
                        ac.IsEnabled = true;
                        if (ac.IsStitchesMode)
                        {
                            ac.StartStitchesMode();
                        }
                        else
                        {
                            ac.StartLoop(ac.TargetToolId);
                        }
                        SendMsg(engine, isFromClient, $"`2[Autoclave]`o Pozisyon ({ac.TileX}, {ac.TileY}) açılarak otomasyon başlatılıyor...");
                    }
                    else
                    {
                        SendMsg(engine, isFromClient, "`4[Hata]`o Önce en az bir kez autoclave makinesine tıklamalısınız (makine konumu henüz bilinmiyor).");
                    }
                    break;

                case "status":
                case "durum":
                    ShowStatusAndHelp(engine, isFromClient);
                    break;

                default:
                    var toolInput = string.Join(' ', args);
                    if (AutoclaveManager.TryFindTool(toolInput, out var toolId, out var toolName))
                    {
                        ac.TargetToolId = toolId;
                        ac.IsEnabled = true;
                        ac.StartLoop(toolId);
                    }
                    else
                    {
                        SendMsg(engine, isFromClient, $"`4[Autoclave]`o Geçersiz seçenek veya alet: `{toolInput}`. Kullanım: `2/ac stit`o veya `2/ac anes`o");
                    }
                    break;
            }
        }

        private void ShowStatusAndHelp(ProxyEngine engine, bool isFromClient)
        {
            var ac = engine.Autoclave;
            var targetName = ac.IsStitchesMode ? "`2STITCHES MODU (1270 Hariç Tümü)``" : AutoclaveManager.GetToolName(ac.TargetToolId);
            var enabledStr = ac.IsEnabled ? "`2AÇIK`o" : "`4KAPALI`o";
            var runningStr = ac.IsRunning ? (ac.IsStitchesMode ? "`2ÇALIŞIYOR (Stitches Modu)`o" : "`2ÇALIŞIYOR (Tek Alet Döngüsü)`o") : "`7BEKLEMEDE`o";

            var sb = new StringBuilder();
            sb.AppendLine("`2=== Autoclave Modu Durumu ===`o");
            sb.AppendLine($"Durum: {enabledStr} | İşlem: {runningStr}");
            sb.AppendLine($"Hedef: `$ {targetName}`` | Yapılan Clave: `9{ac.CyclesCompleted}`o");
            if (ac.TileX >= 0 && ac.TileY >= 0)
            {
                sb.AppendLine($"Kayıtlı Autoclave Pozisyonu: `9({ac.TileX}, {ac.TileY})`o");
            }
            sb.AppendLine("`9Kullanım:`o");
            sb.AppendLine("  `2/ac stit`o          : Stitches hariç çantadaki tüm aletleri dönüştürür (20 ve üzeri olanları)");
            sb.AppendLine("  `2/ac <alet_adı|id>`o : Seçilen aleti otomatik dönüştürür (Örn: /ac anes, /ac 1262)");
            sb.AppendLine("  `2/ac stop`o          : Çalışan işlemi hemen durdurur");
            sb.AppendLine("  `2/ac on | /ac off`o  : Autoclave özelliğini açar/kapatır");
            sb.AppendLine("  `8(Kısayol: /stit yazarak da doğrudan Stitches modunu başlatabilirsiniz!)`o");

            SendMsg(engine, isFromClient, sb.ToString().TrimEnd());
        }

        private void SendMsg(ProxyEngine engine, bool isFromClient, string msg)
        {
            if (isFromClient)
            {
                engine.SendClientMessage(msg);
            }
            else
            {
                Logger.Info(msg.Replace("`", ""));
            }
        }
    }
}
