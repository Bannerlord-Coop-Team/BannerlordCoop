using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Common.PacketHandlers;
using Common.Serialization;
using Coop.Core.Common.Network;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Time;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Registry.Messages;
using GameInterface.Services.Entity;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Coop.Core.Server;

/// <summary>
/// Server used for Coop
/// </summary>
public interface ICoopServer : INetwork, INatPunchListener, IDisposable
{
}

/// <inheritdoc cref="ICoopServer"/>
public class CoopServer : CoopNetworkBase, ICoopServer
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopServer>();

    public const string ServerControllerId = "Server";

    public override int Priority => 0;

    private readonly IMessageBroker messageBroker;
    private readonly IPacketManager packetManager;
    private readonly IConnectionMessageQueue connectionMessageQueue;
    // Lazy breaks the construction cycle: the manager depends on ITimeControlInterface, which depends
    // on INetwork (this server). It is only needed each Update, so deferring construction is fine.
    private readonly Lazy<IOverloadedPeerManager> overloadedPeerManager;

    public CoopServer(
        INetworkConfig configuration,
        IMessageBroker messageBroker,
        IPacketManager packetManager,
        IConnectionMessageQueue connectionMessageQueue,
        IControllerIdProvider controllerIdProvider,
        Lazy<IOverloadedPeerManager> overloadedPeerManager,
        ICommonSerializer serializer) : base(configuration, serializer)
    {
        // Dependancy assignment
        this.messageBroker = messageBroker;
        this.packetManager = packetManager;
        this.connectionMessageQueue = connectionMessageQueue;
        this.overloadedPeerManager = overloadedPeerManager;

        // Netmanager initialization
        netManager.NatPunchEnabled = true;
        netManager.NatPunchModule.Init(this);

        controllerIdProvider.SetControllerId(ServerControllerId);
    }

    public override void OnConnectionRequest(ConnectionRequest request)
    {
        Logger.Information("Client connection accepted for {Endpoint}", request.RemoteEndPoint);
        request.Accept();
    }

    public void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
    {
        throw new NotImplementedException();
    }

    public void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, NatAddressType type, string token)
    {
        // Not used on server
    }

    public override void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
    {
        Logger.Warning("Network error from {EndPoint}: {SocketError}", endPoint, socketError);
    }

    public override void OnNetworkLatencyUpdate(NetPeer peer, int latency)
    {

    }

    public override void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        IPacket packet = (IPacket)serializer.Deserialize(reader.GetRemainingBytes());
        packetManager.HandleReceive(peer, packet);
    }

    public override void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
    {
        Logger.Warning("Received unconnected message from {EndPoint}", remoteEndPoint);
    }

    public override void OnPeerConnected(NetPeer peer)
    {
        PlayerConnected message = new PlayerConnected(peer);
        messageBroker.Publish(this, message);
    }

    public override void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        PlayerDisconnected message = new PlayerDisconnected(peer, disconnectInfo);
        messageBroker.Publish(this, message);
    }

    public override void Update(TimeSpan frameTime)
    {
        overloadedPeerManager.Value.CheckForOverloadedPeers();

        netManager.PollEvents();
        netManager.NatPunchModule.PollEvents();
    }

    public override void Start()
    {
        Logger.Information("Server starting on port {Port}", Configuration.Port);
        netManager.Start(IPAddress.Any, IPAddress.IPv6Any, Configuration.Port);
    }

    public override void SendAll(IPacket packet)
    {
        SendAll(netManager, packet);
    }

    public override void SendAllBut(NetPeer ignoredPeer, IPacket packet)
    {
        SendAllBut(netManager, ignoredPeer, packet);
    }

    // Every per-peer send funnels through here, so a still-loading peer's world deltas are dropped
    // (pre-save) or held (loading) instead of sent live — broadcasts and direct sends alike. The queue
    // replays the held ones on campaign entry. Connection-level traffic that must always reach a
    // mid-join peer (the save, the join handshake) uses SendImmediate to bypass this.
    public override void Send(NetPeer netPeer, IPacket packet)
    {
        if (connectionMessageQueue.TryHandleBroadcast(netPeer, packet)) return;
        base.Send(netPeer, packet);
    }
}
