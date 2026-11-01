using GtProxyMod.Core;
using GtProxyMod.Network;
using GtProxyMod.State;

namespace GtProxyMod.Commands
{
    public class GhostCommand : ICommand
    {
        public string Name => "ghost";
        public string Description => "Ghost modunu açar/kapatır (Sunucuya hareket paketi gitmez)";
        public string Usage => "/ghost [on/off]";
        public string[] Aliases => new[] { "stealth", "vanish" };

        public void Execute(string[] args, ProxyEngine engine, ProxyState state, bool isFromClient)
        {
            if (args.Length > 0)
            {
                var arg = args[0].ToLowerInvariant();
                state.GhostMode = (arg == "on" || arg == "1" || arg == "true");
            }
            else
            {
                state.GhostMode = !state.GhostMode;
            }

            var statusStr = state.GhostMode ? "`2AÇIK`o" : "`4KAPALI`o";
            var msg = $"`9[Ghost Mod]`o Durum: {statusStr}. {(state.GhostMode ? "Artık hareketleriniz sunucuya iletilmiyor (serbest dolaşım)." : "Hareketleriniz normal şekilde sunucuya iletiliyor.")}";

            if (isFromClient)
            {
                engine.SendClientMessage(msg);
                engine.SendClientOverlay(state.GhostMode ? "Ghost Mod: AKTİF" : "Ghost Mod: DEVRE DIŞI");
            }
            Logger.Proxy($"Ghost Mod: {(state.GhostMode ? "AÇIK" : "KAPALI")}");
        }
    }
}
