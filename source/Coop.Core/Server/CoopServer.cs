using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Common.Serialization;
using Coop.Core.Common.Network;
using Coop.Core.Server.Connections.Messages;
using GameInterface;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Coop.Core.Server;

/// <summary>
/// Server used for Coop
/// </summary>
public interface ICoopServer : INetwork, INatPunchListener, INetEventListener, IDisposable
{
    IEnumerable<NetPeer> ConnectedPeers { get; }
}

/// <inheritdoc cref="ICoopServer"/>
public class CoopServer : CoopNetworkBase, ICoopServer
{
    public const string ServerControllerId = "Server";

    public override int Priority => 0;

    public IEnumerable<NetPeer> ConnectedPeers => netManager.ConnectedPeerList;

    private readonly IMessageBroker messageBroker;
    private readonly IPacketManager packetManager;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly NetManager netManager;

    private bool allowJoining = false;

    public CoopServer(
        INetworkConfiguration configuration, 
        IMessageBroker messageBroker,
        IPacketManager packetManager,
        IControllerIdProvider controllerIdProvider) : base(configuration)
    {
        // Dependancy assignment
        this.messageBroker = messageBroker;
        this.packetManager = packetManager;
        this.controllerIdProvider = controllerIdProvider;
        messageBroker.Subscribe<GameTimeControlsEnabled>(Handle_GameTimeControlsEnabled);

        ModInformation.IsServer = true;

        // TODO add configuration
        netManager = new NetManager(this);

#if DEBUG
        // Increase disconnect timeout to prevent disconnect during debugging
        netManager.DisconnectTimeout = 300 * 1000;
#endif

        // Netmanager initialization
        netManager.NatPunchEnabled = true;
        netManager.NatPunchModule.Init(this);

        controllerIdProvider.SetControllerId(ServerControllerId);
    }

    public void Dispose()
    {
        netManager.DisconnectAll();
        netManager.Stop();
    }

    public void OnConnectionRequest(ConnectionRequest request)
    {
        if (allowJoining)
        {
            request.Accept();
        }
        else
        {
            request.Reject();
        }
    }

    public void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
    {
        throw new NotImplementedException();
    }

    public void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, NatAddressType type, string token)
    {
        // Not used on server
    }

    public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
    {
        throw new NotImplementedException();
    }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
    {

    }

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        IPacket packet = (IPacket)ProtoBufSerializer.Deserialize(reader.GetRemainingBytes());
        packetManager.HandleRecieve(peer, packet);
    }

    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
    {
        throw new NotImplementedException();
    }

    public void OnPeerConnected(NetPeer peer)
    {
        PlayerConnected message = new PlayerConnected(peer);
        messageBroker.Publish(this, message);
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        PlayerDisconnected message = new PlayerDisconnected(peer, disconnectInfo);
        messageBroker.Publish(this, message);
    }

    public override void Update(TimeSpan frameTime)
    {
        netManager.PollEvents();
        netManager.NatPunchModule.PollEvents();
    }

    public override void Start()
    {
        netManager.Start(Configuration.Port);
    }

    public override void Stop()
    {
        netManager.Stop();
    }

    public override void SendAll(IPacket packet)
    {
        SendAll(netManager, packet);
    }

    public override void SendAllBut(NetPeer netPeer, IPacket packet)
    {
        SendAllBut(netManager, netPeer, packet);
    }

    private void Handle_GameTimeControlsEnabled(MessagePayload<GameTimeControlsEnabled> obj)
    {
        allowJoining = true;
    }
}
