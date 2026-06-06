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
using SandBox;

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
                GameBinDirectory = ResolveGameBinDirectory(args);
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
                    return System.Reflection.Assembly.LoadFrom(candidate);
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

                SaveGameFileInfo selectedSave = PromptForSave();
                if (selectedSave == null)
                {
                    Console.WriteLine("[ServerHeadless] No save selected. Exiting.");
                    return 0;
                }

                if (!LoadSelectedSave(selectedSave))
                {
                    return 1;
                }

                TestServerSave();
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
        /// Lists the local save games and lets the operator pick one. Returns null if there are
        /// none or the operator cancels.
        /// </summary>
        private static SaveGameFileInfo PromptForSave()
        {
            // MBSaveLoad's save driver is installed by Module.Initialize, so this enumerates the
            // player's on-disk saves (newest first).
            SaveGameFileInfo[] saves = MBSaveLoad.GetSaveFiles();

            if (saves == null || saves.Length == 0)
            {
                Console.WriteLine("[ServerHeadless] No save games found.");
                return null;
            }

            while (!Shutdown.IsCancellationRequested)
            {
                Console.WriteLine();
                Console.WriteLine("Available save games:");
                for (int i = 0; i < saves.Length; i++)
                {
                    Console.WriteLine($"  [{i + 1}] {saves[i].Name}");
                }
                Console.WriteLine("  [q] Quit");
                Console.Write("Select a save to host: ");

                string input = Console.ReadLine();
                if (input == null || input.Trim().Equals("q", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                if (int.TryParse(input.Trim(), out int choice) && choice >= 1 && choice <= saves.Length)
                {
                    return saves[choice - 1];
                }

                Console.WriteLine("Invalid selection, try again.");
            }

            return null;
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
        /// null headless). Runs at a fixed timestep until CTRL+C.
        /// </summary>
        private static void TickCampaign()
        {
            Campaign campaign = Campaign.Current;

            Console.WriteLine($"[ServerHeadless] Ticking campaign at {TicksPerSecond} TPS. Press CTRL+C to stop.");
            Console.WriteLine($"    Start time: {CampaignTime.Now}");

            const float dt = 1f / TicksPerSecond;
            long tick = 0;
            long errors = 0;
            string lastError = null;
            while (!Shutdown.IsCancellationRequested)
            {
                // Resilient tick: the systematic headless gaps are patched out, but an arbitrary save
                // can still surface a rare edge case (e.g. raid loot) deep in a subsystem. A server
                // shouldn't die on a single bad tick — log the first occurrence of each distinct
                // failure and keep simulating.
                try
                {
                    // Re-assert play mode every tick: encounters/menus reset TimeControlMode to Stop,
                    // which would freeze the campaign clock. A headless server must keep advancing.
                    campaign.SetTimeControlModeLock(false);
                    campaign.TimeControlMode = CampaignTimeControlMode.UnstoppablePlay;

                    campaign.RealTick(dt);
                    campaign.Tick();

                    // Pump the Coop server's main-thread work queue (network handlers etc.).
                    CoopServerLauncher.PumpGameLoop(TimeSpan.FromSeconds(dt));
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
                    Console.WriteLine($"[ServerHeadless] tick {tick} — {CampaignTime.Now} — {errors} error(s)");
                }

                Shutdown.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(dt));
            }

            Console.WriteLine($"[ServerHeadless] Stopped after {tick} ticks.");
        }
    }
}
