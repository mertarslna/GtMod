using GtProxyMod.Core;
using GtProxyMod.Network;
using GtProxyMod.State;

namespace GtProxyMod.Commands
{
    public class LogCommand : ICommand
    {
        public string Name => "log";
        public string Description => "Paket günlüğü ve filtre ayarlarını değiştirir";
        public string Usage => "/log [all/none/move/text/packet]";
        public string[] Aliases => new[] { "logger" };

        public void Execute(string[] args, ProxyEngine engine, ProxyState state, bool isFromClient)
        {
            if (args.Length == 0)
            {
                var msg = $"`9[Log Durumu]`o Paket Log: {(state.PacketLogging ? "`2AÇIK`o" : "`4KAPALI`o")}, Hareket Log: {(state.MovementLogging ? "`2AÇIK`o" : "`4KAPALI`o")}. Kullanım: `2/log [all/none/move]`o";
                if (isFromClient) engine.SendClientMessage(msg);
                else Logger.Info(msg);
                return;
            }

            var sub = args[0].ToLowerInvariant();
            switch (sub)
            {
                case "all":
                    state.PacketLogging = true;
                    state.MovementLogging = true;
                    engine.Config.LogMovement = true;
                    engine.Config.LogPackets = true;
                    Notify(engine, isFromClient, "Tüm paket ve hareket logları açıldı.");
                    break;

                case "none":
                case "off":
                    state.PacketLogging = false;
                    state.MovementLogging = false;
                    engine.Config.LogMovement = false;
                    engine.Config.LogPackets = false;
                    Notify(engine, isFromClient, "Tüm paket logları kapatıldı.");
                    break;

                case "move":
                case "movement":
                    state.MovementLogging = !state.MovementLogging;
                    engine.Config.LogMovement = state.MovementLogging;
                    Notify(engine, isFromClient, $"Hareket (State) logları {(state.MovementLogging ? "AÇILDI" : "KAPATILDI")}.");
                    break;

                case "on":
                case "packets":
                    state.PacketLogging = true;
                    engine.Config.LogPackets = true;
                    Notify(engine, isFromClient, "Paket logları açıldı.");
                    break;

                default:
                    Notify(engine, isFromClient, "Geçersiz seçenek. Kullanım: /log [all/none/move/on]");
                    break;
            }
        }

        private void Notify(ProxyEngine engine, bool isFromClient, string text)
        {
            if (isFromClient)
                engine.SendClientMessage($"`9[Log]`o {text}");
            Logger.Proxy(text);
        }
    }
}
