using Common.Logging;
using Steamworks;
using System;

namespace Coop.Steam;

/// <inheritdoc cref="SteamNetworkingTunnelTransportBase"/>
/// <remarks>
/// User-flavor sockets under the player's own Steam identity, so the game's per-frame
/// SteamAPI.RunCallbacks pump dispatches the status callback with no pump of our own.
/// </remarks>
public class SteamNetworkingTunnelTransport : SteamNetworkingTunnelTransportBase
{
    public SteamNetworkingTunnelTransport()
        : base(LogManager.GetLogger<SteamNetworkingTunnelTransport>())
    {
    }

    protected override Callback<SteamNetConnectionStatusChangedCallback_t> CreateStatusChangedCallback(
        Callback<SteamNetConnectionStatusChangedCallback_t>.DispatchDelegate handler)
        => Callback<SteamNetConnectionStatusChangedCallback_t>.Create(handler);

    protected override void InitRelayNetworkAccess()
        => SteamNetworkingUtils.InitRelayNetworkAccess();

    protected override bool SetConfigValue(ESteamNetworkingConfigValue key, ESteamNetworkingConfigScope scope,
        IntPtr scopeObj, ESteamNetworkingConfigDataType type, IntPtr arg)
        => SteamNetworkingUtils.SetConfigValue(key, scope, scopeObj, type, arg);

    protected override ESteamNetworkingGetConfigValueResult GetConfigValue(ESteamNetworkingConfigValue key,
        ESteamNetworkingConfigScope scope, IntPtr scopeObj, out ESteamNetworkingConfigDataType type,
        IntPtr result, ref ulong size)
        => SteamNetworkingUtils.GetConfigValue(key, scope, scopeObj, out type, result, ref size);

    protected override EResult GetConnectionRealTimeStatus(HSteamNetConnection conn,
        ref SteamNetConnectionRealTimeStatus_t status, int lanes,
        ref SteamNetConnectionRealTimeLaneStatus_t laneStatus)
        => SteamNetworkingSockets.GetConnectionRealTimeStatus(conn, ref status, lanes, ref laneStatus);

    protected override HSteamNetConnection ConnectP2P(ref SteamNetworkingIdentity identity, int virtualPort,
        int options, SteamNetworkingConfigValue_t[] optionValues)
        => SteamNetworkingSockets.ConnectP2P(ref identity, virtualPort, options, optionValues);

    protected override HSteamListenSocket CreateListenSocketP2P(int virtualPort, int options,
        SteamNetworkingConfigValue_t[] optionValues)
        => SteamNetworkingSockets.CreateListenSocketP2P(virtualPort, options, optionValues);

    protected override bool CloseListenSocket(HSteamListenSocket socket)
        => SteamNetworkingSockets.CloseListenSocket(socket);

    protected override EResult AcceptConnectionRaw(HSteamNetConnection conn)
        => SteamNetworkingSockets.AcceptConnection(conn);

    protected override bool CloseConnectionRaw(HSteamNetConnection conn, int reason, string debug, bool enableLinger)
        => SteamNetworkingSockets.CloseConnection(conn, reason, debug, enableLinger);

    protected override EResult SendMessageToConnection(HSteamNetConnection conn, IntPtr data, uint size,
        int flags, out long messageNumber)
        => SteamNetworkingSockets.SendMessageToConnection(conn, data, size, flags, out messageNumber);

    protected override int ReceiveMessagesOnConnection(HSteamNetConnection conn, IntPtr[] messages, int maxMessages)
        => SteamNetworkingSockets.ReceiveMessagesOnConnection(conn, messages, maxMessages);
}
