using Autofac;
using Common;
using Common.Messaging;
using Common.PacketHandlers;
using Common.Serialization;
using Common.Tests.Utils;
using Common.Util;
using E2E.Tests.Environment.Mock;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters;
using HarmonyLib;
using LiteNetLib;
using ProtoBuf.Meta;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace E2E.Tests.Environment.Instance;

/// <summary>
/// Single instance of a server or client. Stores relevant test information.
/// </summary>
public abstract class EnvironmentInstance : IDisposable
{
    public NetPeer NetPeer => mockNetwork.NetPeer;
    /// <summary>
    /// Messages sent internally or received over the network via the message broker
    /// </summary>
    public MessageCollection InternalMessages => messageBroker.Messages;
    /// <summary>
    /// Messages sent over the network
    /// </summary>
    public MessageCollection NetworkSentMessages => mockNetwork.NetworkSentMessages;
    public MessageCollection NetworkSentImmediateMessages => mockNetwork.NetworkSentImmediateMessages;

    public ILifetimeScope Container => container;
    public IObjectManager ObjectManager => Container.Resolve<IObjectManager>();

    public GameInstance GameInstance = new GameInstance();

    private readonly TestMessageBroker messageBroker;
    private readonly MockNetworkBase mockNetwork;
    private readonly ILifetimeScope container;

    public EnvironmentInstance(
        TestMessageBroker messageBroker,
        MockNetworkBase mockNetwork,
        ILifetimeScope container)
    {
        this.messageBroker = messageBroker;
        this.mockNetwork = mockNetwork;
        this.container = container;
    }

    /// <summary>
    /// Simulate receiving a message from the message broker
    /// </summary>
    /// <param name="source">Source of the message</param>
    /// <param name="message">Received Message</param>
    /// <param name="markGameThread">Whether the current test thread should apply game-thread work inline.</param>
    public void SimulateMessage<T>(object source, T message, bool markGameThread = true) where T : IMessage
    {
        using (new StaticScope(this, markGameThread))
        {
            messageBroker.Publish(source, message);
        }
    }

    /// <summary>
    /// Simulate receiving a packet from the network
    /// </summary>
    /// <param name="source">Source Peer</param>
    /// <param name="packet">Received Packet</param>
    /// <param name="markGameThread">Whether the current test thread should apply game-thread work inline.</param>
    public void SimulatePacket(NetPeer source, IPacket packet, bool markGameThread = true)
    {
        using (new StaticScope(this, markGameThread))
        {
            EnsureSerializable(packet);
            mockNetwork.ReceiveFromNetwork(source, packet);
        }
    }

    /// <summary>
    /// Calls a given action with correctly setup static variables used by the patches
    /// </summary>
    /// <param name="callFunction">Function to call</param>
    public void Call(Action callFunction, IEnumerable<MethodBase>? disabledMethods = null)
    {
        if (disabledMethods == null)
        {
            disabledMethods = Array.Empty<MethodBase>();
        }

        // The same lock StaticScope takes, so PatchScope's patch/unpatch cannot interleave with
        // another thread executing patched game code inside a SimulateMessage/SimulatePacket —
        // those only enter GameInstance.@lock (via StaticScope), and the previous separate _lock
        // left the Harmony rewrites unguarded against them. Monitor is reentrant per thread, which
        // routed sends rely on: a Call's handler chain synchronously delivers into another
        // instance's Simulate*, nesting scopes on the same thread.
        lock (GameInstance.@lock)
        {
            using (new PatchScope(disabledMethods))
            {
                using (new StaticScope(this))
                {
                    callFunction();
                }
            }
        }
    }

    /// <summary>
    /// Resolves an object created by this instance
    /// </summary>
    /// <typeparam name="T">Type to resolve</typeparam>
    /// <returns>Object of type <typeparamref name="T"/></returns>
    public T Resolve<T>() where T : notnull
    {
        return Container.Resolve<T>();
    }

    /// <summary>
    /// Creates an uninitialized object that is registered with the object manager
    /// </summary>
    /// <typeparam name="T">Type to create</typeparam>
    /// <param name="stringId">String id to assign new object which is referencable by this instances object manager</param>
    /// <returns>New uninitialized object of type <typeparamref name="T"/></returns>
    public T CreateRegisteredObject<T>(string stringId) where T : class
    {
        var obj = ObjectHelper.SkipConstructor<T>();

        var objectManager = Resolve<IObjectManager>();
        objectManager.AddExisting(stringId, obj);

        return obj;
    }

    public T GetRegisteredObject<T>(string stringId) where T : class
    {
        var objectManager = Resolve<IObjectManager>();
        if (objectManager.TryGetObject<T>(stringId, out var obj) == false)
        {
            throw new Exception($"Unable to resolve {stringId} for type {typeof(T)}");
        }

        return obj;
    }

    private class StaticScope : IDisposable
    {
        private readonly ILifetimeScope previousContainer;
        private readonly MBObjectManager previousObjectManager;
        private readonly Campaign previousCampaign;
        private readonly Game previousGame;
        private readonly TaleWorlds.MountAndBlade.Module previousModule;
        private readonly TestMessageBroker previousMessageBroker;
        private readonly bool wasServer;
        private readonly bool markedGameThread;
        private readonly int previousGameThreadId;

        public StaticScope(EnvironmentInstance instance, bool markGameThread = true)
        {
            Monitor.Enter(GameInstance.@lock);
            bool restorePreviousStatics = false;

            // The lock must be released even when the body throws (resolving from an instance a
            // concurrent test already disposed), otherwise it stays owned by this (possibly
            // recycled) thread forever and every later scope or GameInstance build deadlocks.
            try
            {
                if (markGameThread)
                {
                    // xUnit can move a test from its fixture-constructor thread before the next scoped call.
                    // Save-and-restore rather than bare-mark: a scope entered on a worker thread (e.g.
                    // Task.Run(() => Server.Call(...))) must not leave the game-thread mark on that thread —
                    // every later GameThread.RunSafe from the real test thread would silently enqueue onto
                    // a queue nobody pumps instead of running inline.
                    markedGameThread = true;
                    previousGameThreadId = GameThread.Instance.GameThreadId;
                    GameThread.Instance.MarkGameThread();
                }

                // Save previous static values
                wasServer = ModInformation.IsServer;
                previousObjectManager = MBObjectManager.Instance;
                previousCampaign = Campaign.Current;
                previousGame = Game.Current;
                previousModule = TaleWorlds.MountAndBlade.Module.CurrentModule;
                if (GameInterface.ContainerProvider.TryGetContainer(out previousContainer) == false)
                {
                    // If no previous container is set, set it to the current container
                    previousContainer = instance.Container;
                }
                previousMessageBroker = previousContainer.Resolve<TestMessageBroker>();
                var instanceMessageBroker = instance.Container.Resolve<TestMessageBroker>();

                // Set new static values
                restorePreviousStatics = true;
                instance.GameInstance.SetStatics();

                ModInformation.IsServer = instance is ServerInstance;
                instanceMessageBroker.SetStaticInstance();
                GameInterface.ContainerProvider.SetContainer(instance.Container);
            }
            catch
            {
                try
                {
                    if (restorePreviousStatics)
                    {
                        RestorePreviousStatics();
                    }
                    else if (markedGameThread)
                    {
                        // The mark is set before the statics are saved, so a throw in between must
                        // still put it back (RestorePreviousStatics is not reachable yet here).
                        GameThread.Instance.RestoreGameThread(previousGameThreadId);
                    }
                }
                finally
                {
                    Monitor.Exit(GameInstance.@lock);
                }
                throw;
            }
        }

        public void Dispose()
        {
            try
            {
                RestorePreviousStatics();
            }
            finally
            {
                Monitor.Exit(GameInstance.@lock);
            }
        }

        private void RestorePreviousStatics()
        {
            MBObjectManager.Instance = previousObjectManager;
            Campaign.Current = previousCampaign;
            Game.Current = previousGame;
            TaleWorlds.MountAndBlade.Module.CurrentModule = previousModule;
            ModInformation.IsServer = wasServer;
            GameInterface.ContainerProvider.SetContainer(previousContainer);
            previousMessageBroker.SetStaticInstance();
            if (markedGameThread)
            {
                GameThread.Instance.RestoreGameThread(previousGameThreadId);
            }
        }
    }

    private class PatchScope : IDisposable
    {
        private readonly Harmony harmony = new Harmony("patch scope harmony");

        private readonly HarmonyMethod[] patches;
        private readonly MethodBase[] methods;

        public PatchScope(IEnumerable<MethodBase> disableMethods)
        {
            var disableMethod = AccessTools.Method(typeof(PatchScope), nameof(Disable));
            methods = disableMethods.ToArray();
            // Priority.Last, not First: an explicit priority keeps the disable's position deterministic
            // across container rebuilds (same-priority prefixes run in patch-insertion order, which varies),
            // but it must sort AFTER the mod's own prefixes — a bool prefix returning false skips every
            // later bool prefix, and tests drive real patched natives expecting their routing prefixes
            // (e.g. SiegeEntryFlowPatches' publish-and-pre-null shapes) to still fire while only the
            // native body is suppressed.
            patches = methods
                .Select(_ => new HarmonyMethod(disableMethod) { priority = Priority.Last })
                .ToArray();

            for (int i = 0; i < methods.Length; i++)
            {
                harmony.Patch(methods[i], prefix: patches[i]);
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < methods.Length; i++)
            {
                harmony.Unpatch(methods[i], HarmonyPatchType.Prefix, harmony.Id);
            }
        }

        static bool Disable() => false;
    }

    public T EnsureSerializable<T>(T obj)
    {
        if (RuntimeTypeModel.Default.CanSerialize(obj?.GetType()) == false)
        {
            Assert.Fail($"ProtoBuf is unable to serialize type {obj?.GetType().Name}");
        }

        var serializer = Container.Resolve<ICommonSerializer>();

        byte[] bytes = serializer.Serialize(obj);

        return serializer.Deserialize<T>(bytes);
    }

    public abstract void Dispose();
}
