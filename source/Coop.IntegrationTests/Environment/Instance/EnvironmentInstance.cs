using Autofac;
using Common.Messaging;
using Common.PacketHandlers;
using Common.Serialization;
using Common.Tests.Utils;
using Common.Util;
using Coop.IntegrationTests.Environment.Mock;
using GameInterface;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using LiteNetLib;
using System.Reflection;

namespace Coop.IntegrationTests.Environment.Instance;

/// <summary>
/// Single instance of a server or client. Stores relevant test information.
/// </summary>
public abstract class EnvironmentInstance
{
    public NetPeer NetPeer => MockNetwork.NetPeer;
    /// <summary>
    /// Messages sent internally or received over the network via the message broker
    /// </summary>
    public MessageCollection InternalMessages => MessageBroker.Messages;
    /// <summary>
    /// Messages sent over the network
    /// </summary>
    public MessageCollection NetworkSentMessages => MockNetwork.NetworkSentMessages;

    public abstract IContainer Container { get; }

    protected abstract TestMessageBroker MessageBroker { get; }
    protected abstract MockNetworkBase MockNetwork { get; }

    /// <summary>
    /// Simulate receiving a message from the message broker
    /// </summary>
    /// <param name="source">Source of the message</param>
    /// <param name="message">Received Message</param>
    public void SimulateMessage<T>(object source, T message) where T : IMessage
    {
        using (new StaticScope(this))
        {
            MessageBroker.Publish(source, message);
        }
    }

    /// <summary>
    /// Simulate receiving a packet from the network
    /// </summary>
    /// <param name="source">Source Peer</param>
    /// <param name="packet">Received Packet</param>
    public void SimulatePacket(NetPeer source, IPacket packet)
    {
        using(new StaticScope(this))
        {
            EnsureSerializable(packet);
            MockNetwork.ReceiveFromNetwork(source, packet);
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

        using (new PatchScope(disabledMethods))
        {
            using (new StaticScope(this))
            {
                callFunction();
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

    public T GetRegisteredObject<T>(string stringId) where T: class
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
        private readonly bool wasServer;

        public StaticScope(EnvironmentInstance instance)
        {
            // Save previous static values
            if(ContainerProvider.TryGetContainer(out previousContainer) == false)
            {
                // If no previous container is set, set it to the current container
                previousContainer = instance.Container;
            }

            // Set new static values
            ContainerProvider.SetContainer(instance.Container);
            
        }

        public void Dispose()
        {
            // Restore previous static values
            ContainerProvider.SetContainer(previousContainer);
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
            patches = methods.Select(m => new HarmonyMethod(disableMethod)).ToArray();

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
        var serializer = Container.Resolve<ICommonSerializer>();

        byte[] bytes = serializer.Serialize(obj);

        return serializer.Deserialize<T>(bytes);
    }
}