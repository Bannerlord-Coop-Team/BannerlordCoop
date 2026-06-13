using System;
using System.Reflection;

namespace ServerHeadless
{
    /// <summary>
    /// Drives the Coop mod's server flows by reflection.
    ///
    /// The Coop assemblies (Common / Coop.Core / GameInterface) are netstandard2.0 and are NOT
    /// referenced from this net472 project: a ProjectReference drags their NuGet + TaleWorlds graph
    /// into the exe, which produces binding-redirect/version conflicts (e.g.
    /// <c>System.Threading.Tasks.Extensions</c>) and duplicate assembly identities. Instead they are
    /// loaded at runtime from the deployed module bin (via the AssemblyResolve handler in
    /// <see cref="Program"/>), and the server is started exactly as the in-game mod does:
    ///   <c>new CoopartiveMultiplayerExperience()</c> then publish <c>HostSaveGame</c> on the shared
    ///   <c>MessageBroker</c>, which runs <c>StartAsServer()</c> + the save load.
    /// </summary>
    internal static class CoopServerLauncher
    {
        private static object _gameLoopRunner;     // Common.GameLoopRunner.Instance
        private static MethodInfo _update;         // GameLoopRunner.Update(TimeSpan)
        private static object _coop;               // CoopartiveMultiplayerExperience (kept alive)

        /// <summary>
        /// Marks the current (main) thread as the Coop game-loop thread so the mod's
        /// RunOnMainThread executes work inline rather than queuing for another thread.
        /// </summary>
        public static void Initialize()
        {
            Type gameLoopRunnerType = Load("Common", "Common.GameLoopRunner");
            _gameLoopRunner = gameLoopRunnerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            gameLoopRunnerType.GetMethod("SetGameLoopThread").Invoke(_gameLoopRunner, null);
            _update = gameLoopRunnerType.GetMethod("Update", new[] { typeof(TimeSpan) });
        }

        /// <summary>
        /// Routes the Coop mod's Serilog output to the console. The mod's only on-disk/UI sinks are
        /// the in-game console (an <c>OutputSinkManager</c> callback the graphical mod registers) and
        /// a Seq server — neither exists headless, so Poller-caught exceptions are otherwise invisible.
        /// Registering an <c>OutputSinkManager</c> callback surfaces them on stderr.
        /// </summary>
        public static void AttachConsoleLog()
        {
            Type sinkType = Load("Common", "Common.Logging.OutputSinkManager");
            MethodInfo add = sinkType.GetMethod("AddLogCallback", BindingFlags.Public | BindingFlags.Static);
            Action<string> cb = line => Console.Error.WriteLine("[Coop] " + line);
            add.Invoke(null, new object[] { cb });
        }

        /// <summary>
        /// Starts the Coop server and loads the named save into it (mirrors the in-game host flow).
        /// </summary>
        public static void HostSaveGameAsServer(string saveName)
        {
            Type coopType = Load("Coop.Core", "Coop.Core.CoopartiveMultiplayerExperience");
            _coop = Activator.CreateInstance(coopType);

            object broker = Broker();
            Type hostSaveGameType = Load("GameInterface", "GameInterface.Services.UI.Messages.HostSaveGame");
            object hostSaveGame = Activator.CreateInstance(hostSaveGameType, saveName);
            Publish(broker, _coop, hostSaveGameType, hostSaveGame);
        }

        /// <summary>
        /// Publishes <c>CampaignReady</c>, which the server's InitialServerState waits for to call
        /// <c>network.Start()</c> (bind the listen socket), register all game objects and allow
        /// joining. The mod normally raises this from a Harmony hook on <c>MapScreen.OnInitialize</c>
        /// — a graphical screen that never initializes headless — so without this the server never
        /// binds its port and is unreachable.
        /// </summary>
        public static void SignalCampaignReady()
        {
            object broker = Broker();
            Type readyType = Load("GameInterface", "GameInterface.Services.GameState.Messages.CampaignReady");
            object ready = Activator.CreateInstance(readyType);
            Publish(broker, null, readyType, ready);
        }

        /// <summary>
        /// Packages the current campaign for transfer — the same save the server sends a joining
        /// client — by resolving <c>ISaveInterface</c> from the Coop container and calling
        /// <c>SaveCurrentGame()</c>. The save is a synchronous interface call (returning
        /// <c>SaveResults</c>), not a <c>PackageGameSaveData</c>/<c>GameSaveDataPackaged</c> message
        /// round-trip. Must run on this game-loop thread.
        /// </summary>
        public static bool TrySaveCurrentState(out byte[] data, out string campaignId)
        {
            data = null;
            campaignId = null;

            Type containerProviderType = Load("GameInterface", "GameInterface.ContainerProvider");
            Type saveInterfaceType = Load("GameInterface", "GameInterface.Services.Heroes.Interfaces.ISaveInterface");

            // ContainerProvider.TryResolve<ISaveInterface>(out var saveInterface)
            object[] resolveArgs = { null };
            bool resolved = (bool)containerProviderType
                .GetMethod("TryResolve", BindingFlags.Public | BindingFlags.Static)
                .MakeGenericMethod(saveInterfaceType)
                .Invoke(null, resolveArgs);
            if (!resolved || resolveArgs[0] == null) return false;

            // saveInterface.SaveCurrentGame() — runs the save inline on this game-loop thread.
            object saveResults = saveInterfaceType.GetMethod("SaveCurrentGame").Invoke(resolveArgs[0], null);
            if (saveResults == null) return false;

            Type resultsType = saveResults.GetType();
            data = (byte[])resultsType.GetProperty("Data").GetValue(saveResults);
            campaignId = (string)resultsType.GetProperty("CampaignId").GetValue(saveResults);
            return (bool)resultsType.GetProperty("Success").GetValue(saveResults);
        }

        /// <summary>Runs the Coop main-thread work queue (must be called on the game-loop thread).</summary>
        public static void PumpGameLoop(TimeSpan dt) => _update.Invoke(_gameLoopRunner, new object[] { dt });

        private static object Broker()
        {
            Type messageBrokerType = Load("Common", "Common.Messaging.MessageBroker");
            return messageBrokerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        }

        private static void Publish(object broker, object source, Type messageType, object message)
        {
            broker.GetType().GetMethod("Publish").MakeGenericMethod(messageType)
                .Invoke(broker, new[] { source, message });
        }

        private static Type Load(string assemblyName, string typeName)
        {
            Assembly assembly = Assembly.Load(new AssemblyName(assemblyName));
            return assembly.GetType(typeName, throwOnError: true);
        }
    }
}
