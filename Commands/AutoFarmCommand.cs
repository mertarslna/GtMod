using System;
using GtProxyMod.Core;
using GtProxyMod.Network;
using GtProxyMod.State;

namespace GtProxyMod.Commands
{
    public class AutoFarmCommand : ICommand
    {
        public string Name => "autofarm";
        public string[] Aliases => new[] { "af" };
        public string Description => "Belirtilen blok/tohum ID'sini koyup kırar.";
        public string Usage => "/autofarm <BlokID> | /autofarm off";

        public void Execute(string[] args, ProxyEngine engine, ProxyState state, bool isFromClient)
        {
            if (args.Length == 0)
            {
                if (engine.AutoFarm.IsRunning)
                    engine.AutoFarm.Stop("Kullanıcı kapattı");
                else
                    engine.SendClientMessage($"`4[Hata]`o Kullanım: `{Usage}` (Örn: `/af 5666`)");
                return;
            }

            var arg = args[0].Trim();

            if (arg.Equals("off", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("kapat", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("stop", StringComparison.OrdinalIgnoreCase))
            {
                if (engine.AutoFarm.IsRunning)
                    engine.AutoFarm.Stop("Kullanıcı kapattı");
                else
                    engine.SendClientMessage("`4[#AUTOFARM]`o Zaten kapalı.");
                return;
            }

            if (int.TryParse(arg, out int blockId))
            {
                engine.AutoFarm.Toggle(blockId);
            }
            else
            {
                engine.SendClientMessage("`4[Hata]`o Geçerli bir sayı ya da `off` girin.");
            }
        }
    }
}