using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Common.Serialization;
using Coop.Core.Common.Network;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface;
using GameInterface.Registry.Messages;
using GameInterface.Services.Entity;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Text;
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
    IEnumerable<NetPeer> ConnectedPeers { get; }
}

/// <inheritdoc cref="ICoopServer"/>
public class CoopServer : CoopNetworkBase, ICoopServer
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopServer>();

    public const string ServerControllerId = "Server";

    public override int Priority => 0;

    public IEnumerable<NetPeer> ConnectedPeers => netManager;

    private readonly IMessageBroker messageBroker;
    private readonly IPacketManager packetManager;
    private bool allowJoining = false;

    public CoopServer(
        INetworkConfiguration configuration, 
        IMessageBroker messageBroker,
        IPacketManager packetManager,
        IControllerIdProvider controllerIdProvider,
        ICommonSerializer serializer) : base(configuration, serializer)
    {
        // Dependancy assignment
        this.messageBroker = messageBroker;
        this.packetManager = packetManager;
        messageBroker.Subscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);

        ModInformation.IsServer = true;

        // Netmanager initialization
        netManager.NatPunchEnabled = true;
        netManager.NatPunchModule.Init(this);
        netManager.UnconnectedMessagesEnabled = true;

        controllerIdProvider.SetControllerId(ServerControllerId);
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);
        base.Dispose();
    }

    public override void OnConnectionRequest(ConnectionRequest request)
    {
        if (allowJoining)
        {
            Logger.Information("Connection request accepted from {Remote}", request.RemoteEndPoint);
            request.Accept();
            messageBroker.Publish(this, new SendInformationMessage($"Connexion acceptée: {request.RemoteEndPoint}"));
        }
        else
        {
            Logger.Warning("Connection request rejected from {Remote} (joining disabled)", request.RemoteEndPoint);
            request.Reject();
            messageBroker.Publish(this, new SendInformationMessage($"Connexion refusée: {request.RemoteEndPoint} (joining désactivé)"));
        }
    }

    public void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
    {
        Logger.Warning("NAT introduction request from {Local} to {Remote} token {Token}", localEndPoint, remoteEndPoint, token);
    }

    public void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, NatAddressType type, string token)
    {
        // Not used on server
    }

    public override void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
    {
        Logger.Error("Network error {SocketError} at {EndPoint}", socketError, endPoint);
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
        var data = reader.GetRemainingBytes();
        var text = Encoding.UTF8.GetString(data);
        if (messageType == UnconnectedMessageType.BasicMessage && text == "CoopPing")
        {
            var writer = new NetDataWriter();
            writer.Put("CoopPong");
            netManager.SendUnconnectedMessage(writer, remoteEndPoint);
            return;
        }
        Logger.Warning("Unconnected message {MessageType} from {RemoteEndPoint}", messageType, remoteEndPoint);
        messageBroker.Publish(this, new SendInformationMessage($"Paquet non-connecté: {messageType} de {remoteEndPoint}"));
    }

    public override void OnPeerConnected(NetPeer peer)
    {
        Logger.Information("Peer connected {Peer}", peer);
        PlayerConnected message = new PlayerConnected(peer);
        messageBroker.Publish(this, message);
        messageBroker.Publish(this, new SendInformationMessage($"Client connecté: {peer.Address}"));
    }

    public override void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        PlayerDisconnected message = new PlayerDisconnected(peer, disconnectInfo);
        messageBroker.Publish(this, message);
        messageBroker.Publish(this, new SendInformationMessage($"Client déconnecté: {peer.Address} ({disconnectInfo.Reason})"));
    }

    public override void Update(TimeSpan frameTime)
    {
        netManager.PollEvents();
        netManager.NatPunchModule.PollEvents();
    }

    public override void Start()
    {
        Logger.Information("CoopServer starting on port {Port}", Configuration.Port);
        var started = netManager.Start(Configuration.Port);
        if (started)
        {
            messageBroker.Publish(this, new SendInformationMessage($"Serveur: écoute sur port {Configuration.Port}"));
            allowJoining = true;
            Logger.Information("Joining enabled (startup)");
            messageBroker.Publish(this, new SendInformationMessage("Connexions activées"));
        }
        else
        {
            Logger.Error("Failed to start server on port {Port}", Configuration.Port);
            messageBroker.Publish(this, new SendInformationMessage($"Échec démarrage serveur sur port {Configuration.Port}"));
        }
    }

    public override void SendAll(IPacket packet)
    {
        CheckNetworkQueueOverloaded();
        SendAll(netManager, packet);
    }

    public override void SendAllBut(NetPeer ignoredPeer, IPacket packet)
    {
        CheckNetworkQueueOverloaded(ignoredPeer);
        SendAllBut(netManager, ignoredPeer, packet);
    }

    private void CheckNetworkQueueOverloaded(NetPeer ignoredPeer = null)
    {
        // TODO see if Parallel.ForEach works here
        foreach (var netPeer in ConnectedPeers.Where(peer => peer != ignoredPeer))
        {
            // Sending defaults to channel 0
            int outgoingPacketCount = 
                  netPeer.GetPacketsCountInReliableQueue(0, true)
                + netPeer.GetPacketsCountInReliableQueue(0, false);

            if (outgoingPacketCount > Configuration.MaxPacketsInQueue)
            {
                messageBroker.Publish(this, new PeerQueueOverloaded(netPeer));
            }
        }
    }

    private void Handle_AllGameObjectsRegistered(MessagePayload<AllGameObjectsRegistered> obj)
    {
        allowJoining = true;
        Logger.Information("All game objects registered; joining enabled");
        messageBroker.Publish(this, new SendInformationMessage("Serveur prêt: connexions activées"));
    }
}
