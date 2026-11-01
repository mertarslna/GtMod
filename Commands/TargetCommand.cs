using GtProxyMod.Core;
using GtProxyMod.Network;
using GtProxyMod.State;

namespace GtProxyMod.Commands
{
    public class TargetCommand : ICommand
    {
        public string Name => "target";
        public string Description => "Hedef sunucu IP ve Portunu manuel olarak ayarlar";
        public string Usage => "/target <ip> [port]";
        public string[] Aliases => new[] { "server", "ip" };

        public void Execute(string[] args, ProxyEngine engine, ProxyState state, bool isFromClient)
        {
            if (args.Length == 0)
            {
                var current = $"Mevcut hedef sunucu: {engine.Config.TargetHost}:{engine.Config.TargetPort}";
                if (isFromClient) engine.SendClientMessage($"`9[Target]`o {current}");
                else Logger.Info(current);
                return;
            }

            var ip = args[0];
            ushort port = engine.Config.TargetPort;
            if (args.Length > 1 && ushort.TryParse(args[1], out var parsedPort))
            {
                port = parsedPort;
            }

            engine.Config.TargetHost = ip;
            engine.Config.TargetPort = port;

            var msg = $"Hedef sunucu güncellendi -> {ip}:{port}";
            if (isFromClient) engine.SendClientMessage($"`2[Target]`o {msg}");
            Logger.Success(msg);
        }
    }
}
