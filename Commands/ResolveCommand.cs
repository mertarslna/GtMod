using System.Threading.Tasks;
using GtProxyMod.Core;
using GtProxyMod.Network;
using GtProxyMod.State;

namespace GtProxyMod.Commands
{
    public class ResolveCommand : ICommand
    {
        public string Name => "resolve";
        public string Description => "Resmi Growtopia sunucu IP'sini server_data.php üzerinden yeniden sorgular";
        public string Usage => "/resolve";
        public string[] Aliases => new[] { "res", "refresh" };

        public void Execute(string[] args, ProxyEngine engine, ProxyState state, bool isFromClient)
        {
            if (isFromClient) engine.SendClientMessage("`9[Resolve]`o Resmi Growtopia sunucuları sorgulanıyor...");
            Logger.Info("Resmi Growtopia sunucuları sorgulanıyor...");

            Task.Run(async () =>
            {
                var info = await OfficialServerResolver.ResolveAsync();
                engine.Config.TargetHost = info.Server;
                engine.Config.TargetPort = info.Port;

                var result = $"Resmi sunucu güncellendi: {info.Server}:{info.Port}";
                if (isFromClient) engine.SendClientMessage($"`2[Resolve]`o {result}");
                Logger.Success(result);
            });
        }
    }
}
