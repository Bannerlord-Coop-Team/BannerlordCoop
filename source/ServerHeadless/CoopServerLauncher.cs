using System;
using System.Reflection;

namespace ServerHeadless
{
    /// <summary>
    /// Drives the Coop mod's "load as server" flow by reflection.
    ///
    /// The Coop assemblies (Common / Coop.Core / GameInterface) are netstandard2.0 and can't be
    /// referenced from this net472 project without breaking Krafs.Publicizer and colliding the
    /// <c>Common</c> namespace with <c>TaleWorlds.Library.Common</c>. They are loaded at runtime from
    /// the deployed module bin (via the AssemblyResolve handler in <see cref="Program"/>), and the
    /// server is started exactly as the in-game mod does:
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
        /// Starts the Coop server and loads the named save into it (mirrors the in-game host flow).
        /// </summary>
        public static void HostSaveGameAsServer(string saveName)
        {
            Type coopType = Load("Coop.Core", "Coop.Core.CoopartiveMultiplayerExperience");
            _coop = Activator.CreateInstance(coopType);

            Type hostSaveGameType = Load("GameInterface", "GameInterface.Services.UI.Messages.HostSaveGame");
            object hostSaveGame = Activator.CreateInstance(hostSaveGameType, saveName);

            Type messageBrokerType = Load("Common", "Common.Messaging.MessageBroker");
            object broker = messageBrokerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            MethodInfo publish = messageBrokerType.GetMethod("Publish").MakeGenericMethod(hostSaveGameType);
            publish.Invoke(broker, new[] { _coop, hostSaveGame });
        }

        /// <summary>Runs the Coop main-thread work queue (must be called on the game-loop thread).</summary>
        public static void PumpGameLoop(TimeSpan dt) => _update.Invoke(_gameLoopRunner, new object[] { dt });

        private static Type Load(string assemblyName, string typeName)
        {
            Assembly assembly = Assembly.Load(new AssemblyName(assemblyName));
            return assembly.GetType(typeName, throwOnError: true);
        }
    }
}
