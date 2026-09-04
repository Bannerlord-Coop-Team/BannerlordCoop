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
    public ICampaignMission CampaignMissionContext { get; set; }

    private readonly TestMessageBroker messageBroker;
    private readonly MockNetworkBase mockNetwork;
    private readonly ILifetimeScope container;
    private readonly GameThread.QueueContext gameThreadQueue = new();
    private int disposeStarted;

    public int PendingGameThreadActionCount => gameThreadQueue.Count;
    public int RejectedGameThreadActionCount => gameThreadQueue.RejectedAfterCloseCount;

    public EnvironmentInstance(
        TestMessageBroker messageBroker,
        MockNetworkBase mockNetwork,
        ILifetimeScope container)
    {
        this.messageBroker = messageBroker;
        this.mockNetwork = mockNetwork;
        this.container = container;
        gameThreadQueue.WaitPump = mockNetwork.FlushNetworkTick;
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
            mockNetwork.FlushNetworkTick();
        }
    }

    /// <summary>Simulates the real receive path by deserializing the exact transmitted bytes.</summary>
    public void SimulateMessage(object source, byte[] serializedMessage, bool markGameThread = true)
    {
        ArgumentNullException.ThrowIfNull(serializedMessage);

        using (new StaticScope(this, markGameThread))
        {
            var serializer = Container.Resolve<ICommonSerializer>();
            IMessage message = serializer.Deserialize<IMessage>(serializedMessage);
            messageBroker.Publish(source, message);
            mockNetwork.FlushNetworkTick();
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
            mockNetwork.FlushNetworkTick();
        }
    }

    /// <summary>Simulates the real mesh receive path by deserializing the exact transmitted bytes.</summary>
    public void SimulatePacket(NetPeer source, byte[] serializedPacket, bool markGameThread = true)
    {
        ArgumentNullException.ThrowIfNull(serializedPacket);

        using (new StaticScope(this, markGameThread))
        {
            var serializer = Container.Resolve<ICommonSerializer>();
            IPacket packet = serializer.Deserialize<IPacket>(serializedPacket);
            mockNetwork.ReceiveFromNetwork(source, packet);
            mockNetwork.FlushNetworkTick();
        }
    }

    /// <summary>Runs the production receive discriminator over one exact network payload.</summary>
    public void SimulateNetworkPayload(
        NetPeer source,
        byte[] payload,
        bool markGameThread = true,
        bool flushNetworkTick = true)
    {
        ArgumentNullException.ThrowIfNull(payload);

        using (new StaticScope(this, markGameThread))
        {
            var serializer = Container.Resolve<ICommonSerializer>();
            object received = serializer.Deserialize(payload);
            if (received is IPacket packet)
            {
                mockNetwork.ReceiveFromNetwork(source, packet);
                if (flushNetworkTick)
                    mockNetwork.FlushNetworkTick();
                return;
            }

            if (received is IMessage message)
            {
                Container.Resolve<IMessagePacketHandler>().PublishEvent(source, message);
                if (flushNetworkTick)
                    mockNetwork.FlushNetworkTick();
                return;
            }

            throw new InvalidOperationException(
                $"Network payload deserialized to unsupported type {received?.GetType().FullName ?? "(null)"}.");
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
                    mockNetwork.FlushNetworkTick();
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

    /// <summary>
    /// Drains game-thread work for this recipient with its process statics installed.
    /// </summary>
    public int PumpGameThread(int maximumPasses = 100)
    {
        if (maximumPasses <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumPasses));

        int passes = 0;
        using (new StaticScope(this))
        using (AllowedThread.Suspend())
        {
            while (true)
            {
                while (PendingGameThreadActionCount > 0)
                {
                    if (passes >= maximumPasses)
                    {
                        throw new InvalidOperationException(
                            $"Game-thread work did not drain within {maximumPasses} pass(es).");
                    }

                    GameThread.Instance.Update(TimeSpan.Zero);
                    passes++;
                }

                mockNetwork.FlushNetworkTick();
                if (PendingGameThreadActionCount == 0) break;

                if (passes >= maximumPasses)
                {
                    throw new InvalidOperationException(
                        $"Game-thread work did not drain within {maximumPasses} pass(es).");
                }
            }
        }

        return passes;
    }

    /// <summary>
    /// Releases work owned by this simulated process and reports any apply that the test never pumped.
    /// </summary>
    internal void ReleasePendingGameThreadActions()
    {
        int discarded = GameThread.Instance.CloseAndDiscardQueuedActions(gameThreadQueue);
        if (discarded == 0) return;

        throw new InvalidOperationException(
            $"{GetType().Name} {NetPeer.Id} discarded {discarded} unpumped game-thread action(s) during teardown.");
    }

    protected void DisposeContainer()
    {
        if (Interlocked.Exchange(ref disposeStarted, 1) != 0) return;

        CloseGameThreadQueueAndDispose(container.Dispose);
    }

    internal void CloseGameThreadQueueAndDispose(Action disposeResources)
    {
        if (disposeResources == null) throw new ArgumentNullException(nameof(disposeResources));

        // PumpGameThread holds this lock across dequeue and execution. Waiting here prevents an
        // already-dequeued apply from resuming after its instance dependencies have been disposed.
        lock (GameInstance.@lock)
        {
            CloseGameThreadQueueAndDisposeLocked(disposeResources);
        }
    }

    private void CloseGameThreadQueueAndDisposeLocked(Action disposeResources)
    {
        Exception? queueFailure = null;
        try
        {
            // Release blocked callers and reject silently lost applies before their dependencies disappear.
            ReleasePendingGameThreadActions();
        }
        catch (Exception e)
        {
            queueFailure = e;
        }

        Exception? containerFailure = null;
        try
        {
            disposeResources();
        }
        catch (Exception e)
        {
            containerFailure = e;
        }

        if (queueFailure != null && containerFailure != null)
            throw new AggregateException(queueFailure, containerFailure);
        if (queueFailure != null)
            throw queueFailure;
        if (containerFailure != null)
            throw containerFailure;
    }

    private class StaticScope : IDisposable
    {
        private readonly ILifetimeScope previousContainer;
        private readonly MBObjectManager previousObjectManager;
        private readonly Campaign previousCampaign;
        private readonly ICampaignMission previousCampaignMission;
        private readonly Game previousGame;
        private readonly TaleWorlds.MountAndBlade.Module previousModule;
        private readonly TestMessageBroker previousMessageBroker;
        private readonly bool wasServer;
        private readonly bool changedGameThreadRegistration;
        private readonly int previousGameThreadId;
        private readonly IDisposable gameThreadQueueScope;

        public StaticScope(EnvironmentInstance instance, bool markGameThread = true)
        {
            Monitor.Enter(GameInstance.@lock);
            bool restorePreviousStatics = false;

            // The lock must be released even when the body throws (resolving from an instance a
            // concurrent test already disposed), otherwise it stays owned by this (possibly
            // recycled) thread forever and every later scope or GameInstance build deadlocks.
            try
            {
                gameThreadQueueScope = GameThread.ActivateQueue(instance.gameThreadQueue);

                // A nested poller receive can run on the fixture's already-marked test thread. Clear the
                // registration explicitly so GameThread.Run queues exactly as it does on LiteNetLib's poller.
                changedGameThreadRegistration = true;
                previousGameThreadId = GameThread.Instance.GameThreadId;
                if (markGameThread)
                {
                    // xUnit can move a test from its fixture-constructor thread before the next scoped call.
                    // Save-and-restore rather than bare-mark: a scope entered on a worker thread (e.g.
                    // Task.Run(() => Server.Call(...))) must not leave the game-thread mark on that thread —
                    // every later GameThread.RunSafe from the real test thread would silently enqueue onto
                    // a queue nobody pumps instead of running inline.
                    GameThread.Instance.MarkGameThread();
                }
                else
                {
                    GameThread.Instance.UnmarkGameThread();
                }

                // Save previous static values
                wasServer = ModInformation.IsServer;
                previousObjectManager = MBObjectManager.Instance;
                previousCampaign = Campaign.Current;
                previousCampaignMission = CampaignMission.Current;
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
                CampaignMission.Current = instance.CampaignMissionContext;

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
                    else if (changedGameThreadRegistration)
                    {
                        // The registration is changed before the statics are saved, so a throw in between must
                        // still put it back (RestorePreviousStatics is not reachable yet here).
                        GameThread.Instance.RestoreGameThread(previousGameThreadId);
                    }
                }
                finally
                {
                    gameThreadQueueScope?.Dispose();
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
                try
                {
                    gameThreadQueueScope.Dispose();
                }
                finally
                {
                    Monitor.Exit(GameInstance.@lock);
                }
            }
        }

        private void RestorePreviousStatics()
        {
            MBObjectManager.Instance = previousObjectManager;
            Campaign.Current = previousCampaign;
            CampaignMission.Current = previousCampaignMission;
            Game.Current = previousGame;
            TaleWorlds.MountAndBlade.Module.CurrentModule = previousModule;
            ModInformation.IsServer = wasServer;
            GameInterface.ContainerProvider.SetContainer(previousContainer);
            previousMessageBroker.SetStaticInstance();
            if (changedGameThreadRegistration)
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

    public byte[] SerializeForWire<T>(T obj)
    {
        if (RuntimeTypeModel.Default.CanSerialize(obj?.GetType()) == false)
        {
            Assert.Fail($"ProtoBuf is unable to serialize type {obj?.GetType().Name}");
        }

        var serializer = Container.Resolve<ICommonSerializer>();

        return serializer.Serialize(obj);
    }

    public T DeserializeFromWire<T>(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        var serializer = Container.Resolve<ICommonSerializer>();

        return serializer.Deserialize<T>(bytes);
    }

    public T EnsureSerializable<T>(T obj)
    {
        byte[] bytes = SerializeForWire(obj);

        return DeserializeFromWire<T>(bytes);
    }

    public abstract void Dispose();
}
