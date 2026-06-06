using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

namespace ServerHeadless
{
    /// <summary>
    /// Drives the Coop mod's server flows by reflection.
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

        // Captured save result (set asynchronously by MessageBroker.Respond).
        private static volatile int _savedBytes = -1;
        private static volatile string _savedCampaignId;
        private static readonly SaveResultSink _sink = new SaveResultSink();

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

            object broker = Broker();
            Type hostSaveGameType = Load("GameInterface", "GameInterface.Services.UI.Messages.HostSaveGame");
            object hostSaveGame = Activator.CreateInstance(hostSaveGameType, saveName);
            Publish(broker, _coop, hostSaveGameType, hostSaveGame);
        }

        /// <summary>
        /// Triggers the server's "package the campaign for transfer" save (the same
        /// <c>PackageGameSaveData</c> request the server raises when a client connects) and waits for
        /// the resulting in-memory save bytes.
        /// </summary>
        public static bool TrySaveCurrentState(out int bytes, out string campaignId, int timeoutMs = 10000)
        {
            _savedBytes = -1;
            _savedCampaignId = null;

            object broker = Broker();
            Type brokerType = broker.GetType();

            // Subscribe to GameSaveDataPackaged. MessageBroker.Respond delivers to the subscriber
            // whose delegate target == the publish source, so _sink is both subscriber and source.
            Type resultType = Load("GameInterface", "GameInterface.Services.Heroes.Messages.GameSaveDataPackaged");
            Type payloadType = Load("Common", "Common.Messaging.MessagePayload`1").MakeGenericType(resultType);
            Type actionType = typeof(Action<>).MakeGenericType(payloadType);
            MethodInfo onPackaged = typeof(SaveResultSink).GetMethod(nameof(SaveResultSink.OnPackaged));
            Delegate handler = Delegate.CreateDelegate(actionType, _sink, onPackaged);
            brokerType.GetMethod("Subscribe").MakeGenericMethod(resultType).Invoke(broker, new object[] { handler });

            // Raise the package request (runs SaveCurrentGame inline on this game-loop thread).
            Type requestType = Load("GameInterface", "GameInterface.Services.Heroes.Messages.PackageGameSaveData");
            object request = Activator.CreateInstance(requestType);
            Publish(broker, _sink, requestType, request);

            // Respond dispatches the result asynchronously — pump + wait for it.
            Stopwatch sw = Stopwatch.StartNew();
            while (_savedBytes < 0 && sw.ElapsedMilliseconds < timeoutMs)
            {
                PumpGameLoop(TimeSpan.FromMilliseconds(16));
                Thread.Sleep(10);
            }

            bytes = _savedBytes;
            campaignId = _savedCampaignId;
            return _savedBytes > 0;
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

        /// <summary>
        /// Receives the GameSaveDataPackaged response. Must be an instance method (the broker holds a
        /// weak reference to the delegate target and matches it against the request source).
        /// </summary>
        internal sealed class SaveResultSink
        {
            public void OnPackaged(object payload)
            {
                object what = payload.GetType().GetProperty("What").GetValue(payload);
                byte[] data = (byte[])what.GetType().GetProperty("GameSaveData").GetValue(what);
                _savedCampaignId = (string)what.GetType().GetProperty("CampaignID").GetValue(what);
                _savedBytes = data?.Length ?? 0;
            }
        }
    }
}
