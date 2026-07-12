using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Common.Serialization;
using Coop.Core.Client.Messages;
using Coop.Core.Common.Network;
using GameInterface.Services.GameDebug.Messages;
using LiteNetLib;
using Serilog;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Coop.Core.Client;

/// <summary>
/// Client used for Coop
/// </summary>
public interface ICoopClient : IRelayNetwork, IUpdateable, INetEventListener, IDisposable
{
}

/// <inheritdoc cref="ICoopClient"/>
public class CoopClient : CoopNetworkBase, ICoopClient
{
    public override int Priority => 0;
    public IPEndPoint ServerEndpoint { get; private set; }

    private static readonly ILogger Logger = LogManager.GetLogger<CoopClient>();

    private readonly IMessageBroker messageBroker;
    private readonly IPacketManager packetManager;
    private readonly IMessagePacketHandler messagePacketHandler;
    private bool isConnected = false;
    private bool reconnectPending = false;
    private DateTime reconnectAfter = DateTime.MinValue;

    public CoopClient(
        INetworkConfig config,
        IMessageBroker messageBroker,
        IPacketManager packetManager,
        IMessagePacketHandler messagePacketHandler,
        ICommonSerializer serializer) : base(config, serializer)
    {
        this.messageBroker = messageBroker;
        this.packetManager = packetManager;
        this.messagePacketHandler = messagePacketHandler;
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
        try
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
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to process packet");
        }
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

        reconnectPending = false;
        reconnectAfter = DateTime.MinValue;

        netManager.Start();

        var ip = ResolveConnectAddress(Config.Address, preferIPv6: false);
        ServerEndpoint = new IPEndPoint(ip, Config.Port);

        Logger.Information("Attempting connection to {Endpoint}...", ServerEndpoint);
        netManager.Connect(ServerEndpoint, Config.Token);
    }

    private static IPAddress ResolveConnectAddress(string address, bool preferIPv6)
    {
        if (IPAddress.TryParse(address, out var parsed))
            return parsed;

        var addresses = Dns.GetHostAddresses(address)
            .Where(IsUsableConnectAddress)
            .ToArray();

        var preferredFamily = preferIPv6
            ? AddressFamily.InterNetworkV6
            : AddressFamily.InterNetwork;

        var fallbackFamily = preferIPv6
            ? AddressFamily.InterNetwork
            : AddressFamily.InterNetworkV6;

        return addresses.FirstOrDefault(x => x.AddressFamily == preferredFamily)
            ?? addresses.FirstOrDefault(x => x.AddressFamily == fallbackFamily)
            ?? throw new InvalidOperationException($"Could not resolve usable address: {address}");
    }

    private static bool IsUsableConnectAddress(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetwork)
            return true;

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return !address.IsIPv6LinkLocal
                && !address.IsIPv6Multicast
                && !address.IsIPv6SiteLocal;
        }

        return false;
    }

    public override void Update(TimeSpan frameTime)
    {
        netManager.PollEvents();

        // Send any sub-budget aggregated messages so nothing waits longer than one poll interval.
        FlushPendingMessages();

        if (reconnectPending && DateTime.UtcNow >= reconnectAfter)
        {
            reconnectPending = false;

            messageBroker.Publish(this, new SendInformationMessage("Retrying connection to server..."));
            Logger.Information("Retrying connection to {Endpoint}...", ServerEndpoint);
            netManager.Connect(ServerEndpoint, Config.Token);
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