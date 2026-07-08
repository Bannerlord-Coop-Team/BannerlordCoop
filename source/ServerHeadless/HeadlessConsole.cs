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
        private static readonly string[] BuiltinCommands = { "help", "list", "commands", "events", "nav", "quit", "exit", "stop" };

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
                case "nav":
                    PrintNavQuery(args);
                    return;
                case "events":
                    PrintMapEvents();
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

        /// <summary>
        /// Nav-grid diagnostics: <c>nav x y</c> prints the face under a position; <c>nav x1 y1 x2 y2</c>
        /// additionally runs a pathfind between the two points.
        /// </summary>
        private static void PrintNavQuery(string args)
        {
            var grid = Bootstrap.HeadlessNavGrid.Instance;
            if (grid == null)
            {
                Console.WriteLine("No nav grid loaded.");
                return;
            }

            var parts = args.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            // "nav towns": town positions + their grid cells, for picking pathfinding test points.
            if (parts.Length >= 1 && parts[0].Equals("towns", StringComparison.OrdinalIgnoreCase))
            {
                PrintTowns(grid);
                return;
            }

            // "nav wet": every party currently positioned on a water face — who they are, whether
            // the position says land, their navigation capability and current behavior. The tool
            // for "parties are walking across water" reports.
            if (parts.Length >= 1 && parts[0].Equals("wet", StringComparison.OrdinalIgnoreCase))
            {
                PrintWetParties(grid);
                return;
            }

            // "nav holes x y r": count off-mesh cells (navmesh holes, e.g. settlement footprints)
            // in a box of radius r around a position, with the hole extents.
            if (parts.Length >= 4 && parts[0].Equals("holes", StringComparison.OrdinalIgnoreCase)
                && float.TryParse(parts[1], out float hx) && float.TryParse(parts[2], out float hy)
                && float.TryParse(parts[3], out float hr))
            {
                int offMesh = 0, total = 0;
                float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
                for (float dy = -hr; dy <= hr; dy += grid.CellSize)
                {
                    for (float dx = -hr; dx <= hr; dx += grid.CellSize)
                    {
                        total++;
                        var pt = new TaleWorlds.Library.Vec2(hx + dx, hy + dy);
                        if (grid.OrdinalAt(pt) < 0)
                        {
                            offMesh++;
                            if (pt.x < minX) minX = pt.x;
                            if (pt.x > maxX) maxX = pt.x;
                            if (pt.y < minY) minY = pt.y;
                            if (pt.y > maxY) maxY = pt.y;
                        }
                    }
                }
                Console.WriteLine(offMesh > 0
                    ? $"holes around ({hx:0.#},{hy:0.#}) r={hr:0.#}: {offMesh}/{total} off-mesh, extents ({minX:0.#},{minY:0.#})-({maxX:0.#},{maxY:0.#})"
                    : $"holes around ({hx:0.#},{hy:0.#}) r={hr:0.#}: none ({total} cells all on-mesh)");
                return;
            }

            if (parts.Length < 2 || !float.TryParse(parts[0], out float x) || !float.TryParse(parts[1], out float y))
            {
                Console.WriteLine("Usage: nav towns | nav <x> <y> [x2 y2]");
                return;
            }

            var from = new TaleWorlds.Library.Vec2(x, y);
            var face = grid.FaceRecordAt(from);
            Console.WriteLine($"({x:0.##},{y:0.##}): face={face.FaceIndex} group={face.FaceGroupIndex} island={face.FaceIslandIndex} " +
                              $"terrain={grid.TerrainAt(from)} water={grid.IsWaterAt(from)}");

            if (parts.Length >= 4 && float.TryParse(parts[2], out float x2) && float.TryParse(parts[3], out float y2))
            {
                var to = new TaleWorlds.Library.Vec2(x2, y2);
                var toFace = grid.FaceRecordAt(to);
                Console.WriteLine($"({x2:0.##},{y2:0.##}): face={toFace.FaceIndex} terrain={grid.TerrainAt(to)}");

                bool landClear = grid.IsLineClear(from, to, Bootstrap.HeadlessNavGrid.DefaultLandExclusions);
                Console.WriteLine($"land line clear: {landClear}");

                var points = new System.Collections.Generic.List<TaleWorlds.Library.Vec2>();
                bool ok = grid.TryFindPath(from, to, Bootstrap.HeadlessNavGrid.DefaultLandExclusions, 10, 10, points, out float cost);
                if (ok)
                {
                    int wet = points.Count(p => grid.IsWaterAt(p));
                    // Parties lerp straight between consecutive path points, so every chord must
                    // be walkable; a blocked segment means the path cuts a navmesh hole
                    // (settlement) or excluded terrain. The final hop is reported separately —
                    // a destination inside a settlement hole is legitimate.
                    int blocked = 0;
                    var prev = from;
                    for (int i = 0; i < points.Count - 1; i++)
                    {
                        if (!grid.IsLineClear(prev, points[i], Bootstrap.HeadlessNavGrid.DefaultLandExclusions))
                        {
                            blocked++;
                            Console.WriteLine($"  blocked segment {i}: ({prev.x:0.##},{prev.y:0.##}) -> ({points[i].x:0.##},{points[i].y:0.##}) " +
                                              $"[fromTerrain={grid.TerrainAt(prev)} toTerrain={grid.TerrainAt(points[i])}]");
                        }
                        prev = points[i];
                    }
                    bool lastHopClear = points.Count == 0 ||
                        grid.IsLineClear(prev, points[points.Count - 1], Bootstrap.HeadlessNavGrid.DefaultLandExclusions);
                    Console.WriteLine($"land path OK: cost={cost:0.#}, {points.Count} waypoints, {wet} on water, " +
                                      $"{blocked} blocked segments, last hop clear: {lastHopClear}");
                }
                else
                {
                    Console.WriteLine("land path FAILED");
                }

                // The AI's view: same query with the PartyNavigationModel's full invalid-terrain
                // list (rivers, mountains, canyons... — much stricter than the sea-only default).
                var model = TaleWorlds.CampaignSystem.Campaign.Current?.Models?.PartyNavigationModel;
                var aiExclusions = model?.GetInvalidTerrainTypesForNavigationType(
                    TaleWorlds.CampaignSystem.Party.MobileParty.NavigationType.Default);
                if (aiExclusions != null)
                {
                    var aiPoints = new System.Collections.Generic.List<TaleWorlds.Library.Vec2>();
                    bool aiOk = grid.TryFindPath(from, to, aiExclusions, 10, 10, aiPoints, out float aiCost);
                    Console.WriteLine(aiOk
                        ? $"AI path OK: cost={aiCost:0.#}, {aiPoints.Count} waypoints"
                        : "AI path FAILED");
                }
            }
        }

        /// <summary>Active map events (battles, raids, sieges) — the server-side truth behind map icons.</summary>
        private static void PrintMapEvents()
        {
            var campaign = TaleWorlds.CampaignSystem.Campaign.Current;
            var events = campaign?.MapEventManager?.MapEvents;
            if (events == null)
            {
                Console.WriteLine("No campaign.");
                return;
            }

            foreach (var e in events.ToList())
            {
                string attacker = e.AttackerSide?.LeaderParty?.Name?.ToString() ?? "?";
                string defender = e.DefenderSide?.LeaderParty?.Name?.ToString() ?? "?";
                var pos = e.Position.ToVec2();
                string place = e.MapEventSettlement?.Name?.ToString() ?? $"({pos.x:0.#},{pos.y:0.#})";
                Console.WriteLine($"  {e.EventType}: {attacker} vs {defender} at {place}");
            }
            Console.WriteLine($"{events.Count} active map event(s)");
        }

        private static void PrintWetParties(Bootstrap.HeadlessNavGrid grid)
        {
            var campaign = TaleWorlds.CampaignSystem.Campaign.Current;
            if (campaign?.MobileParties == null)
            {
                Console.WriteLine("No campaign.");
                return;
            }

            int count = 0;
            foreach (var party in campaign.MobileParties.ToList())
            {
                var pos = party.Position.ToVec2();
                bool offMesh = grid.OrdinalAt(pos) < 0;
                if (!offMesh && !grid.IsWaterAt(pos)) continue;

                count++;
                var target = party.TargetPosition.ToVec2();
                Console.WriteLine(
                    $"  {party.StringId} \"{party.Name}\" at ({pos.x:0.#},{pos.y:0.#}) " +
                    $"{(offMesh ? "OFF-MESH" : "water")} IsOnLand={party.Position.IsOnLand} " +
                    $"navCap={party.NavigationCapability} behavior={party.ShortTermBehavior} " +
                    $"target=({target.x:0.#},{target.y:0.#})");
            }
            Console.WriteLine($"{count} part(ies) on water/off-mesh of {campaign.MobileParties.Count}");
        }

        private static void PrintTowns(Bootstrap.HeadlessNavGrid grid)
        {
            var campaign = TaleWorlds.CampaignSystem.Campaign.Current;
            if (campaign == null)
            {
                Console.WriteLine("No campaign.");
                return;
            }

            foreach (var settlement in campaign.Settlements.Where(s => s.IsTown))
            {
                var p = settlement.GatePosition.ToVec2();
                Console.WriteLine($"  {settlement.StringId} \"{settlement.Name}\" at ({p.x:0.#},{p.y:0.#}) " +
                                  $"ordinal={grid.OrdinalAt(p)} terrain={grid.TerrainAt(p)}");
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
