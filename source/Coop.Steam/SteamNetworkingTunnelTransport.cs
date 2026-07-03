using Common.Logging;
using Serilog;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Coop.Steam;

/// <inheritdoc cref="ISteamTunnelTransport"/>
/// <remarks>
/// User-flavor sockets under the player's own Steam identity, so the game's per-frame
/// SteamAPI.RunCallbacks pump dispatches the status callback with no pump of our own.
/// Datagrams are sent unreliable: LiteNetLib runs its own reliability on top.
/// </remarks>
public class SteamNetworkingTunnelTransport : ISteamTunnelTransport
{
    private static readonly ILogger Logger = LogManager.GetLogger<SteamNetworkingTunnelTransport>();

    private readonly object gate = new object();
    private readonly HashSet<uint> ownedConnections = new HashSet<uint>();
    private readonly Callback<SteamNetConnectionStatusChangedCallback_t> statusChangedCallback;
    private readonly IntPtr[] receivedMessage = new IntPtr[1];
    private HSteamListenSocket listenSocket = HSteamListenSocket.Invalid;

    public event Action<uint, TunnelConnectionState> ConnectionStateChanged;

    public SteamNetworkingTunnelTransport()
    {
        statusChangedCallback = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnConnectionStatusChanged);
    }

    public void EnsureRelayAccess() => SteamNetworkingUtils.InitRelayNetworkAccess();

    public uint ConnectToHost(ulong hostSteamId, int virtualPort)
    {
        var identity = new SteamNetworkingIdentity();
        identity.SetSteamID64(hostSteamId);

        var connection = SteamNetworkingSockets.ConnectP2P(ref identity, virtualPort, 0, null);
        if (connection == HSteamNetConnection.Invalid)
        {
            throw new InvalidOperationException("Steam refused the tunnel connection");
        }

        lock (gate)
        {
            ownedConnections.Add(connection.m_HSteamNetConnection);
        }

        return connection.m_HSteamNetConnection;
    }

    public void ListenForClients(int virtualPort)
    {
        lock (gate)
        {
            if (listenSocket != HSteamListenSocket.Invalid) return;

            var socket = SteamNetworkingSockets.CreateListenSocketP2P(virtualPort, 0, null);
            if (socket == HSteamListenSocket.Invalid)
            {
                throw new InvalidOperationException("Steam refused the tunnel listen socket");
            }

            listenSocket = socket;
        }
    }

    public void StopListening()
    {
        lock (gate)
        {
            if (listenSocket == HSteamListenSocket.Invalid) return;

            SteamNetworkingSockets.CloseListenSocket(listenSocket);
            listenSocket = HSteamListenSocket.Invalid;
        }
    }

    public void AcceptConnection(uint connection)
    {
        var result = SteamNetworkingSockets.AcceptConnection(new HSteamNetConnection(connection));
        if (result != EResult.k_EResultOK)
        {
            Logger.Error("Accepting tunnel connection {Connection} failed: {Result}", connection, result);
            CloseConnection(connection);
        }
    }

    public void CloseConnection(uint connection)
    {
        bool owned;
        lock (gate)
        {
            owned = ownedConnections.Remove(connection);
        }

        if (owned)
        {
            SteamNetworkingSockets.CloseConnection(new HSteamNetConnection(connection), 0, string.Empty, false);
        }
    }

    public bool SendDatagram(uint connection, byte[] data, int length)
    {
        // Pinning the caller's buffer avoids a copy and keeps sends off the shared gate,
        // which the game thread's status callback also takes.
        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            var result = SteamNetworkingSockets.SendMessageToConnection(
                new HSteamNetConnection(connection), handle.AddrOfPinnedObject(), (uint)length,
                Constants.k_nSteamNetworkingSend_UnreliableNoNagle, out _);

            return result == EResult.k_EResultOK;
        }
        finally
        {
            handle.Free();
        }
    }

    public int ReceiveDatagram(uint connection, byte[] buffer)
    {
        int count = SteamNetworkingSockets.ReceiveMessagesOnConnection(
            new HSteamNetConnection(connection), receivedMessage, 1);
        if (count <= 0) return 0;

        try
        {
            var message = SteamNetworkingMessage_t.FromIntPtr(receivedMessage[0]);
            if (message.m_cbSize > buffer.Length)
            {
                Logger.Warning("Dropping oversized tunnel datagram ({Size} bytes)", message.m_cbSize);
                return 0;
            }

            Marshal.Copy(message.m_pData, buffer, 0, message.m_cbSize);
            return message.m_cbSize;
        }
        finally
        {
            SteamNetworkingMessage_t.Release(receivedMessage[0]);
        }
    }

    private void OnConnectionStatusChanged(SteamNetConnectionStatusChangedCallback_t change)
    {
        try
        {
            uint connection = change.m_hConn.m_HSteamNetConnection;

            bool inbound;
            lock (gate)
            {
                inbound = listenSocket != HSteamListenSocket.Invalid && change.m_info.m_hListenSocket == listenSocket;

                if (inbound)
                {
                    ownedConnections.Add(connection);
                }
                else if (!ownedConnections.Contains(connection))
                {
                    return;
                }
            }

            switch (change.m_info.m_eState)
            {
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                    if (inbound)
                    {
                        ConnectionStateChanged?.Invoke(connection, TunnelConnectionState.Connecting);
                    }
                    break;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                    ConnectionStateChanged?.Invoke(connection, TunnelConnectionState.Connected);
                    break;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                    Logger.Information("Tunnel connection {Connection} closed: {Reason}",
                        connection, change.m_info.m_szEndDebug);
                    ConnectionStateChanged?.Invoke(connection, TunnelConnectionState.Closed);
                    CloseConnection(connection);
                    break;
            }
        }
        catch (Exception ex)
        {
            // The Steam dispatcher swallows handler exceptions into Console, invisible in our logs.
            Logger.Error(ex, "Tunnel connection status handler failed");
        }
    }

    public void Dispose()
    {
        StopListening();

        uint[] connections;
        lock (gate)
        {
            connections = new uint[ownedConnections.Count];
            ownedConnections.CopyTo(connections);
        }

        foreach (var connection in connections)
        {
            CloseConnection(connection);
        }

        statusChangedCallback?.Dispose();
    }
}
