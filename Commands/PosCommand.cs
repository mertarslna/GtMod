using GtProxyMod.Core;
using GtProxyMod.Network;
using GtProxyMod.State;

namespace GtProxyMod.Commands
{
    public class PosCommand : ICommand
    {
        public string Name => "pos";
        public string Description => "Karakterin tam piksel ve blok koordinatlarını gösterir";
        public string Usage => "/pos";
        public string[] Aliases => new[] { "coords", "loc" };

        public void Execute(string[] args, ProxyEngine engine, ProxyState state, bool isFromClient)
        {
            int tileX = (int)(state.X / 32f);
            int tileY = (int)(state.Y / 32f);

            var msg = $"`9[Konum Bilgisi]`o Piksel: `2({state.X:F1}, {state.Y:F1})`o | Blok (Tile): `2({tileX}, {tileY})`o";
            if (isFromClient) engine.SendClientMessage(msg);
            Logger.Proxy($"Konum: ({state.X:F1}, {state.Y:F1}) Blok: ({tileX}, {tileY})");
        }
    }
}
