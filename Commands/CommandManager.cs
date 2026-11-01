using System;
using System.Collections.Generic;
using System.Linq;
using GtProxyMod.Core;
using GtProxyMod.Network;
using GtProxyMod.State;

namespace GtProxyMod.Commands
{
    public class CommandManager
    {
        private readonly Dictionary<string, ICommand> _commands = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyCollection<ICommand> AllCommands => _commands.Values.Distinct().ToList();

        public void Register(ICommand command)
        {
            _commands[command.Name] = command;
            foreach (var alias in command.Aliases)
            {
                _commands[alias] = command;
            }
        }

        public bool HandleChatInput(string rawText, ProxyEngine engine, ProxyState state)
        {
            if (string.IsNullOrWhiteSpace(rawText))
                return false;

            var prefix = engine.Config.CommandPrefix;
            if (!rawText.StartsWith(prefix))
                return false;

            var trimmed = rawText.Substring(prefix.Length).Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                return false;

            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var cmdName = parts[0];
            var args = parts.Skip(1).ToArray();

            if (_commands.TryGetValue(cmdName, out var command))
            {
                Logger.Command($"Oyuncu komut çalıştırdı: {rawText}");
                try
                {
                    command.Execute(args, engine, state, isFromClient: true);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Komut çalıştırma hatası ({cmdName}): {ex.Message}");
                    engine.SendClientMessage($"`4[Hata]`o Komut çalıştırılırken sorun oluştu: {ex.Message}");
                }
                return true; // Command intercepted! Do NOT send to Growtopia server
            }

            engine.SendClientMessage($"`4[Mod]`o Bilinmeyen komut: `{cmdName}`. Tüm komutlar için `2/help`o yazabilirsiniz.");
            return true;
        }

        public bool HandleConsoleInput(string inputLine, ProxyEngine engine, ProxyState state)
        {
            if (string.IsNullOrWhiteSpace(inputLine))
                return false;

            var trimmed = inputLine.Trim();
            if (trimmed.StartsWith(engine.Config.CommandPrefix))
                trimmed = trimmed.Substring(engine.Config.CommandPrefix.Length).Trim();

            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return false;

            var cmdName = parts[0];
            var args = parts.Skip(1).ToArray();

            if (_commands.TryGetValue(cmdName, out var command))
            {
                try
                {
                    command.Execute(args, engine, state, isFromClient: false);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Konsol komut hatası ({cmdName}): {ex.Message}");
                }
                return true;
            }

            Logger.Warning($"Bilinmeyen konsol komutu: '{cmdName}'. Komut listesi için 'help' yazın.");
            return false;
        }
    }
}
