using System.Threading.Tasks;
using GtProxyMod.Network;
using GtProxyMod.State;

namespace GtProxyMod.Commands
{
    public interface ICommand
    {
        string Name { get; }
        string Description { get; }
        string Usage { get; }
        string[] Aliases { get; }

        void Execute(string[] args, ProxyEngine engine, ProxyState state, bool isFromClient);
    }
}
