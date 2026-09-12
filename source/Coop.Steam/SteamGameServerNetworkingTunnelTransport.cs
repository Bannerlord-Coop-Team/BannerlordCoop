using Common.Logging;
using Steamworks;
using System;

namespace Coop.Steam;

/// <inheritdoc cref="SteamNetworkingTunnelTransportBase"/>
/// <remarks>
/// Uses the standalone process's anonymous game-server identity. Its callbacks come from the
/// explicit game-server pump, and this flavor only listens for clients.
/// </remarks>
public class SteamGameServerNetworkingTunnelTransport : SteamNetworkingTunnelTransportBase
{
    public SteamGameServerNetworkingTunnelTransport()
        : base(LogManager.GetLogger<SteamGameServerNetworkingTunnelTransport>())
    {
    }

    public override uint ConnectToHost(ulong hostSteamId, int virtualPort)
        => throw new NotSupportedException("The game-server tunnel transport only listens for clients");

    protected override Callback<SteamNetConnectionStatusChangedCallback_t> CreateStatusChangedCallback(
        Callback<SteamNetConnectionStatusChangedCallback_t>.DispatchDelegate handler)
        => Callback<SteamNetConnectionStatusChangedCallback_t>.CreateGameServer(handler);

    protected override void InitRelayNetworkAccess()
        => SteamGameServerNetworkingUtils.InitRelayNetworkAccess();

    protected override bool SetConfigValue(ESteamNetworkingConfigValue key, ESteamNetworkingConfigScope scope,
        IntPtr scopeObj, ESteamNetworkingConfigDataType type, IntPtr arg)
        => SteamGameServerNetworkingUtils.SetConfigValue(key, scope, scopeObj, type, arg);

    protected override ESteamNetworkingGetConfigValueResult GetConfigValue(ESteamNetworkingConfigValue key,
        ESteamNetworkingConfigScope scope, IntPtr scopeObj, out ESteamNetworkingConfigDataType type,
        IntPtr result, ref ulong size)
        => SteamGameServerNetworkingUtils.GetConfigValue(key, scope, scopeObj, out type, result, ref size);

    protected override EResult GetConnectionRealTimeStatus(HSteamNetConnection conn,
        ref SteamNetConnectionRealTimeStatus_t status, int lanes,
        ref SteamNetConnectionRealTimeLaneStatus_t laneStatus)
        => SteamGameServerNetworkingSockets.GetConnectionRealTimeStatus(conn, ref status, lanes, ref laneStatus);

    protected override HSteamNetConnection ConnectP2P(ref SteamNetworkingIdentity identity, int virtualPort,
        int options, SteamNetworkingConfigValue_t[] optionValues)
        => SteamGameServerNetworkingSockets.ConnectP2P(ref identity, virtualPort, options, optionValues);

    protected override HSteamListenSocket CreateListenSocketP2P(int virtualPort, int options,
        SteamNetworkingConfigValue_t[] optionValues)
        => SteamGameServerNetworkingSockets.CreateListenSocketP2P(virtualPort, options, optionValues);

    protected override bool CloseListenSocket(HSteamListenSocket socket)
        => SteamGameServerNetworkingSockets.CloseListenSocket(socket);

    protected override EResult AcceptConnectionRaw(HSteamNetConnection conn)
        => SteamGameServerNetworkingSockets.AcceptConnection(conn);

    protected override bool CloseConnectionRaw(HSteamNetConnection conn, int reason, string debug, bool enableLinger)
        => SteamGameServerNetworkingSockets.CloseConnection(conn, reason, debug, enableLinger);

    protected override EResult SendMessageToConnection(HSteamNetConnection conn, IntPtr data, uint size,
        int flags, out long messageNumber)
        => SteamGameServerNetworkingSockets.SendMessageToConnection(conn, data, size, flags, out messageNumber);

    protected override int ReceiveMessagesOnConnection(HSteamNetConnection conn, IntPtr[] messages, int maxMessages)
        => SteamGameServerNetworkingSockets.ReceiveMessagesOnConnection(conn, messages, maxMessages);
}
