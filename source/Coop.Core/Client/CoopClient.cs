using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Common.Serialization;
using Coop.Core.Client.Messages;
using Coop.Core.Client.Network;
using Coop.Core.Common.Network;
using GameInterface;
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
    private readonly ILoadingPacketBuffer loadingPacketBuffer;
    private bool isConnected = false;
    private bool reconnectPending = false;
    private DateTime reconnectAfter = DateTime.MinValue;

    private IPEndPoint connectEndPoint;

    public CoopClient(
        INetworkConfiguration config,
        IMessageBroker messageBroker,
        IPacketManager packetManager,
        ILoadingPacketBuffer loadingPacketBuffer,
        ICommonSerializer serializer) : base(config, serializer)
    {
        this.messageBroker = messageBroker;
        this.packetManager = packetManager;
        this.loadingPacketBuffer = loadingPacketBuffer;
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

        // While loading a transfer save, world-change packets are buffered and replayed once the
        // campaign is ready (see ILoadingPacketBuffer); otherwise handle immediately.
        if (loadingPacketBuffer.Intercept(peer, packet)) return;

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

        reconnectPending = false;
        reconnectAfter = DateTime.MinValue;

        netManager.Start();

        var ip = ResolveConnectAddress(Configuration.Address, preferIPv6: false);
        connectEndPoint = new IPEndPoint(ip, Configuration.Port);

        Logger.Information("Attempting connection to {Endpoint}...", connectEndPoint);
        netManager.Connect(connectEndPoint, Configuration.Token);
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

        // Replay any packets buffered during the transfer-save load, in order, on this poller thread.
        foreach (var (peer, packet) in loadingPacketBuffer.DrainIfRequested())
        {
            packetManager.HandleReceive(peer, packet);
        }

        if (reconnectPending && DateTime.UtcNow >= reconnectAfter)
        {
            reconnectPending = false;

            messageBroker.Publish(this, new SendInformationMessage($"Retrying connection to {connectEndPoint}..."));
            Logger.Information("Retrying connection to {Endpoint}...", connectEndPoint);
            netManager.Connect(connectEndPoint, Configuration.Token);
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