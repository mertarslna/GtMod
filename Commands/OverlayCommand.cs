using GtProxyMod.Core;
using GtProxyMod.Network;
using GtProxyMod.State;

namespace GtProxyMod.Commands
{
    public class OverlayCommand : ICommand
    {
        public string Name => "overlay";
        public string Description => "İstemci ekranında büyük yazı bildirimi gösterir";
        public string Usage => "/overlay <mesaj>";
        public string[] Aliases => new[] { "banner", "toast" };

        public void Execute(string[] args, ProxyEngine engine, ProxyState state, bool isFromClient)
        {
            if (args.Length == 0)
            {
                var help = "Lütfen gösterilecek metni yazın. Örnek: /overlay Selam Dünya!";
                if (isFromClient) engine.SendClientMessage($"`4[Hata]`o {help}");
                else Logger.Warning(help);
                return;
            }

            var text = string.Join(' ', args);
            engine.SendClientOverlay(text);
            Logger.Proxy($"Ekrana bildirim gönderildi: {text}");
        }
    }
}
