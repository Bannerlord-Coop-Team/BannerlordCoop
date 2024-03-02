using Autofac;
using Common.Messaging;
using Common.PacketHandlers;
using Common.Serialization;
using Common.Tests.Utils;
using Common.Util;
using Coop.Core;
using Coop.IntegrationTests.Environment.Mock;
using GameInterface;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using System.Net.Sockets;
using TaleWorlds.CampaignSystem;

namespace Coop.IntegrationTests.Environment.Instance;

/// <summary>
/// Single instance of a server or client. Stores relevant test information.
/// </summary>
public abstract class EnvironmentInstance
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

    public IContainer Container => containerProvider.GetContainer();
    public IObjectManager ObjectManager => Container.Resolve<IObjectManager>();

    private readonly TestMessageBroker messageBroker;
    private readonly MockNetworkBase mockNetwork;
    private readonly IContainerProvider containerProvider;

    public EnvironmentInstance(
        TestMessageBroker messageBroker,
        MockNetworkBase mockNetwork,
        IContainerProvider containerProvider)
    {
        this.messageBroker = messageBroker;
        this.mockNetwork = mockNetwork;
        this.containerProvider = containerProvider;
    }

    /// <summary>
    /// Simulate receiving a message from the message broker
    /// </summary>
    /// <param name="source">Source of the message</param>
    /// <param name="message">Received Message</param>
    public void SimulateMessage<T>(object source, T message) where T : IMessage
    {
        using (new StaticScope(this))
        {
            EnsureSerializable(message);
            messageBroker.Publish(source, message);
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
            mockNetwork.ReceiveFromNetwork(source, packet);
        }
    }

    /// <summary>
    /// Calls a given action with correctly setup static variables used by the patches
    /// </summary>
    /// <param name="callFunction">Function to call</param>
    public void Call(Action callFunction)
    {
        using (new StaticScope(this))
        {
            callFunction();
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

    private class StaticScope : IDisposable
    {
        private readonly ILifetimeScope previousContainer;
        private readonly bool wasServer;

        public StaticScope(EnvironmentInstance instance)
        {
            // Save previous static values
            wasServer = ModInformation.IsServer;
            if(GameInterface.ContainerProvider.TryGetContainer(out previousContainer) == false)
            {
                // If no previous container is set, set it to the current container
                previousContainer = instance.Container;
            }

            // Set new static values
            ModInformation.IsServer = instance is ServerInstance;
            instance.Container.Resolve<TestMessageBroker>().SetStaticInstance();
            GameInterface.ContainerProvider.SetContainer(instance.Container);
            
        }

        public void Dispose()
        {
            // Restore previous static values
            ModInformation.IsServer = wasServer;
            GameInterface.ContainerProvider.SetContainer(previousContainer);
            previousContainer.Resolve<TestMessageBroker>().SetStaticInstance();
        }
    }

    public T EnsureSerializable<T>(T obj)
    {
        var serializer = Container.Resolve<ICommonSerializer>();

        byte[] bytes = serializer.Serialize(obj);

        return serializer.Deserialize<T>(bytes);
    }
}