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

            // "nav stuck": every party positioned on PATH-INVALID terrain (river, mountain,
            // canyon... — the PartyNavigationModel's list). Spawn placement is more permissive
            // than pathfinding, so parties can be placed where the AI cannot path from.
            if (parts.Length >= 1 && parts[0].Equals("stuck", StringComparison.OrdinalIgnoreCase))
            {
                PrintStuckParties(grid);
                return;
            }

            // "nav party <stringId or name fragment>": full navigation diagnosis of one party —
            // position cell, snapped face record (and its LAYER group), cached navigation face,
            // behavior and target. The tool for "party X is stuck holding" reports.
            if (parts.Length >= 2 && parts[0].Equals("party", StringComparison.OrdinalIgnoreCase))
            {
                PrintPartyDiagnosis(grid, string.Join(" ", parts.Skip(1)));
                return;
            }

            // "nav rescue": teleport parties stranded on off-mesh/path-invalid cells (legacy
            // strandings from earlier pathfinding bugs, or naval-only spots) to the nearest
            // walkable point. Operator-invoked one-time repair.
            if (parts.Length >= 1 && parts[0].Equals("rescue", StringComparison.OrdinalIgnoreCase))
            {
                RescueStuckParties(grid);
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

        private static void PrintPartyDiagnosis(Bootstrap.HeadlessNavGrid grid, string query)
        {
            var campaign = TaleWorlds.CampaignSystem.Campaign.Current;
            if (campaign?.MobileParties == null)
            {
                Console.WriteLine("No campaign.");
                return;
            }

            var matches = campaign.MobileParties
                .Where(p => (p.StringId?.IndexOf(query, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                         || (p.Name?.ToString()?.IndexOf(query, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0)
                .Take(5)
                .ToList();
            if (matches.Count == 0)
            {
                Console.WriteLine($"No party matches '{query}'.");
                return;
            }

            foreach (var party in matches)
            {
                var pos = party.Position.ToVec2();
                var snapped = grid.FaceRecordAt(pos);
                var cached = party.CurrentNavigationFace;
                var target = party.TargetPosition.ToVec2();
                var targetFace = grid.FaceRecordAt(target);

                Console.WriteLine($"  {party.StringId} \"{party.Name}\"");
                Console.WriteLine($"    pos=({pos.x:0.#},{pos.y:0.#}) cell={(grid.OrdinalAt(pos) < 0 ? "OFF-MESH" : grid.TerrainAt(pos).ToString())} " +
                                  $"posFace={snapped.FaceIndex}/g{snapped.FaceGroupIndex}/i{snapped.FaceIslandIndex} " +
                                  $"IsOnLand={party.Position.IsOnLand} posValid={party.Position.IsValid()}");
                Console.WriteLine($"    cachedNavFace={cached.FaceIndex}/g{cached.FaceGroupIndex}/i{cached.FaceIslandIndex} " +
                                  $"navCap={party.NavigationCapability} desiredNav={party.DesiredAiNavigationType}");
                Console.WriteLine($"    behavior={party.ShortTermBehavior} default={party.DefaultBehavior} " +
                                  $"target=({target.x:0.#},{target.y:0.#}) targetFace={targetFace.FaceIndex}/g{targetFace.FaceGroupIndex} " +
                                  $"targetSettlement={party.TargetSettlement?.Name?.ToString() ?? "-"}");
                Console.WriteLine($"    army={party.Army?.Name?.ToString() ?? "-"} attachedTo={party.AttachedTo?.StringId ?? "-"} " +
                                  $"inSettlement={party.CurrentSettlement?.Name?.ToString() ?? "-"} " +
                                  $"isActive={party.IsActive} isMoving={party.IsMoving} speed={party.Speed:0.##}");
            }
        }

        /// <summary>
        /// Teleports every party stranded on off-mesh or path-invalid terrain to the nearest
        /// walkable point (up to 64 cells away). Fixes legacy strandings left behind by earlier
        /// pathfinding bugs and parties parked at naval-only shores.
        /// </summary>
        private static void RescueStuckParties(Bootstrap.HeadlessNavGrid grid)
        {
            var campaign = TaleWorlds.CampaignSystem.Campaign.Current;
            var model = campaign?.Models?.PartyNavigationModel;
            if (campaign?.MobileParties == null || model == null)
            {
                Console.WriteLine("No campaign.");
                return;
            }

            var exclusions = model.GetInvalidTerrainTypesForNavigationType(
                TaleWorlds.CampaignSystem.Party.MobileParty.NavigationType.Default);

            int rescued = 0, unrescuable = 0, flagsFixed = 0;
            foreach (var party in campaign.MobileParties.ToList())
            {
                if (party.CurrentSettlement != null || party.MapEvent != null) continue;

                var pos = party.Position.ToVec2();
                bool offMesh = grid.OrdinalAt(pos) < 0;
                if (!offMesh && model.IsTerrainTypeValidForNavigationType(
                        grid.TerrainAt(pos), TaleWorlds.CampaignSystem.Party.MobileParty.NavigationType.Default))
                {
                    // On walkable land but with a poisoned IsOnLand=false flag: CampaignVec2
                    // .IsValid() then validates the land face against NAVAL rules (always false
                    // in the default model), GetClosestSettlement asserts "Mobileparty is
                    // nowhere to be found", and the lord AI holds forever. Naval transitions
                    // never run headless, so the flag cannot self-heal.
                    if (!party.Position.IsOnLand)
                    {
                        party.Position = new TaleWorlds.CampaignSystem.CampaignVec2(pos, true);
                        flagsFixed++;
                        Console.WriteLine($"  fixed IsOnLand for {party.StringId} \"{party.Name}\" at ({pos.x:0.#},{pos.y:0.#})");
                    }
                    continue;
                }

                if (grid.TryGetNearestAllowedPoint(pos, exclusions, 64f, out var found))
                {
                    // Server-authoritative teleport: the position set replicates to clients like
                    // any other server-side move (the party re-thinks from the new spot).
                    party.Position = new TaleWorlds.CampaignSystem.CampaignVec2(found, true);
                    rescued++;
                    Console.WriteLine($"  rescued {party.StringId} \"{party.Name}\" ({pos.x:0.#},{pos.y:0.#}) -> ({found.x:0.#},{found.y:0.#})");
                }
                else
                {
                    unrescuable++;
                    Console.WriteLine($"  NO walkable point within 64u of {party.StringId} at ({pos.x:0.#},{pos.y:0.#})");
                }
            }
            Console.WriteLine($"rescued {rescued} part(ies); fixed {flagsFixed} IsOnLand flag(s); {unrescuable} beyond rescue radius");
        }

        /// <summary>Parties standing on terrain the navigation model rejects for land pathing.</summary>
        private static void PrintStuckParties(Bootstrap.HeadlessNavGrid grid)
        {
            var campaign = TaleWorlds.CampaignSystem.Campaign.Current;
            var model = campaign?.Models?.PartyNavigationModel;
            if (campaign?.MobileParties == null || model == null)
            {
                Console.WriteLine("No campaign.");
                return;
            }

            int count = 0;
            foreach (var party in campaign.MobileParties.ToList())
            {
                if (party.CurrentSettlement != null) continue; // parked in a settlement = fine

                var pos = party.Position.ToVec2();
                var terrain = grid.TerrainAt(pos);
                bool offMesh = grid.OrdinalAt(pos) < 0;
                if (!offMesh && model.IsTerrainTypeValidForNavigationType(
                        terrain, TaleWorlds.CampaignSystem.Party.MobileParty.NavigationType.Default))
                    continue;

                count++;
                if (count <= 30)
                {
                    var target = party.TargetPosition.ToVec2();
                    Console.WriteLine(
                        $"  {party.StringId} \"{party.Name}\" at ({pos.x:0.#},{pos.y:0.#}) " +
                        $"{(offMesh ? "OFF-MESH" : terrain.ToString())} behavior={party.ShortTermBehavior} " +
                        $"target=({target.x:0.#},{target.y:0.#})");
                }
            }
            Console.WriteLine($"{count} part(ies) on path-invalid terrain of {campaign.MobileParties.Count}");
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
