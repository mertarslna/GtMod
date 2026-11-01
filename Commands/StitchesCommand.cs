using System;
using GtProxyMod.Core;
using GtProxyMod.Network;
using GtProxyMod.State;

namespace GtProxyMod.Commands
{
    public class StitchesCommand : ICommand
    {
        public string Name => "stit";
        public string Description => "Stitches (1270) hariç tüm cerrahi aletleri autoclave yapar";
        public string Usage => "/stit [stop]";
        public string[] Aliases => new[] { "stitches", "stitch", "dikiş", "dikis" };

        public void Execute(string[] args, ProxyEngine engine, ProxyState state, bool isFromClient)
        {
            var ac = engine.Autoclave;

            if (args.Length > 0 && (args[0].Equals("stop", StringComparison.OrdinalIgnoreCase) ||
                                    args[0].Equals("dur", StringComparison.OrdinalIgnoreCase) ||
                                    args[0].Equals("cancel", StringComparison.OrdinalIgnoreCase)))
            {
                ac.Stop("Kullanıcı tarafından durduruldu");
                return;
            }

            ac.StartStitchesMode();
        }
    }
}
