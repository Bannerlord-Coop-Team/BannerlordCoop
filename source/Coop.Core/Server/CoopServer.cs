using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Common.Network.Messages;
using Common.PacketHandlers;
using Common.Serialization;
using Coop.Core.Common.Network;
using Coop.Core.Common.Session;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Instances;
using Coop.Core.Server.Services.Session.Messages;
using Coop.Core.Server.Services.Time;
using GameInterface.Services.Entity;
using GameInterface.Services.GameState;
using LiteNetLib;
using LiteNetLib.Utils;
using Serilog;
using System;
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
    private readonly IMessagePacketHandler messagePacketHandler;
    private readonly IConnectionMessageQueue connectionMessageQueue;
    // Buffers per-change sends and merges them into one send per key. Drained each tick in Update.
    private readonly ISendCoalescer coalescer;
    // Lazy breaks the construction cycle: the manager depends on ITimeControlInterface, which depends
    // on INetwork (this server). It is only needed each Update, so deferring construction is fine.
    private readonly Lazy<IOverloadedPeerManager> overloadedPeerManager;

    // Co-hosted NAT-punch rendezvous for P2P instances (taverns etc.). The server's NetManager
    // already has NatPunchEnabled; the MissionManager answers the introduction requests.
    private readonly IMissionManager missionManager;

    public CoopServer(
        INetworkConfig configuration,
        IMessageBroker messageBroker,
        IPacketManager packetManager,
        IMessagePacketHandler messagePacketHandler,
        IConnectionMessageQueue connectionMessageQueue,
        IControllerIdProvider controllerIdProvider,
        IMissionManager missionManager,
        Lazy<IOverloadedPeerManager> overloadedPeerManager,
        ISendCoalescer coalescer,
        ICommonSerializer serializer) : base(configuration, serializer)
    {
        // Dependancy assignment
        this.messageBroker = messageBroker;
        this.packetManager = packetManager;
        this.messagePacketHandler = messagePacketHandler;
        this.connectionMessageQueue = connectionMessageQueue;
        this.missionManager = missionManager;
        this.overloadedPeerManager = overloadedPeerManager;
        this.coalescer = coalescer;

        // Netmanager initialization
        netManager.NatPunchEnabled = true;
        netManager.NatPunchModule.Init(this);

        controllerIdProvider.SetControllerId(ServerControllerId);
    }

    public override void OnConnectionRequest(ConnectionRequest request)
    {
        string suppliedPassword;
        try
        {
            suppliedPassword = request.Data.GetString(ConnectionPassword.MaxLength);
        }
        catch (Exception)
        {
            Logger.Warning("Client connection rejected for {Endpoint}: malformed password data", request.RemoteEndPoint);
            RejectIncorrectPassword(request);
            return;
        }

        if (!ConnectionPassword.IsAccepted(Config.Token, suppliedPassword))
        {
            Logger.Warning("Client connection rejected for {Endpoint}: incorrect password", request.RemoteEndPoint);
            RejectIncorrectPassword(request);
            return;
        }

        Logger.Information("Client connection accepted for {Endpoint}", request.RemoteEndPoint);
        request.Accept();
    }

    private static void RejectIncorrectPassword(ConnectionRequest request)
    {
        var reason = new NetDataWriter();
        reason.Put((byte)ConnectionRejectCode.IncorrectPassword);
        request.Reject(reason);
    }

    public void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
    {
        missionManager.HandleIntroductionRequest(netManager.NatPunchModule, localEndPoint, remoteEndPoint, token);
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
        object received = serializer.Deserialize(reader.GetRemainingBytes());

        if (received is IPacket packet)
        {
            packetManager.HandleReceive(peer, packet);
        }
        else if (received is IMessage message)
        {
            messagePacketHandler.PublishEvent(peer, message);
        }
        else
        {
            Logger.Error("Received payload deserialized to neither IPacket nor IMessage: {Type}", received?.GetType());
        }
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

        // Drain this tick's coalesced sends. Update runs on the poll thread, but creates and destroys
        // send on the game thread, so we marshal the flush there too: that keeps each merged SendAll
        // ordered behind an object's create and ahead of its destroy, and off netPeer.Send's non-thread-safe
        // path. Inert until a send path enqueues, so the guard avoids queueing an empty flush every tick.
        if (coalescer.HasPending)
        {
            GameThread.RunSafe(() => coalescer.Flush(this));
        }
    }

    public override void Start()
    {
        Logger.Information("Server starting on port {Port}", Config.Port);

        if (netManager.Start(IPAddress.Any, IPAddress.IPv6Any, Config.Port))
        {
            messageBroker.Publish(this, new ServerListening());
            return;
        }

        Logger.Error("Server failed to bind port {Port}; it may already be in use", Config.Port);

        // A managed server that cannot listen is a zombie whose shutdown save would overwrite the
        // live session's save; quit without saving instead of masquerading as a reachable host.
        if (ManagedServerConfig.IsManagedServer)
        {
            GameThread.RunSafe(ServerShutdown.QuitToDesktop, context: "ServerBindFailed");
        }
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
