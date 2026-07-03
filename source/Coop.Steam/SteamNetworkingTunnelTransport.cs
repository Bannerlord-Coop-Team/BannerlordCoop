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
/// Reliable-class datagrams (the join save, campaign sync) ride Steam's reliable lane so
/// Steam owns pacing, retransmission, and flow control — the join-save burst overwhelms an
/// unreliable pipe. Droppable classes (movement snapshots, pings) stay unreliable so a
/// lost packet never head-of-line-blocks newer ones behind a retransmit.
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

    public void EnsureRelayAccess()
    {
        ApplyGlobalTunnelConfig();
        SteamNetworkingUtils.InitRelayNetworkAccess();
    }

    public string DescribeConnection(uint connection)
    {
        var status = default(SteamNetConnectionRealTimeStatus_t);
        var lanes = default(SteamNetConnectionRealTimeLaneStatus_t);
        var result = SteamNetworkingSockets.GetConnectionRealTimeStatus(
            new HSteamNetConnection(connection), ref status, 0, ref lanes);
        if (result != EResult.k_EResultOK) return $"status unavailable ({result})";

        return $"sendRate={status.m_nSendRateBytesPerSecond}B/s out={status.m_flOutBytesPerSec:F0}B/s " +
            $"in={status.m_flInBytesPerSec:F0}B/s pendingReliable={status.m_cbPendingReliable} " +
            $"unacked={status.m_cbSentUnackedReliable} ping={status.m_nPing}ms " +
            $"quality={status.m_flConnectionQualityLocal:F2}";
    }

    // The per-call options on ConnectP2P/CreateListenSocketP2P should already cover this,
    // but whether an accepted connection inherits listen-socket options is the one link we
    // cannot observe locally, so the same values are also set globally.
    private static void ApplyGlobalTunnelConfig()
    {
        SetConfigInt32(ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global, IntPtr.Zero,
            ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendBufferSize, SteamTunnel.SendBufferBytes);
        SetConfigInt32(ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global, IntPtr.Zero,
            ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendRateMax, SteamTunnel.SendRateMaxBytesPerSecond);
    }

    public void SetSendRateFloor(uint connection, int bytesPerSecond)
    {
        SetConfigInt32(ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Connection, (IntPtr)connection,
            ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendRateMin, bytesPerSecond);
    }

    public bool TryGetSendHealth(uint connection, out int pendingReliableBytes, out float quality)
    {
        var status = default(SteamNetConnectionRealTimeStatus_t);
        var lanes = default(SteamNetConnectionRealTimeLaneStatus_t);
        var result = SteamNetworkingSockets.GetConnectionRealTimeStatus(
            new HSteamNetConnection(connection), ref status, 0, ref lanes);

        pendingReliableBytes = status.m_cbPendingReliable;
        quality = status.m_flConnectionQualityLocal;
        return result == EResult.k_EResultOK;
    }

    private static void SetConfigInt32(ESteamNetworkingConfigScope scope, IntPtr scopeObj,
        ESteamNetworkingConfigValue key, int value)
    {
        var handle = GCHandle.Alloc(value, GCHandleType.Pinned);
        try
        {
            if (!SteamNetworkingUtils.SetConfigValue(key, scope, scopeObj,
                ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32, handle.AddrOfPinnedObject()))
            {
                Logger.Warning("Steam refused tunnel config {Key} at scope {Scope}", key, scope);
            }
        }
        finally
        {
            handle.Free();
        }
    }

    public uint ConnectToHost(ulong hostSteamId, int virtualPort)
    {
        var identity = new SteamNetworkingIdentity();
        identity.SetSteamID64(hostSteamId);

        var options = TunnelConnectionOptions();
        var connection = SteamNetworkingSockets.ConnectP2P(ref identity, virtualPort, options.Length, options);
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

            var options = TunnelConnectionOptions();
            var socket = SteamNetworkingSockets.CreateListenSocketP2P(virtualPort, options.Length, options);
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
            return;
        }

        // Observed live: an accepted connection ignores both the listen-socket options and
        // the global values (it stayed pinned at the 256 KB/s default), while connection
        // scope provably works, so the config is applied straight onto it.
        SetConfigInt32(ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Connection, (IntPtr)connection,
            ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendBufferSize, SteamTunnel.SendBufferBytes);
        SetConfigInt32(ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Connection, (IntPtr)connection,
            ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendRateMax, SteamTunnel.SendRateMaxBytesPerSecond);

        // Read-back separates "stored but the pacer ignores it" from "never stored".
        Logger.Information("Tunnel connection {Connection} config: sendRateMin={Min} sendRateMax={Max} sendBuffer={Buffer}",
            connection,
            ReadConfigInt32(connection, ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendRateMin),
            ReadConfigInt32(connection, ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendRateMax),
            ReadConfigInt32(connection, ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendBufferSize));
    }

    private static string ReadConfigInt32(uint connection, ESteamNetworkingConfigValue key)
    {
        var buffer = new int[1];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            ulong size = sizeof(int);
            var result = SteamNetworkingUtils.GetConfigValue(key,
                ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Connection, (IntPtr)connection,
                out _, handle.AddrOfPinnedObject(), ref size);

            return result == ESteamNetworkingGetConfigValueResult.k_ESteamNetworkingGetConfigValue_OK ||
                result == ESteamNetworkingGetConfigValueResult.k_ESteamNetworkingGetConfigValue_OKInherited
                ? buffer[0].ToString()
                : result.ToString();
        }
        finally
        {
            handle.Free();
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

    public bool SendDatagram(uint connection, byte[] data, int length, bool droppable)
    {
        int sendFlags = droppable
            ? Constants.k_nSteamNetworkingSend_UnreliableNoNagle
            : Constants.k_nSteamNetworkingSend_ReliableNoNagle;

        // Pinning the caller's buffer avoids a copy and keeps sends off the shared gate,
        // which the game thread's status callback also takes.
        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            var result = SteamNetworkingSockets.SendMessageToConnection(
                new HSteamNetConnection(connection), handle.AddrOfPinnedObject(), (uint)length,
                sendFlags, out _);

            // Only a full send buffer asks the pump to retry, and only for reliable-class
            // datagrams (a droppable one is simply lost, per its contract). Any other
            // failure means the connection is gone, so the datagram is swallowed and the
            // Closed event that follows tears the peer down.
            return droppable || result != EResult.k_EResultLimitExceeded;
        }
        finally
        {
            handle.Free();
        }
    }

    // A full send buffer (k_EResultLimitExceeded) surfaces as false from SendDatagram, and
    // the pumps hold the datagram until it fits.
    private static SteamNetworkingConfigValue_t[] TunnelConnectionOptions()
    {
        return new[]
        {
            Int32Option(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendBufferSize, SteamTunnel.SendBufferBytes),
            Int32Option(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendRateMax, SteamTunnel.SendRateMaxBytesPerSecond),
        };
    }

    private static SteamNetworkingConfigValue_t Int32Option(ESteamNetworkingConfigValue key, int value)
    {
        return new SteamNetworkingConfigValue_t
        {
            m_eValue = key,
            m_eDataType = ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
            m_val = new SteamNetworkingConfigValue_t.OptionValue { m_int32 = value },
        };
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
