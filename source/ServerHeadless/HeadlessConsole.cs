using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using TaleWorlds.Library;

namespace ServerHeadless
{
    /// <summary>
    /// Interactive operator console for the headless server.
    ///
    /// A frontend (the pinned-prompt <see cref="InteractiveConsole"/>, or a plain stdin reader
    /// when stdio is redirected) queues submitted lines; the game-loop thread drains the queue
    /// once per tick (<see cref="PumpCommands"/>) and executes the commands there, since game
    /// commands mutate campaign state that is only safe to touch on the game thread.
    ///
    /// The commands are the game's own console commands (<see cref="CommandLineFunctionality"/>):
    /// everything registered with <c>[CommandLineArgumentFunction]</c> — the native cheats
    /// (<c>campaign.*</c>, gated on cheat mode, see <c>NativeConfigPatches</c>) and the Coop debug
    /// commands (<c>coop.debug.*</c>). The native engine normally collects these at engine init
    /// (<c>ManagedExtensions.CollectCommandLineFunctions</c>), which never runs headless, so
    /// <see cref="Start"/> collects them explicitly.
    /// </summary>
    internal static class HeadlessConsole
    {
        private static readonly ConcurrentQueue<string> PendingCommands = new ConcurrentQueue<string>();
        private static bool _started;
        private static Action _requestShutdown;

        /// <summary>Local console verbs, also offered to tab completion alongside the game commands.</summary>
        private static readonly string[] BuiltinCommands = { "help", "list", "commands", "quit", "exit", "stop" };

        /// <summary>
        /// Registers the game commands and starts the console frontend: the pinned-prompt
        /// interactive console on a real terminal (line editing, history, tab completion), or a
        /// plain stdin reader when stdio is redirected (piped input, docker logs, CI). Call after
        /// the save is loaded, so the Coop assemblies (and their commands) are in the AppDomain.
        /// </summary>
        public static void Start(Action requestShutdown)
        {
            if (_started) return;
            _started = true;
            _requestShutdown = requestShutdown;

            // Scans every loaded assembly for [CommandLineArgumentFunction] methods. Idempotent
            // (already-known names are skipped); returns the newly registered ones.
            int count = CommandLineFunctionality.CollectCommandLineFunctions().Count;

            bool interactive = InteractiveConsole.TryStart(
                line => PendingCommands.Enqueue(line),
                () => BuiltinCommands.Concat(GetAllCommandNames()));

            Console.WriteLine($"[ServerHeadless] Console ready: {count} game commands registered. Type 'help' for usage."
                + (interactive ? " (tab completes, up/down recall history)" : ""));

            if (!interactive)
            {
                var readerThread = new Thread(ReadLoop)
                {
                    Name = "HeadlessConsole",
                    // Never keeps the process alive: on shutdown the thread is still blocked in
                    // ReadLine and is simply torn down with the process.
                    IsBackground = true,
                };
                readerThread.Start();
            }
        }

        private static void ReadLoop()
        {
            while (true)
            {
                string line;
                try
                {
                    line = Console.ReadLine();
                }
                catch (Exception)
                {
                    return; // console detached
                }

                if (line == null) return; // stdin closed (e.g. redirected input exhausted)

                if (!string.IsNullOrWhiteSpace(line))
                {
                    PendingCommands.Enqueue(line.Trim());
                }
            }
        }

        /// <summary>Executes the queued console commands. Must run on the game-loop thread.</summary>
        public static void PumpCommands()
        {
            while (PendingCommands.TryDequeue(out string line))
            {
                Execute(line);
            }
        }

        private static void Execute(string line)
        {
            // First token = command name, remainder = argument text (the in-game console's split;
            // CallFunction itself splits the argument text on spaces).
            int space = line.IndexOf(' ');
            string name = space < 0 ? line : line.Substring(0, space);
            string args = space < 0 ? string.Empty : line.Substring(space + 1).Trim();

            switch (name.ToLowerInvariant())
            {
                case "help":
                    PrintHelp();
                    return;
                case "list":
                case "commands":
                    PrintCommands(args);
                    return;
                case "quit":
                case "exit":
                case "stop":
                    _requestShutdown?.Invoke();
                    return;
            }

            try
            {
                string result = CommandLineFunctionality.CallFunction(name, args, out _);
                if (!string.IsNullOrWhiteSpace(result))
                {
                    Console.WriteLine(result.TrimEnd());
                }
            }
            catch (Exception ex)
            {
                // A bad command must not take the server down; report it and keep ticking.
                Console.Error.WriteLine($"[ServerHeadless] Command '{name}' failed: {ex}");
            }
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Console usage:");
            Console.WriteLine("  <command> [args...]   run a game console command (e.g. campaign.add_gold_to_hero)");
            Console.WriteLine("  list [filter]         list game commands, optionally filtered (e.g. list coop.debug)");
            Console.WriteLine("  help                  this text");
            Console.WriteLine("  quit | exit | stop    shut the server down");
        }

        private static void PrintCommands(string filter)
        {
            List<string> names = GetAllCommandNames()
                .Where(n => string.IsNullOrEmpty(filter) || n.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (string name in names)
            {
                Console.WriteLine("  " + name);
            }
            Console.WriteLine($"{names.Count} command(s)" + (string.IsNullOrEmpty(filter) ? "" : $" matching '{filter}'"));
        }

        /// <summary>
        /// All registered command names. CommandLineFunctionality keeps them in a private static
        /// dictionary and TaleWorlds.Library is not publicized in this project, so read it by
        /// reflection.
        /// </summary>
        private static IEnumerable<string> GetAllCommandNames()
        {
            FieldInfo field = typeof(CommandLineFunctionality)
                .GetField("AllFunctions", BindingFlags.NonPublic | BindingFlags.Static);
            return ((System.Collections.IDictionary)field.GetValue(null)).Keys.Cast<string>();
        }
    }
}
