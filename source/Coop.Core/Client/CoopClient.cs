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
using System.Linq;
using System.Text;
using LiteNetLib.Utils;

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
    private DateTime? connectStart;
    private int reconnectAttempts = 0;
    private bool serverReachable = false;
    private DateTime? pingSentAt;
    private IPEndPoint serverEndPoint;

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
        // Client should not accept inbound connection requests; reject by design.
        request.Reject();
    }

    public override void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
    {
        Logger.Error("Network error {SocketError} at {EndPoint}", socketError, endPoint);
        messageBroker.Publish(this, new SendInformationMessage($"Erreur réseau: {socketError}"));
        // Attempt to reconnect on errors to keep UX smooth.
        AttemptReconnect("Erreur réseau");
    }

    public override void OnNetworkLatencyUpdate(NetPeer peer, int latency)
    {
        
    }

    public override void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        // Deserialize and route packets to the packet manager for handling.
        IPacket packet = (IPacket)serializer.Deserialize(reader.GetRemainingBytes());
        packetManager.HandleReceive(peer, packet);
    }

    public override void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
    {
        var data = reader.GetRemainingBytes();
        var text = Encoding.UTF8.GetString(data);
        if (messageType == UnconnectedMessageType.BasicMessage && text == "CoopPong")
        {
            if (serverEndPoint != null && remoteEndPoint.Equals(serverEndPoint))
            {
                serverReachable = true;
                Logger.Information("Server reachable via UDP ping {Remote}", remoteEndPoint);
                messageBroker.Publish(this, new SendInformationMessage("Serveur joignable (UDP)"));
                // On ping response, perform a normal Connect using LiteNetLib token.
                netManager.Connect(Configuration.Address, Configuration.Port, Configuration.Token);
                connectStart = DateTime.Now;
            }
            return;
        }
        Logger.Warning("Unconnected message {MessageType} from {RemoteEndPoint}", messageType, remoteEndPoint);
    }

    public override void OnPeerConnected(NetPeer peer)
    {
        if(isConnected == false)
        {
            isConnected = true;

            messageBroker.Publish(this, new SendInformationMessage("Connected! Please wait for transfer"));
            // Notify client logic to transition out of MainMenuState into validation flow.
            messageBroker.Publish(this, new NetworkConnected());
        }
    }

    public override void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        if (isConnected == true)
        {
            messageBroker.Publish(this, new SendInformationMessage(disconnectInfo.Reason.ToString()));
            AttemptReconnect("Peer disconnected: " + disconnectInfo.Reason);
        }
    }

    public override void Start()
    {
        messageBroker.Publish(this, new SendInformationMessage("Connecting..."));

        if (isConnected)
        {
            // Ensure a clean start by disposing any previous manager instance.
            Dispose();
        }

        var started = netManager.Start();
        if (!started)
        {
            Logger.Error("Client network start failed");
            messageBroker.Publish(this, new SendInformationMessage("Client: démarrage réseau échoué"));
            return;
        }

        try
        {
            // Resolve server IP (supports hostnames), then send an unconnected UDP ping.
            var hostEntry = Dns.GetHostEntry(Configuration.Address);
            var ip = hostEntry.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? hostEntry.AddressList.FirstOrDefault();
            if (ip == null)
            {
                throw new Exception("No resolved IP address");
            }
            serverEndPoint = new IPEndPoint(ip, Configuration.Port);
            var writer = new NetDataWriter();
            writer.Put("CoopPing");
            netManager.SendUnconnectedMessage(writer, serverEndPoint);
            pingSentAt = DateTime.Now;
            serverReachable = false;
            messageBroker.Publish(this, new SendInformationMessage($"Ping serveur {Configuration.Address}:{Configuration.Port}"));
        }
        catch (Exception ex)
        {
            Logger.Error("DNS resolve/connect error {Error}", ex.Message);
            messageBroker.Publish(this, new SendInformationMessage("Erreur de résolution DNS pour l'adresse du serveur"));
        }
    }

    public override void Update(TimeSpan frameTime)
    {
        netManager.PollEvents();
        if (isConnected == false && connectStart.HasValue)
        {
            var now = DateTime.Now;
            if (now - connectStart.Value > Configuration.ConnectionTimeout)
            {
                AttemptReconnect("Connexion échouée: timeout");
            }
        }
        if (!serverReachable && pingSentAt.HasValue)
        {
            var now = DateTime.Now;
            if (now - pingSentAt.Value > TimeSpan.FromSeconds(3))
            {
                messageBroker.Publish(this, new SendInformationMessage("Serveur injoignable (pas de réponse ping UDP), tentative de connexion directe"));
                pingSentAt = null;
                try
                {
                    netManager.Connect(Configuration.Address, Configuration.Port, Configuration.Token);
                    connectStart = DateTime.Now;
                }
                catch (Exception ex)
                {
                    Logger.Error("Direct connect failed {Error}", ex.Message);
                }
            }
        }
    }

    private void AttemptReconnect(string reason)
    {
        reconnectAttempts++;
        Logger.Warning("Attempting reconnect #{Attempt} ({Reason})", reconnectAttempts, reason);
        messageBroker.Publish(this, new SendInformationMessage($"Reconnexion en cours (tentative {reconnectAttempts})"));

        try
        {
            netManager.Stop();
        }
        catch { }

        var started = netManager.Start();
        if (!started)
        {
            Logger.Error("Client network restart failed");
            messageBroker.Publish(this, new SendInformationMessage("Client: redémarrage réseau échoué"));
            return;
        }

        Logger.Information("Reconnecting to {Address}:{Port}", Configuration.Address, Configuration.Port);
        netManager.Connect(Configuration.Address, Configuration.Port, Configuration.Token);
        connectStart = DateTime.Now;
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
