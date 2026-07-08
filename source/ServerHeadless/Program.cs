using ServerHeadless.Bootstrap;
using ServerHeadless.Bootstrap.Patches;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem;

namespace ServerHeadless
{
    /// <summary>
    /// Fully headless Bannerlord campaign host (no native engine, no graphics).
    ///
    /// Boot sequence:
    ///   1. Resolve the game directories and install the assembly resolver (see <see cref="Main"/>).
    ///   2. Headless bootstrap: Harmony-mock the native-only methods, init the object manager and
    ///      module, install the file save driver (<see cref="HeadlessBootstrap"/>).
    ///   3. Present the local save games in a console menu.
    ///   4. Load the chosen save (deserialize into <see cref="Campaign.Current"/>).
    ///   5. (next milestone) tick the campaign.
    ///
    /// CTRL+C requests a graceful shutdown.
    /// </summary>
    internal static class Program
    {
        /// <summary>Target simulation rate for the headless game loop.</summary>
        private const int TicksPerSecond = 60;

        private static readonly CancellationTokenSource Shutdown = new CancellationTokenSource();

        /// <summary>Resolved game binary directory (bin\Win64_Shipping_Client).</summary>
        private static string GameBinDirectory;

        /// <summary>Deployed Coop module binary directory (Modules\Coop\bin\Win64_Shipping_Client).</summary>
        private static string ModuleBinDirectory;

        /// <summary>Game root directory (the mb2 install root).</summary>
        private static string GameRootDirectory;

        /// <summary>
        /// Specific save to host (<c>--save &lt;name&gt;</c> or <c>BANNERLORD_SAVE</c>). Null means
        /// the default behavior: host the latest save, or create a new campaign when none exist.
        /// </summary>
        private static string PreselectedSaveName;

        /// <summary>Show the interactive save menu instead of the automatic default (<c>--menu</c>).</summary>
        private static bool ShowSaveMenu;

        /// <summary>
        /// Minutes between autosaves (<c>--autosave-minutes</c> or <c>BANNERLORD_AUTOSAVE_MINUTES</c>;
        /// 0 disables). Saves go through the game's own rotating autosave slots.
        /// </summary>
        private static int AutoSaveMinutes = 10;

        /// <summary>
        /// Thin trampoline. Resolves the game directories and installs the assembly resolver
        /// BEFORE any game type is referenced, then hands off to <see cref="Run"/>. Keeping the
        /// TaleWorlds / Coop type usage out of this method ensures the resolver is registered
        /// before those assemblies are first probed (which happens when <see cref="Run"/> is JITed).
        /// </summary>
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                string[] positional = ParseOptions(args);

                GameBinDirectory = ResolveGameBinDirectory(positional);
                ModuleBinDirectory = Path.GetFullPath(
                    Path.Combine(GameBinDirectory, "..", "..", "Modules", "Coop", "bin", "Win64_Shipping_Client"));
                // Game root = ...\mb2 (two levels above bin\Win64_Shipping_Client).
                GameRootDirectory = Path.GetFullPath(Path.Combine(GameBinDirectory, "..", ".."));

                // Native DLLs and relative game paths resolve against the working directory.
                Directory.SetCurrentDirectory(GameBinDirectory);

                AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[ServerHeadless] Startup error:");
                Console.Error.WriteLine(ex);
                return 1;
            }

            return Run();
        }

        /// <summary>
        /// Splits options out of the command line and returns the remaining positional arguments
        /// (the optional game bin directory). Options: <c>--save &lt;name&gt;</c> hosts a specific
        /// save, <c>--menu</c> shows the interactive save menu, <c>--autosave-minutes &lt;n&gt;</c>
        /// sets the autosave interval (0 disables). The <c>BANNERLORD_SAVE</c> and
        /// <c>BANNERLORD_AUTOSAVE_MINUTES</c> environment variables are the fallbacks.
        /// </summary>
        private static string[] ParseOptions(string[] args)
        {
            var positional = new System.Collections.Generic.List<string>();
            bool autoSaveSetByArg = false;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("--save", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    PreselectedSaveName = args[++i];
                }
                else if (args[i].Equals("--menu", StringComparison.OrdinalIgnoreCase))
                {
                    ShowSaveMenu = true;
                }
                else if (args[i].Equals("--autosave-minutes", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length
                         && int.TryParse(args[++i], out int minutes))
                {
                    AutoSaveMinutes = minutes;
                    autoSaveSetByArg = true;
                }
                else
                {
                    positional.Add(args[i]);
                }
            }

            if (string.IsNullOrEmpty(PreselectedSaveName))
            {
                PreselectedSaveName = Environment.GetEnvironmentVariable("BANNERLORD_SAVE");
            }

            if (!autoSaveSetByArg
                && int.TryParse(Environment.GetEnvironmentVariable("BANNERLORD_AUTOSAVE_MINUTES"), out int envMinutes))
            {
                AutoSaveMinutes = envMinutes;
            }

            return positional.ToArray();
        }

        /// <summary>
        /// Resolves the game and Coop module assemblies (TaleWorlds.*, Common, GameInterface,
        /// Autofac, Serilog, …) from their install locations, since they are not copied next to
        /// this executable.
        /// </summary>
        private static System.Reflection.Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            string simpleName = new System.Reflection.AssemblyName(args.Name).Name;

            foreach (string dir in new[] { GameBinDirectory, ModuleBinDirectory })
            {
                if (dir == null) continue;
                string candidate = Path.Combine(dir, simpleName + ".dll");
                if (File.Exists(candidate))
                {
                    try
                    {
                        return System.Reflection.Assembly.LoadFrom(candidate);
                    }
                    catch (Exception)
                    {
                        // On .NET 6 a candidate can be unloadable (e.g. a System.* version the
                        // default context already holds a different version of). Report the
                        // original bind failure rather than dying inside the resolver.
                        return null;
                    }
                }
            }

            return null;
        }

        private static int Run()
        {
            // CTRL+C => request a graceful shutdown rather than killing the process outright.
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                Console.WriteLine();
                Console.WriteLine("[ServerHeadless] Shutdown requested (CTRL+C). Stopping game loop...");
                Shutdown.Cancel();
            };

            try
            {
                Console.WriteLine($"[ServerHeadless] Game binaries: {GameBinDirectory}");
                Console.WriteLine($"[ServerHeadless] Game root:     {GameRootDirectory}");

                Bootstrap();

                SaveGameFileInfo selectedSave = null;
                if (!string.IsNullOrEmpty(PreselectedSaveName))
                {
                    // An explicitly named save that is missing is an error, never a silent new game.
                    if (!TryFindSave(PreselectedSaveName, out selectedSave))
                    {
                        return 1;
                    }
                    Console.WriteLine($"[ServerHeadless] Hosting preselected save '{selectedSave.Name}'.");
                }
                else if (ShowSaveMenu)
                {
                    if (!PromptForSave(out selectedSave))
                    {
                        Console.WriteLine("[ServerHeadless] No save selected. Exiting.");
                        return 0;
                    }
                    // A null save here means the operator picked "[n] New game".
                }
                else
                {
                    // Default: host the latest save (GetSaveFiles is sorted newest first), or
                    // fall through to a fresh campaign when there are none.
                    SaveGameFileInfo[] saves = MBSaveLoad.GetSaveFiles();
                    if (saves != null && saves.Length > 0)
                    {
                        selectedSave = saves[0];
                        Console.WriteLine($"[ServerHeadless] Hosting latest save '{selectedSave.Name}'.");
                    }
                    else
                    {
                        Console.WriteLine("[ServerHeadless] No save games found.");
                    }
                }

                // Null save (no saves exist, or "New game" chosen): start a new campaign.
                bool started = selectedSave != null
                    ? LoadSelectedSave(selectedSave)
                    : StartNewCampaign();
                if (!started)
                {
                    return 1;
                }

                TestServerSave();

                // Interactive operator console: game console commands typed on stdin are queued
                // here and executed on this (game-loop) thread inside TickCampaign.
                HeadlessConsole.Start(() =>
                {
                    Console.WriteLine("[ServerHeadless] Shutdown requested (console). Stopping game loop...");
                    Shutdown.Cancel();
                });

                TickCampaign();
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown during startup.
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[ServerHeadless] Fatal error:");
                Console.Error.WriteLine(ex);
                return 1;
            }

            Console.WriteLine("[ServerHeadless] Stopped.");
            return 0;
        }

        /// <summary>
        /// Locates the game's <c>bin\Win64_Shipping_Client</c> directory containing TaleWorlds.Native.dll.
        /// Order of preference: explicit first argument, current directory, then a search upward for
        /// <c>mb2\bin\Win64_Shipping_Client</c> (the in-repo layout).
        /// </summary>
        private static string ResolveGameBinDirectory(string[] args)
        {
            if (args.Length > 0 && Directory.Exists(args[0]))
            {
                return Path.GetFullPath(args[0]);
            }

            if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "TaleWorlds.Native.dll")))
            {
                return Directory.GetCurrentDirectory();
            }

            DirectoryInfo dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, "mb2", "bin", "Win64_Shipping_Client");
                if (File.Exists(Path.Combine(candidate, "TaleWorlds.Native.dll")))
                {
                    return candidate;
                }
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the game binaries (TaleWorlds.Native.dll). " +
                "Pass the path to bin\\Win64_Shipping_Client as the first argument, " +
                "or run from that directory.");
        }

        private static void Bootstrap()
        {
            Console.WriteLine("[ServerHeadless] Headless bootstrap (Harmony native-mocks + object manager + module)...");

            // Point the (publicized) BasePath getter at the real game root so module/save paths resolve.
            BasePathPatches.GameRootPath = GameRootDirectory.Replace('\\', '/').TrimEnd('/') + "/";

            HeadlessBootstrap.Initialize(GameRootDirectory);

            Console.WriteLine("[ServerHeadless] Bootstrap complete.");
        }

        /// <summary>
        /// Resolves a save by name (case-insensitive). On failure reports the available saves so a
        /// container log shows what would have worked.
        /// </summary>
        private static bool TryFindSave(string saveName, out SaveGameFileInfo save)
        {
            SaveGameFileInfo[] saves = MBSaveLoad.GetSaveFiles() ?? Array.Empty<SaveGameFileInfo>();

            save = saves.FirstOrDefault(s => saveName.Equals(s.Name, StringComparison.OrdinalIgnoreCase));
            if (save != null) return true;

            Console.Error.WriteLine($"[ServerHeadless] Save '{saveName}' not found. Available saves: " +
                (saves.Length == 0 ? "<none>" : string.Join(", ", saves.Select(s => $"'{s.Name}'"))));
            return false;
        }

        /// <summary>
        /// Lists the local save games and lets the operator pick one to host, choose "[n] New game"
        /// to create a fresh campaign, or "[q] Quit". Returns false to quit; on true,
        /// <paramref name="save"/> is the selection — null meaning "create a new game".
        /// </summary>
        private static bool PromptForSave(out SaveGameFileInfo save)
        {
            save = null;

            // MBSaveLoad's save driver is installed by Module.Initialize, so this enumerates the
            // player's on-disk saves (newest first).
            SaveGameFileInfo[] saves = MBSaveLoad.GetSaveFiles() ?? Array.Empty<SaveGameFileInfo>();

            while (!Shutdown.IsCancellationRequested)
            {
                Console.WriteLine();
                Console.WriteLine("Available save games:");
                for (int i = 0; i < saves.Length; i++)
                {
                    Console.WriteLine($"  [{i + 1}] {saves[i].Name}");
                }
                Console.WriteLine("  [n] New game");
                Console.WriteLine("  [q] Quit");
                Console.Write("Select a save to host: ");

                string input = Console.ReadLine();
                if (input == null || input.Trim().Equals("q", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (input.Trim().Equals("n", StringComparison.OrdinalIgnoreCase))
                {
                    return true; // save stays null: create a new game
                }

                if (int.TryParse(input.Trim(), out int choice) && choice >= 1 && choice <= saves.Length)
                {
                    save = saves[choice - 1];
                    return true;
                }

                Console.WriteLine("Invalid selection, try again.");
            }

            return false;
        }

        /// <summary>
        /// Loads the selected save as a Co-op server through the Coop mod. Publishing
        /// <see cref="HostSaveGame"/> drives <c>CoopartiveMultiplayerExperience.StartAsServer()</c>
        /// (which stands up the Coop server + GameInterface patches) and then the LoadGame flow,
        /// which runs <c>MBGameManager.StartNewGame(new SandBoxGameManager(loadResult))</c>. We then
        /// drive the loading state machine until the game manager reports loaded; on success
        /// <see cref="Campaign.Current"/> is populated from the save.
        /// </summary>
        private static bool LoadSelectedSave(SaveGameFileInfo save)
        {
            Console.WriteLine($"[ServerHeadless] Hosting save '{save.Name}' as a Co-op server...");

            // This (main) thread is the Coop game-loop thread, then start the server + load the save
            // through the Coop mod (StartAsServer + HostSaveGame), driven by reflection.
            CoopServerLauncher.Initialize();
            CoopServerLauncher.AttachConsoleLog();
            CoopServerLauncher.HostSaveGameAsServer(save.Name);

            // HostSaveGame -> StartAsServer + LoadGame has pushed a GameLoadingState; advance it.
            const int maxTicks = 100_000;
            const float dt = 1f / TicksPerSecond;
            int ticks = 0;
            while ((GameManagerBase.Current as MBGameManager)?.IsLoaded != true && ticks < maxTicks && !Shutdown.IsCancellationRequested)
            {
                GameStateManager.Current.OnTick(dt);
                CoopServerLauncher.PumpGameLoop(TimeSpan.FromSeconds(dt));
                ticks++;
            }

            if ((GameManagerBase.Current as MBGameManager)?.IsLoaded != true)
            {
                Console.Error.WriteLine($"[ServerHeadless] Loading did not complete after {ticks} ticks.");
                return false;
            }

            Console.WriteLine($"[ServerHeadless] Loaded in {ticks} ticks.");
            ReportCampaign();
            ReportItems();

            // The campaign is loaded; signal the server to bind its socket and accept clients.
            // (The mod's graphical MapScreen.OnInitialize hook that normally does this never runs.)
            Console.WriteLine("[ServerHeadless] Signalling CampaignReady (starting network)...");
            CoopServerLauncher.SignalCampaignReady();

            return true;
        }

        /// <summary>Name of the packaged blank campaign and of the save it becomes.</summary>
        private const string DefaultSaveFileName = "default_new_game.sav";
        private const string NewCampaignSaveName = "NewCampaign";

        /// <summary>
        /// Starts a new campaign — preferably by copying the packaged blank save (a day-0 campaign
        /// created by the REAL game, so every scene-derived value, appearance and clock is genuine)
        /// into "Game Saves" and loading it through the proven load path. The in-process world
        /// generation remains only as a degraded fallback when no blank save is available.
        ///
        /// The blank save is searched in the user-data root and next to the executable
        /// (docker images can bake it beside the app). Create one by starting a new campaign in
        /// the real game and saving immediately, then copying the .sav as
        /// <see cref="DefaultSaveFileName"/>.
        /// </summary>
        private static bool StartNewCampaign()
        {
            string userRoot = HeadlessBootstrap.UserDataRoot;
            string[] candidates =
            {
                Path.Combine(userRoot, DefaultSaveFileName),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultSaveFileName),
            };

            string defaultSave = candidates.FirstOrDefault(File.Exists);
            if (defaultSave == null)
            {
                Console.WriteLine($"[ServerHeadless] No '{DefaultSaveFileName}' found (looked in '{candidates[0]}' and next to the exe).");
                Console.WriteLine("[ServerHeadless] Falling back to in-process world generation — generated-hero appearance is");
                Console.WriteLine("[ServerHeadless] approximated there; a blank save made in the real game gives pristine data.");
                return CreateNewGame();
            }

            string targetDir = Path.Combine(userRoot, "Game Saves");
            Directory.CreateDirectory(targetDir);

            // Unique name: "[n] New game" can be chosen when a NewCampaign save already exists.
            string saveName = NewCampaignSaveName;
            string target = Path.Combine(targetDir, saveName + ".sav");
            for (int i = 2; File.Exists(target); i++)
            {
                saveName = $"{NewCampaignSaveName}_{i}";
                target = Path.Combine(targetDir, saveName + ".sav");
            }

            File.Copy(defaultSave, target);
            Console.WriteLine($"[ServerHeadless] New campaign from packaged blank save: '{defaultSave}' -> '{target}'.");

            if (!TryFindSave(saveName, out SaveGameFileInfo save))
            {
                Console.Error.WriteLine("[ServerHeadless] Copied blank save was not found by the save system.");
                return false;
            }

            return LoadSelectedSave(save);
        }

        /// <summary>
        /// FALLBACK: creates a fresh sandbox campaign in-process.
        /// <see cref="CoopServerLauncher.HostNewGameAsServer"/> starts the server (patches + logic,
        /// no load) and queues the mod's StartNewGame; the loop below then pumps the loading state
        /// machine while <see cref="HeadlessNewGame"/> auto-advances the setup states a player
        /// would normally click through (intro video, character creation), until the new campaign
        /// reaches the map. Known gap versus a real-game blank save: generated-hero appearance is
        /// approximated (the face-generator bit layout is native; see HeadlessFaceGen).
        /// </summary>
        private static bool CreateNewGame()
        {
            Console.WriteLine("[ServerHeadless] Creating a new campaign as a Co-op server...");

            CoopServerLauncher.Initialize();
            CoopServerLauncher.AttachConsoleLog();
            CoopServerLauncher.HostNewGameAsServer();

            const int maxTicks = 100_000;
            const float dt = 1f / TicksPerSecond;
            int ticks = 0;
            while (!HeadlessNewGame.IsOnMap && ticks < maxTicks && !Shutdown.IsCancellationRequested)
            {
                // Advance BEFORE ticking: a freshly pushed setup state (intro video / character
                // creation) must be auto-completed before its UI-less OnTick can run.
                HeadlessNewGame.AdvanceSetupStep();
                GameStateManager.Current.OnTick(dt);
                CoopServerLauncher.PumpGameLoop(TimeSpan.FromSeconds(dt));
                ticks++;
            }

            if (!HeadlessNewGame.IsOnMap)
            {
                Console.Error.WriteLine($"[ServerHeadless] New campaign did not reach the map after {ticks} ticks.");
                return false;
            }

            Console.WriteLine($"[ServerHeadless] New campaign created in {ticks} ticks.");
            ReportCampaign();
            ReportItems();

            Console.WriteLine("[ServerHeadless] Signalling CampaignReady (starting network)...");
            CoopServerLauncher.SignalCampaignReady();

            return true;
        }

        /// <summary>Reports item/monster loading as a sanity check (horses need a valid Monster).</summary>
        private static void ReportItems()
        {
            var mgr = TaleWorlds.ObjectSystem.MBObjectManager.Instance;
            var items = mgr.GetObjectTypeList<ItemObject>();
            int horses = items.Count(i => i.Type == ItemObject.ItemTypeEnum.Horse);
            int monsters = mgr.GetObjectTypeList<Monster>().Count;
            var sampleHorse = items.FirstOrDefault(i => i.Type == ItemObject.ItemTypeEnum.Horse);
            string horseInfo = sampleHorse == null
                ? "none"
                : $"'{sampleHorse.StringId}' (Monster={sampleHorse.HorseComponent?.Monster?.StringId ?? "<null>"})";
            Console.WriteLine($"[ServerHeadless] Items: {items.Count} (horses: {horses}), Monsters: {monsters}, sample horse: {horseInfo}");
            // Note: multiplayer-only items (mpitems.xml, IncludedGameTypes=MultiplayerGame) are
            // intentionally not loaded for a campaign — same as the singleplayer client.
        }

        /// <summary>
        /// Exercises the server's state-transfer save (the same PackageGameSaveData request the
        /// server raises when a client connects) and reports the resulting byte size.
        /// </summary>
        private static void TestServerSave()
        {
            Console.WriteLine("[ServerHeadless] Packaging campaign save for transfer...");
            if (CoopServerLauncher.TrySaveCurrentState(out byte[] data, out string campaignId))
            {
                Console.WriteLine($"[ServerHeadless] Save OK: {data.Length:N0} bytes (campaign {campaignId}).");
            }
            else
            {
                Console.Error.WriteLine($"[ServerHeadless] Save failed ({data?.Length ?? 0} bytes).");
            }
        }

        private static void ReportCampaign()
        {
            Campaign campaign = Campaign.Current;
            if (campaign == null)
            {
                Console.Error.WriteLine("[ServerHeadless] WARNING: Campaign.Current is null after load.");
                return;
            }

            Console.WriteLine("[ServerHeadless] Campaign loaded:");
            Console.WriteLine($"    Heroes:      {campaign.AliveHeroes?.Count ?? 0} alive");
            Console.WriteLine($"    Parties:     {campaign.MobileParties?.Count ?? 0}");
            Console.WriteLine($"    Settlements: {campaign.Settlements?.Count ?? 0}");
            Console.WriteLine($"    Main hero:   {Hero.MainHero?.Name?.ToString() ?? "<none>"}");
        }

        /// <summary>
        /// Drives the campaign simulation. Mirrors MapState.OnMapModeTick: each frame calls
        /// <c>Campaign.RealTick(dt)</c> then <c>Campaign.Tick()</c> (the MapState UI Handler calls are
        /// null headless). Like the game's map screen, the timestep is variable: each tick advances
        /// game time by the measured wall-clock duration of the previous iteration, so campaign
        /// speed matches a graphical client no matter how long tick work takes. Runs until CTRL+C.
        /// </summary>
        private static void TickCampaign()
        {
            Campaign campaign = Campaign.Current;

            Console.WriteLine($"[ServerHeadless] Ticking campaign at {TicksPerSecond} TPS. Press CTRL+C to stop.");
            Console.WriteLine($"    Start time: {CampaignTime.Now}");
            Console.WriteLine(AutoSaveMinutes > 0
                ? $"    Auto-save: every {AutoSaveMinutes} minute(s)"
                : "    Auto-save: disabled");

            // Start campaign time through the mod (a raw TimeControlMode set is dropped by the
            // mod's patches — see CoopServerLauncher.StartCampaignTime). Clients can still pause.
            CoopServerLauncher.StartCampaignTime();

            var autoSaveTimer = System.Diagnostics.Stopwatch.StartNew();

            // Variable timestep. A fixed dt=1/60 here silently slowed the whole world down: each
            // iteration costs (tick work + sleep), which is always more than 16.7ms, yet advanced
            // game time by exactly 16.7ms — so campaign time (and party movement) ran at maybe
            // 60-75% of wall-clock speed while clients rendered a true 60fps. Instead, measure how
            // long the previous iteration really took and feed that to RealTick, sleeping only the
            // remainder of the frame budget.
            TimeSpan targetFrame = TimeSpan.FromSeconds(1.0 / TicksPerSecond);
            var frameTimer = System.Diagnostics.Stopwatch.StartNew();
            var tpsTimer = System.Diagnostics.Stopwatch.StartNew();
            float dt = (float)targetFrame.TotalSeconds;
            long tick = 0;
            long lastReportTick = 0;
            long errors = 0;
            string lastError = null;
            while (!Shutdown.IsCancellationRequested)
            {
                // The mod can legitimately end the game (e.g. a server-stop request goes through
                // MBGameManager.EndGame). Once the campaign is destroyed there is nothing left to
                // tick and no menu to return to — stop cleanly instead of erroring every tick.
                if (Campaign.Current != campaign)
                {
                    Console.WriteLine("[ServerHeadless] Campaign ended — stopping the tick loop.");
                    break;
                }

                // Resilient tick: the systematic headless gaps are patched out, but an arbitrary save
                // can still surface a rare edge case (e.g. raid loot) deep in a subsystem. A server
                // shouldn't die on a single bad tick — log the first occurrence of each distinct
                // failure and keep simulating.
                try
                {
                    campaign.RealTick(dt);
                    campaign.Tick();

                    // Pump the Coop server's main-thread work queue (network handlers etc.).
                    CoopServerLauncher.PumpGameLoop(targetFrame);

                    // Execute any console commands typed since the last tick (game-thread only).
                    HeadlessConsole.PumpCommands();

                    // Process queued saves — normally MapState.OnTick's job, which never runs
                    // headless. No-ops unless a save has been requested.
                    campaign.SaveHandler.SaveTick();

                    if (AutoSaveMinutes > 0 && autoSaveTimer.Elapsed >= TimeSpan.FromMinutes(AutoSaveMinutes))
                    {
                        autoSaveTimer.Restart();
                        Console.WriteLine($"[ServerHeadless] Auto-saving ({CampaignTime.Now})...");
                        // Enqueue the game's rotating autosave directly (SetSaveArgs is all
                        // ForceAutoSave does, minus its IsAutoSaveDisabled check that reads the
                        // UI-installed SandBoxSaveManager — null headless). The next SaveTick
                        // writes the file.
                        campaign.SaveHandler.SetSaveArgs(SaveHandler.SaveArgs.SaveMode.AutoSave, null);
                    }
                }
                catch (Exception ex)
                {
                    errors++;
                    string firstFrame = ex.StackTrace?.Split('\n')[0]?.Trim();
                    string sig = ex.GetType().Name + ": " + firstFrame;
                    if (sig != lastError)
                    {
                        lastError = sig;
                        Console.WriteLine($"[ServerHeadless] tick error #{errors}: {ex.GetType().Name} {firstFrame}");
                    }
                }
                tick++;

                if (tick % (TicksPerSecond * 5) == 0)
                {
                    double tps = (tick - lastReportTick) / Math.Max(tpsTimer.Elapsed.TotalSeconds, 0.001);
                    lastReportTick = tick;
                    tpsTimer.Restart();
                    double hourOfDay = CampaignTime.Now.ToHours % 24.0;
                    Console.WriteLine($"[ServerHeadless] tick {tick} — {CampaignTime.Now} {hourOfDay:0.0}h — {tps:0} TPS — {errors} error(s) — weather: {SampleWeather()}");
                }

                TimeSpan remaining = targetFrame - frameTimer.Elapsed;
                if (remaining > TimeSpan.Zero)
                {
                    Shutdown.Token.WaitHandle.WaitOne(remaining);
                }

                // Next tick advances game time by however long this iteration really took, work
                // plus sleep. Clamp hitches (autosave writes, debugger pauses) so the simulation
                // steps, never leaps.
                dt = Math.Min((float)frameTimer.Elapsed.TotalSeconds, 0.1f);
                frameTimer.Restart();
            }

            Console.WriteLine($"[ServerHeadless] Stopped after {tick} ticks.");
        }

        /// <summary>
        /// Tally the current weather event over all settlement positions, so the tick log shows the
        /// server's weather state and confirms it evolves over campaign time.
        /// </summary>
        private static string SampleWeather()
        {
            try
            {
                var model = Campaign.Current?.Models?.MapWeatherModel;
                var settlements = Campaign.Current?.Settlements;
                if (model == null || settlements == null) return "n/a";

                var counts = new System.Collections.Generic.Dictionary<TaleWorlds.CampaignSystem.ComponentInterfaces.MapWeatherModel.WeatherEvent, int>();
                foreach (var settlement in settlements)
                {
                    var ev = model.GetWeatherEventInPosition(settlement.GetPosition2D);
                    counts.TryGetValue(ev, out int c);
                    counts[ev] = c + 1;
                }

                return string.Join(", ", counts.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}={kv.Value}"));
            }
            catch (Exception ex)
            {
                return ex.GetType().Name;
            }
        }
    }
}
