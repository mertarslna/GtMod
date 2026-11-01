using GtProxyMod.Core;
using GtProxyMod.Network;
using GtProxyMod.Protocol;
using GtProxyMod.State;

namespace GtProxyMod.Commands
{
    public class WarpCommand : ICommand
    {
        public string Name => "warp";
        public string Description => "Belirtilen dünyaya hızlı geçiş yapar";
        public string Usage => "/warp <dünya_adı>";
        public string[] Aliases => new[] { "join", "go" };

        public void Execute(string[] args, ProxyEngine engine, ProxyState state, bool isFromClient)
        {
            if (args.Length == 0)
            {
                var helpMsg = "`4[Hata]`o Lütfen bir dünya adı belirtin. Örnek: `2/warp START`o";
                if (isFromClient) engine.SendClientMessage(helpMsg);
                else Logger.Warning("Lütfen bir dünya adı belirtin. Örnek: warp START");
                return;
            }

            var worldName = args[0].Trim().ToUpperInvariant();
            var payload = $"action|join_request\nname|{worldName}\ninvitedWorld|0\n";
            var packetBytes = PacketBuilder.BuildGameMessage(payload);

            engine.SendToServer(packetBytes);

            var infoMsg = $"`9[Warp]`o `{worldName}` dünyasına geçiş isteği gönderildi...";
            if (isFromClient) engine.SendClientMessage(infoMsg);
            Logger.Proxy($"Dünyaya warp isteği gönderildi: {worldName}");
        }
    }
}
