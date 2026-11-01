using System.Text;
using GtProxyMod.Core;
using GtProxyMod.Network;
using GtProxyMod.State;

namespace GtProxyMod.Commands
{
    public class StatusCommand : ICommand
    {
        public string Name => "status";
        public string Description => "Bağlantı ve oyuncu durumunu görüntüler";
        public string Usage => "/status";
        public string[] Aliases => new[] { "info", "who" };

        public void Execute(string[] args, ProxyEngine engine, ProxyState state, bool isFromClient)
        {
            int tileX = (int)(state.X / 32f);
            int tileY = (int)(state.Y / 32f);

            var sb = new StringBuilder();
            sb.AppendLine("`2=== Proxy Durumu ===`o");
            sb.AppendLine($"`9Hedef Sunucu:`o {engine.ActiveTargetIp}:{engine.ActiveTargetPort}");
            sb.AppendLine($"`9İstemci Bağlantısı:`o {(state.IsClientConnected ? "`2Bağlı`o" : "`4Bağlı Değil`o")}");
            sb.AppendLine($"`9Sunucu Bağlantısı:`o {(state.IsServerConnected ? "`2Bağlı`o" : "`4Bağlı Değil`o")}");
            sb.AppendLine($"`9Mevcut Dünya:`o `{state.CurrentWorld}`");
            sb.AppendLine($"`9NetID:`o {state.LocalPlayerNetId}");
            sb.AppendLine($"`9Konum:`o X: {state.X:F1}, Y: {state.Y:F1} `8(Kare: {tileX}, {tileY})`o");
            sb.AppendLine($"`9Ghost Modu:`o {(state.GhostMode ? "`2Aktif`o" : "`4Devre Dışı`o")}");
            sb.AppendLine($"`9Dünyadaki Oyuncular:`o {state.WorldPlayers.Count} kişi");

            if (isFromClient)
            {
                engine.SendClientMessage(sb.ToString().TrimEnd());
            }
            else
            {
                Logger.Info("=== Proxy Durumu ===");
                Logger.Info($"Hedef Sunucu: {engine.ActiveTargetIp}:{engine.ActiveTargetPort}");
                Logger.Info($"İstemci: {(state.IsClientConnected ? "Bağlı" : "Bağlı Değil")}, Sunucu: {(state.IsServerConnected ? "Bağlı" : "Bağlı Değil")}");
                Logger.Info($"Dünya: {state.CurrentWorld}, NetID: {state.LocalPlayerNetId}");
                Logger.Info($"Konum: ({state.X:F1}, {state.Y:F1}) - Kare: ({tileX}, {tileY})");
                Logger.Info($"Ghost Mod: {(state.GhostMode ? "Aktif" : "Devre Dışı")}");
            }
        }
    }
}
