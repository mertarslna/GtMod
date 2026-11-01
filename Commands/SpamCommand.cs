using System;
using System.Text;
using GtProxyMod.Core;
using GtProxyMod.Network;
using GtProxyMod.State;

namespace GtProxyMod.Commands
{
    public class SpamCommand : ICommand
    {
        public string Name => "spam";
        public string Description => "Satış dünyalarında otomatik reklam/sohbet mesajı atar";
        public string Usage => "/spam [mesaj] | /spam delay <saniye> | /spam stop";
        public string[] Aliases => new[] { "autospam", "msg", "reklam" };

        public void Execute(string[] args, ProxyEngine engine, ProxyState state, bool isFromClient)
        {
            var spam = engine.AutoSpam;

            if (args.Length == 0)
            {
                if (spam.IsRunning)
                {
                    spam.Stop();
                }
                else
                {
                    ShowStatusAndHelp(engine, isFromClient);
                }
                return;
            }

            var firstArg = args[0].ToLowerInvariant();

            if (firstArg == "stop" || firstArg == "dur" || firstArg == "off")
            {
                spam.Stop();
                return;
            }

            if (firstArg == "delay" || firstArg == "aralık" || firstArg == "sure" || firstArg == "time")
            {
                if (args.Length > 1 && int.TryParse(args[1], out var sec))
                {
                    if (sec < 4)
                    {
                        SendMsg(engine, isFromClient, "`4[AutoSpam]`o Spam süresi kick ve shadow ban yememek için en az 4 saniye olmalıdır!");
                        return;
                    }

                    spam.IntervalSeconds = sec;
                    SendMsg(engine, isFromClient, $"`2[AutoSpam]`o Mesaj aralığı `9{sec} saniye`o olarak güncellendi.");
                }
                else
                {
                    SendMsg(engine, isFromClient, "`4[AutoSpam]`o Kullanım: `/spam delay <saniye>` (Örn: /spam delay 5)");
                }
                return;
            }

            if (firstArg == "status" || firstArg == "durum")
            {
                ShowStatusAndHelp(engine, isFromClient);
                return;
            }

            // Geri kalan tüm metin reklam mesajı olarak kabul edilir
            var fullMessage = string.Join(' ', args);
            spam.Start(fullMessage);
        }

        private void ShowStatusAndHelp(ProxyEngine engine, bool isFromClient)
        {
            var spam = engine.AutoSpam;
            var statusStr = spam.IsRunning ? "`2ÇALIŞIYOR`o" : "`4DURDURULDU`o";

            var sb = new StringBuilder();
            sb.AppendLine("`2=== AutoSpam (Otomatik Reklam) ===`o");
            sb.AppendLine($"Durum: {statusStr} | Aralık: `9{spam.IntervalSeconds} sn`o");
            sb.AppendLine($"Mevcut Mesaj: `{spam.Message}`");
            sb.AppendLine("`9Kullanım:`o");
            sb.AppendLine("  `2/spam <mesajınız>`o   : Belirtilen mesajla spamı başlatır (Örn: /spam Sell Surg 20/1)");
            sb.AppendLine("  `2/spam delay <saniye>`o: Mesaj atma aralığını belirler (Varsayılan 6 sn)");
            sb.AppendLine("  `2/spam stop`o          : Otomatik mesajı durdurur");
            sb.AppendLine("  `2/spam`o               : Çalışıyorsa durdurur, çalışmıyorsa bu menüyü gösterir");

            SendMsg(engine, isFromClient, sb.ToString().TrimEnd());
        }

        private void SendMsg(ProxyEngine engine, bool isFromClient, string msg)
        {
            if (isFromClient)
            {
                engine.SendClientMessage(msg);
            }
            else
            {
                Logger.Info(msg.Replace("`", ""));
            }
        }
    }
}

