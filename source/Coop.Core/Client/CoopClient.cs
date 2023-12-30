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
    private readonly NetManager netManager;

    private bool isConnected = false;

    public CoopClient(
        INetworkConfiguration config,
        IMessageBroker messageBroker,
        IPacketManager packetManager) : base(config)
    {
        this.messageBroker = messageBroker;
        this.packetManager = packetManager;

        // TODO add configuration
        netManager = new NetManager(this);

        netManager.DisconnectTimeout = 300 * 1000;
#if DEBUG
        // Increase disconnect timeout to prevent disconnect during debugging
        netManager.DisconnectTimeout = 300 * 1000;
#endif
    }

    public void Dispose()
    {
        Stop();
    }

    public void Disconnect()
    {
        netManager.DisconnectAll();
    }

    public void OnConnectionRequest(ConnectionRequest request)
    {
        request.Reject();
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
        packetManager.HandleReceive(peer, packet);
    }

    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
    {
        throw new NotImplementedException();
    }

    public void OnPeerConnected(NetPeer peer)
    {
        if(isConnected == false)
        {
            isConnected = true;

            messageBroker.Publish(this, new SendInformationMessage("Connected! Please wait for transfer"));
            messageBroker.Publish(this, new NetworkConnected());
        }
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        if (isConnected == true)
        {
            messageBroker.Publish(this, new SendInformationMessage(disconnectInfo.Reason.ToString()));
            messageBroker.Publish(this, new NetworkDisconnected(disconnectInfo));
        }
    }

    public override void Start()
    {
        messageBroker.Publish(this, new SendInformationMessage("Connecting..."));

        if (isConnected)
        {
            Stop();
        }

        netManager.Start();

        netManager.Connect(Configuration.Address, Configuration.Port, Configuration.Token);
    }

    public override void Stop()
    {
        netManager.Stop();
    }

    public override void Update(TimeSpan frameTime)
    {
        netManager.PollEvents();
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