using System.Text;
using GtProxyMod.Core;
using GtProxyMod.Network;
using GtProxyMod.State;

namespace GtProxyMod.Commands
{
    public class HelpCommand : ICommand
    {
        public string Name => "help";
        public string Description => "Tüm mod komutlarını listeler";
        public string Usage => "/help";
        public string[] Aliases => new[] { "proxy", "?" };

        public void Execute(string[] args, ProxyEngine engine, ProxyState state, bool isFromClient)
        {
            if (isFromClient)
            {
                var sb = new StringBuilder();
                sb.AppendLine("`2=== Growtopia Proxy Mod Komutları ===`o");
                foreach (var cmd in engine.Commands.AllCommands)
                {
                    sb.AppendLine($"`9/{cmd.Name}`o : {cmd.Description} `8({cmd.Usage})`o");
                }
                engine.SendClientMessage(sb.ToString().TrimEnd());
            }
            else
            {
                Logger.Info("=== Growtopia Proxy Mod Komutları ===");
                foreach (var cmd in engine.Commands.AllCommands)
                {
                    Logger.Info($"/{cmd.Name} : {cmd.Description} ({cmd.Usage})");
                }
            }
        }
    }
}
