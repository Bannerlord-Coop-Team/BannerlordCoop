using Common.Network.Session;
using System;
using System.Globalization;

namespace Coop.Steam;

/// <summary>Adapts Steam Networking Sockets to the shared provider datagram contract.</summary>
internal sealed class SteamDatagramTransportAdapter : IProviderDatagramTransport
{
    private readonly ISteamTunnelTransport transport;
    private readonly Func<ulong> localSteamId;

    public SteamDatagramTransportAdapter(
        ISteamTunnelTransport transport,
        Func<ulong> localSteamId)
    {
        if (transport == null) throw new ArgumentNullException(nameof(transport));
        if (localSteamId == null) throw new ArgumentNullException(nameof(localSteamId));

        this.transport = transport;
        this.localSteamId = localSteamId;
        transport.ConnectionStateChanged += HandleConnectionStateChanged;
    }

    public PlatformIdentity LocalIdentity => SteamIdentity(localSteamId());

    public event Action<long, ProviderConnectionState> ConnectionStateChanged;

    public void Prepare() => transport.EnsureRelayAccess();

    public long Connect(PlatformIdentity remoteIdentity, int channel)
    {
        if (!string.Equals(remoteIdentity.Provider, SteamSessionProvider.ProviderId, StringComparison.Ordinal) ||
            !ulong.TryParse(
                remoteIdentity.UserId,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out ulong steamId) ||
            steamId == 0)
        {
            throw new ArgumentException("The tunnel target is not a valid Steam identity", nameof(remoteIdentity));
        }

        return transport.ConnectToHost(steamId, channel);
    }

    public void Listen(int channel) => transport.ListenForClients(channel);
    public void StopListening() => transport.StopListening();
    public void Accept(long connection) => transport.AcceptConnection(ToConnection(connection));
    public void Close(long connection) => transport.CloseConnection(ToConnection(connection));

    public bool TryGetRemoteIdentity(long connection, out PlatformIdentity identity)
    {
        identity = default;
        if (transport is not ISteamTunnelConnectionIdentityResolver resolver ||
            !resolver.TryGetRemoteSteamId(ToConnection(connection), out ulong steamId))
        {
            return false;
        }

        identity = SteamIdentity(steamId);
        return identity.IsValid;
    }

    public bool Send(long connection, byte[] data, int length, bool droppable) =>
        transport.SendDatagram(ToConnection(connection), data, length, droppable);

    public int Receive(long connection, byte[] buffer) =>
        transport.ReceiveDatagram(ToConnection(connection), buffer);

    public string Describe(long connection) => transport.DescribeConnection(ToConnection(connection));

    public void Dispose()
    {
        transport.ConnectionStateChanged -= HandleConnectionStateChanged;
        transport.Dispose();
    }

    private void HandleConnectionStateChanged(uint connection, TunnelConnectionState state)
    {
        ConnectionStateChanged?.Invoke(connection, state switch
        {
            TunnelConnectionState.Connecting => ProviderConnectionState.Connecting,
            TunnelConnectionState.Connected => ProviderConnectionState.Connected,
            TunnelConnectionState.Closed => ProviderConnectionState.Closed,
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        });
    }

    private static uint ToConnection(long connection) => checked((uint)connection);

    internal static PlatformIdentity SteamIdentity(ulong steamId) => steamId == 0
        ? default
        : new PlatformIdentity(
            SteamSessionProvider.ProviderId,
            steamId.ToString(CultureInfo.InvariantCulture));
}
