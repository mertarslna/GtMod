using System;
using System.Threading.Tasks;
using ENet;
using GtProxyMod.Commands;
using GtProxyMod.Core;
using GtProxyMod.Network;
using GtProxyMod.State;

namespace GtProxyMod
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            Console.Title = "Growtopia Official ENet Proxy Mod";

            PrintBanner();

            // 1. Initialize ENet native library
            try
            {
                if (!Library.Initialize())
                {
                    Logger.Error("ENet kütüphanesi başlatılamadı!");
                    return;
                }
                Logger.Success("ENet kütüphanesi başarıyla yüklendi.");
            }
            catch (Exception ex)
            {
                Logger.Error($"ENet kütüphanesi yüklenirken hata: {ex.Message}");
                return;
            }

            // 2. Setup Configuration & State
            var config = new ProxyConfig();
            var state = new ProxyState();
            var commandManager = new CommandManager();

            // 3. Register Commands
            commandManager.Register(new HelpCommand());
            commandManager.Register(new GhostCommand());
            commandManager.Register(new WarpCommand());
            commandManager.Register(new StatusCommand());
            commandManager.Register(new LogCommand());
            commandManager.Register(new OverlayCommand());
            commandManager.Register(new PosCommand());
            commandManager.Register(new SpamCommand());
            commandManager.Register(new AutoclaveCommand());
            commandManager.Register(new StitchesCommand());
            commandManager.Register(new TargetCommand());
            commandManager.Register(new ResolveCommand());

            // 4. Resolve Official Server
            ServerDataInfo serverData = new();
            if (config.AutoResolveOfficialServer)
            {
                Logger.Info("Resmi Growtopia sunucu adresleri kontrol ediliyor...");
                serverData = await OfficialServerResolver.ResolveAsync();
                config.TargetHost = serverData.Server;
                config.TargetPort = serverData.Port;
            }

            // 5. Start HTTP Redirect Server (for server_data.php)
            var httpServer = new HttpServer(config, serverData);
            httpServer.Start();

            // 6. Start ENet Proxy Engine
            using var engine = new ProxyEngine(config, state, commandManager);
            engine.Start();

            PrintConnectionGuide(config);

            // 7. Interactive Terminal Input Loop
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                Logger.Warning("Proxy kapatılıyor...");
                httpServer.Stop();
                engine.Dispose();
                Library.Deinitialize();
                Environment.Exit(0);
            };

            Logger.Info("Konsola komut yazabilirsiniz ('help' veya 'quit'):");
            while (true)
            {
                var input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input))
                    continue;

                var trimmed = input.Trim();
                if (trimmed.Equals("quit", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                commandManager.HandleConsoleInput(trimmed, engine, state);
            }

            // Clean shutdown
            httpServer.Stop();
            engine.Dispose();
            Library.Deinitialize();
            Logger.Info("Program sonlandırıldı.");
        }

        private static void PrintBanner()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"
   ____ _   ____                         __  __           _ 
  / ___| |_|  _ \ _ __ _____  ___   _   |  \/  | ___   __| |
 | |  _| __| |_) | '__/ _ \ \/ / | | |  | |\/| |/ _ \ / _` |
 | |_| | |_|  __/| | | (_) >  <| |_| |  | |  | | (_) | (_| |
  \____|\__|_|   |_|  \___/_/\_\\__, |  |_|  |_|\___/ \__,_|
                                |___/                       
            -- Official Growtopia ENet Proxy Mod --
");
            Console.ResetColor();
        }

        private static void PrintConnectionGuide(ProxyConfig config)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("-----------------------------------------------------------------");
            Console.WriteLine("  BAĞLANTI TALİMATLARI:");
            Console.WriteLine($"  1. Proxy yerel portu: 127.0.0.1:{config.ListenPort}");
            Console.WriteLine($"  2. Hedef Resmi Sunucu: {config.TargetHost}:{config.TargetPort}");
            Console.WriteLine("  3. Oyunu proxy üzerinden bağlamak için hosts dosyanıza ekleyin:");
            Console.WriteLine("     127.0.0.1 www.growtopia1.com");
            Console.WriteLine("     127.0.0.1 www.growtopia2.com");
            Console.WriteLine("  4. Oyun içi sohbette '/help' yazarak tüm hile/mod komutlarını görün!");
            Console.WriteLine("-----------------------------------------------------------------");
            Console.ResetColor();
        }
    }
}
