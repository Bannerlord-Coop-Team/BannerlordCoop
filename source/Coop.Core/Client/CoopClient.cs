using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Common.Serialization;
using Coop.Core.Client.Messages;
using Coop.Core.Common.Network;
using GameInterface;
using GameInterface.Services.GameDebug.Messages;
using LiteNetLib;
using Serilog;
using System;
using System.Net;
using System.Net.Sockets;

namespace Coop.Core.Client;

/// <summary>
/// Client used for Coop
/// </summary>
public interface ICoopClient : INetwork, IUpdateable, INetEventListener, IDisposable
{
}

/// <inheritdoc cref="ICoopClient"/>
public class CoopClient : CoopNetworkBase, ICoopClient
{
    public override int Priority => 0;
    
    private static readonly ILogger Logger = LogManager.GetLogger<CoopClient>();

    private readonly IMessageBroker messageBroker;
    private readonly IPacketManager packetManager;
    private bool isConnected = false;
    private bool reconnectPending = false;
    private DateTime reconnectAfter = DateTime.MinValue;

    public CoopClient(
        INetworkConfiguration config,
        IMessageBroker messageBroker,
        IPacketManager packetManager,
        ICommonSerializer serializer) : base(config, serializer)
    {
        this.messageBroker = messageBroker;
        this.packetManager = packetManager;
    }

    public override void OnConnectionRequest(ConnectionRequest request)
    {
        request.Reject();
    }

    public override void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
    {
        // Common during auto-connect: server not yet listening when client attempts connection.
        // Log and continue — LiteNetLib will keep retrying within the DisconnectTimeout window.
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
        if(isConnected == false)
        {
            isConnected = true;

            messageBroker.Publish(this, new SendInformationMessage("Connected! Please wait for transfer"));
            messageBroker.Publish(this, new NetworkConnected());
        }
    }

    public override void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        if (isConnected == true)
        {
            messageBroker.Publish(this, new SendInformationMessage(disconnectInfo.Reason.ToString()));
            messageBroker.Publish(this, new NetworkDisconnected(disconnectInfo));
        }
        else
        {
            // During auto-connect, the client may attempt to connect before the server has finished
            // registering game objects and opened joining. Schedule a retry until it's accepted.
            Logger.Warning("Connection attempt failed ({Reason}), retrying in 3 seconds...", disconnectInfo.Reason);
            reconnectPending = true;
            reconnectAfter = DateTime.UtcNow.AddSeconds(3);
        }
    }

    public override void Start()
    {
        messageBroker.Publish(this, new SendInformationMessage("Connecting..."));

        if (isConnected)
        {
            Dispose();
        }

        netManager.Start();

        Logger.Information("Attempting connection to {Address}:{Port}...", Configuration.Address, Configuration.Port);
        netManager.Connect(Configuration.Address, Configuration.Port, Configuration.Token);
    }

    public override void Update(TimeSpan frameTime)
    {
        netManager.PollEvents();

        // Auto-connect retry: fires after a rejection while the server was still loading.
        if (reconnectPending && DateTime.UtcNow >= reconnectAfter)
        {
            reconnectPending = false;
            Logger.Information("Retrying connection to {Address}:{Port}...", Configuration.Address, Configuration.Port);
            netManager.Connect(Configuration.Address, Configuration.Port, Configuration.Token);
        }
    }

    public override void SendAll(IPacket packet)
    {
        SendAll(netManager, packet);
    }

    public override void SendAllBut(NetPeer netPeer, IPacket packet)
    {
        SendAllBut(netManager, netPeer, packet);
    }
}