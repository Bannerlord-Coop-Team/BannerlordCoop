using Serilog;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Coop.Steam;

/// <summary>Shared tunnel mechanics; subclasses bind the user or game-server Steamworks surface.</summary>
public abstract class SteamNetworkingTunnelTransportBase :
    ISteamTunnelTransport,
    ISteamTunnelConnectionIdentityResolver
{
    protected readonly ILogger Logger;

    private readonly object gate = new object();
    private readonly HashSet<uint> ownedConnections = new HashSet<uint>();
    private readonly TunnelConnectionIdentityRegistry remoteIdentities = new TunnelConnectionIdentityRegistry();
    private readonly Callback<SteamNetConnectionStatusChangedCallback_t> statusChangedCallback;
    private readonly IntPtr[] receivedMessage = new IntPtr[1];
    private HSteamListenSocket listenSocket = HSteamListenSocket.Invalid;

    public event Action<uint, TunnelConnectionState> ConnectionStateChanged;

    protected SteamNetworkingTunnelTransportBase(ILogger logger)
    {
        Logger = logger;
        statusChangedCallback = CreateStatusChangedCallback(OnConnectionStatusChanged);
    }

    // Per-flavor bindings: the user flavor routes these through SteamNetworkingSockets/Utils and
    // the game-server flavor through their SteamGameServer* siblings, dispatched by the matching
    // callback pump (SteamAPI.RunCallbacks vs GameServer.RunCallbacks).
    protected abstract Callback<SteamNetConnectionStatusChangedCallback_t> CreateStatusChangedCallback(
        Callback<SteamNetConnectionStatusChangedCallback_t>.DispatchDelegate handler);

    protected abstract void InitRelayNetworkAccess();

    protected abstract bool SetConfigValue(ESteamNetworkingConfigValue key, ESteamNetworkingConfigScope scope,
        IntPtr scopeObj, ESteamNetworkingConfigDataType type, IntPtr arg);

    protected abstract ESteamNetworkingGetConfigValueResult GetConfigValue(ESteamNetworkingConfigValue key,
        ESteamNetworkingConfigScope scope, IntPtr scopeObj, out ESteamNetworkingConfigDataType type,
        IntPtr result, ref ulong size);

    protected abstract EResult GetConnectionRealTimeStatus(HSteamNetConnection conn,
        ref SteamNetConnectionRealTimeStatus_t status, int lanes,
        ref SteamNetConnectionRealTimeLaneStatus_t laneStatus);

    protected abstract HSteamNetConnection ConnectP2P(ref SteamNetworkingIdentity identity, int virtualPort,
        int options, SteamNetworkingConfigValue_t[] optionValues);

    protected abstract HSteamListenSocket CreateListenSocketP2P(int virtualPort, int options,
        SteamNetworkingConfigValue_t[] optionValues);

    protected abstract bool CloseListenSocket(HSteamListenSocket socket);

    protected abstract EResult AcceptConnectionRaw(HSteamNetConnection conn);

    protected abstract bool CloseConnectionRaw(HSteamNetConnection conn, int reason, string debug, bool enableLinger);

    protected abstract EResult SendMessageToConnection(HSteamNetConnection conn, IntPtr data, uint size,
        int flags, out long messageNumber);

    protected abstract int ReceiveMessagesOnConnection(HSteamNetConnection conn, IntPtr[] messages, int maxMessages);

    public void EnsureRelayAccess()
    {
        ApplyGlobalTunnelConfig();
        InitRelayNetworkAccess();
    }

    public string DescribeConnection(uint connection)
    {
        var status = default(SteamNetConnectionRealTimeStatus_t);
        var lanes = default(SteamNetConnectionRealTimeLaneStatus_t);
        var result = GetConnectionRealTimeStatus(new HSteamNetConnection(connection), ref status, 0, ref lanes);
        if (result != EResult.k_EResultOK) return $"status unavailable ({result})";

        return $"sendRate={status.m_nSendRateBytesPerSecond}B/s out={status.m_flOutBytesPerSec:F0}B/s " +
            $"in={status.m_flInBytesPerSec:F0}B/s pendingReliable={status.m_cbPendingReliable} " +
            $"unacked={status.m_cbSentUnackedReliable} ping={status.m_nPing}ms " +
            $"quality={status.m_flConnectionQualityLocal:F2}";
    }

    // The per-call options on ConnectP2P/CreateListenSocketP2P should already cover this,
    // but whether an accepted connection inherits listen-socket options is the one link we
    // cannot observe locally, so the same values are also set globally.
    private void ApplyGlobalTunnelConfig()
    {
        SetConfigInt32(ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global, IntPtr.Zero,
            ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutConnected, SteamTunnel.ConnectedTimeoutMilliseconds);
        SetConfigInt32(ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global, IntPtr.Zero,
            ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendBufferSize, SteamTunnel.SendBufferBytes);
        SetConfigInt32(ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global, IntPtr.Zero,
            ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendRateMin, SteamTunnel.SendRateMinBytesPerSecond);
        SetConfigInt32(ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global, IntPtr.Zero,
            ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendRateMax, SteamTunnel.SendRateMaxBytesPerSecond);
    }

    private void SetConfigInt32(ESteamNetworkingConfigScope scope, IntPtr scopeObj,
        ESteamNetworkingConfigValue key, int value)
    {
        var handle = GCHandle.Alloc(value, GCHandleType.Pinned);
        try
        {
            if (!SetConfigValue(key, scope, scopeObj,
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

    public virtual uint ConnectToHost(ulong hostSteamId, int virtualPort)
    {
        var identity = CreateSteamIdentity(hostSteamId);

        var options = TunnelConnectionOptions();
        var connection = ConnectP2P(ref identity, virtualPort, options.Length, options);
        if (connection == HSteamNetConnection.Invalid)
        {
            throw new InvalidOperationException("Steam refused the tunnel connection");
        }

        lock (gate)
        {
            ownedConnections.Add(connection.m_HSteamNetConnection);
            remoteIdentities.Record(connection.m_HSteamNetConnection, hostSteamId);
        }

        return connection.m_HSteamNetConnection;
    }

    protected virtual SteamNetworkingIdentity CreateSteamIdentity(ulong steamId)
    {
        var identity = new SteamNetworkingIdentity();
        identity.SetSteamID64(steamId);
        return identity;
    }

    public void ListenForClients(int virtualPort)
    {
        lock (gate)
        {
            if (listenSocket != HSteamListenSocket.Invalid) return;

            var options = TunnelConnectionOptions();
            var socket = CreateListenSocketP2P(virtualPort, options.Length, options);
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

            CloseListenSocket(listenSocket);
            listenSocket = HSteamListenSocket.Invalid;
        }
    }

    public void AcceptConnection(uint connection)
    {
        var result = AcceptConnectionRaw(new HSteamNetConnection(connection));
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
            ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutConnected, SteamTunnel.ConnectedTimeoutMilliseconds);
        SetConfigInt32(ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Connection, (IntPtr)connection,
            ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendBufferSize, SteamTunnel.SendBufferBytes);
        SetConfigInt32(ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Connection, (IntPtr)connection,
            ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendRateMin, SteamTunnel.SendRateMinBytesPerSecond);
        SetConfigInt32(ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Connection, (IntPtr)connection,
            ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendRateMax, SteamTunnel.SendRateMaxBytesPerSecond);

        // Read-back separates "stored but the pacer ignores it" from "never stored".
        Logger.Information("Tunnel connection {Connection} config: connectedTimeout={Timeout} sendRateMin={Min} sendRateMax={Max} sendBuffer={Buffer}",
            connection,
            ReadConfigInt32(connection, ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutConnected),
            ReadConfigInt32(connection, ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendRateMin),
            ReadConfigInt32(connection, ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendRateMax),
            ReadConfigInt32(connection, ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendBufferSize));
    }

    private string ReadConfigInt32(uint connection, ESteamNetworkingConfigValue key)
    {
        var buffer = new int[1];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            ulong size = sizeof(int);
            var result = GetConfigValue(key,
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
            remoteIdentities.Remove(connection);
        }

        if (owned)
        {
            CloseConnectionRaw(new HSteamNetConnection(connection), 0, string.Empty, false);
        }
    }

    public bool TryGetRemoteSteamId(uint connection, out ulong steamId)
    {
        lock (gate)
        {
            return remoteIdentities.TryGet(connection, out steamId);
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
            var result = SendMessageToConnection(
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
            Int32Option(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutConnected, SteamTunnel.ConnectedTimeoutMilliseconds),
            Int32Option(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendBufferSize, SteamTunnel.SendBufferBytes),
            Int32Option(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendRateMin, SteamTunnel.SendRateMinBytesPerSecond),
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
        int count = ReceiveMessagesOnConnection(new HSteamNetConnection(connection), receivedMessage, 1);
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
                    remoteIdentities.Record(connection, change.m_info.m_identityRemote.GetSteamID64());
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
                    try
                    {
                        ConnectionStateChanged?.Invoke(connection, TunnelConnectionState.Closed);
                    }
                    finally
                    {
                        CloseConnection(connection);
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            // The Steam dispatcher swallows handler exceptions into Console, invisible in our logs.
            Logger.Error(ex, "Tunnel connection status handler failed");
        }
    }

    public virtual void Dispose()
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

        lock (gate)
        {
            remoteIdentities.Clear();
        }

        statusChangedCallback?.Dispose();
    }
}
