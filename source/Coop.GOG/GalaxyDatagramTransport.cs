using Common.Network.Session;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Coop.GOG;

/// <summary>Maps Galaxy's connectionless P2P datagrams onto shared tunnel connections.</summary>
internal sealed class GalaxyDatagramTransport : IProviderDatagramTransport
{
    private sealed class Connection
    {
        public long Id;
        public ulong RemoteUserId;
        public byte SendChannel;
        public byte ReceiveChannel;
        public bool Accepted;
        public GalaxyFragmentReassembler Reassembler = new GalaxyFragmentReassembler();
        public Queue<byte[]> Datagrams = new Queue<byte[]>();
    }

    private readonly IGalaxySdk sdk;
    private readonly object gate = new object();
    private readonly Dictionary<long, Connection> connections = new Dictionary<long, Connection>();
    private long nextConnection;
    private uint nextMessageId;
    private int? listeningChannel;
    private bool disposed;

    public GalaxyDatagramTransport(IGalaxySdk sdk)
    {
        if (sdk == null) throw new ArgumentNullException(nameof(sdk));

        this.sdk = sdk;
        sdk.PacketReceived += HandlePacketReceived;
    }

    public PlatformIdentity LocalIdentity => GalaxyIdentity(sdk.LocalUserId);

    public event Action<long, ProviderConnectionState> ConnectionStateChanged;

    public void Prepare()
    {
        if (!LocalIdentity.IsValid)
            throw new InvalidOperationException("Galaxy has no authenticated local identity");
    }

    public long Connect(PlatformIdentity remoteIdentity, int channel)
    {
        ulong remoteUserId = ParseGalaxyIdentity(remoteIdentity);
        byte sendChannel = ToRequestChannel(channel);
        byte receiveChannel = checked((byte)(sendChannel + 1));

        lock (gate)
        {
            ThrowIfDisposed();
            foreach (var connection in connections.Values)
            {
                if (connection.RemoteUserId == remoteUserId &&
                    connection.SendChannel == sendChannel &&
                    connection.ReceiveChannel == receiveChannel)
                    return connection.Id;
            }

            var created = new Connection
            {
                Id = ++nextConnection,
                RemoteUserId = remoteUserId,
                SendChannel = sendChannel,
                ReceiveChannel = receiveChannel,
                Accepted = true,
            };
            connections.Add(created.Id, created);
            return created.Id;
        }
    }

    public void Listen(int channel)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            listeningChannel = ToRequestChannel(channel);
        }
    }

    public void StopListening()
    {
        lock (gate)
        {
            listeningChannel = null;
        }
    }

    public void Accept(long connection)
    {
        bool connected = false;
        lock (gate)
        {
            if (connections.TryGetValue(connection, out var tracked) && !tracked.Accepted)
            {
                tracked.Accepted = true;
                connected = true;
            }
        }

        if (connected)
            ConnectionStateChanged?.Invoke(connection, ProviderConnectionState.Connected);
    }

    public void Close(long connection)
    {
        Connection removed;
        uint messageId;
        lock (gate)
        {
            if (!connections.TryGetValue(connection, out removed)) return;
            connections.Remove(connection);
            messageId = ++nextMessageId;
        }

        sdk.SendP2P(
            removed.RemoteUserId,
            removed.SendChannel,
            GalaxyFragmentCodec.EncodeClose(messageId),
            GalaxyP2PSendMode.Reliable);
    }

    public bool TryGetRemoteIdentity(long connection, out PlatformIdentity identity)
    {
        lock (gate)
        {
            if (connections.TryGetValue(connection, out var tracked))
            {
                identity = GalaxyIdentity(tracked.RemoteUserId);
                return identity.IsValid;
            }
        }

        identity = default;
        return false;
    }

    public bool Send(long connection, byte[] data, int length, bool droppable)
    {
        Connection tracked;
        uint messageId;
        lock (gate)
        {
            if (!connections.TryGetValue(connection, out tracked)) return true;
            messageId = ++nextMessageId;
        }

        var sendMode = droppable ? GalaxyP2PSendMode.Unreliable : GalaxyP2PSendMode.Reliable;
        foreach (byte[] fragment in GalaxyFragmentCodec.Encode(messageId, data, length))
        {
            if (!sdk.SendP2P(tracked.RemoteUserId, tracked.SendChannel, fragment, sendMode))
                return droppable;
        }

        return true;
    }

    public int Receive(long connection, byte[] buffer)
    {
        lock (gate)
        {
            if (!connections.TryGetValue(connection, out var tracked) ||
                tracked.Datagrams.Count == 0)
            {
                return 0;
            }

            byte[] datagram = tracked.Datagrams.Dequeue();
            if (datagram.Length > buffer.Length) return 0;

            Array.Copy(datagram, buffer, datagram.Length);
            return datagram.Length;
        }
    }

    public string Describe(long connection)
    {
        lock (gate)
        {
            return connections.TryGetValue(connection, out var tracked)
                ? sdk.GetConnectionType(tracked.RemoteUserId)
                : "closed";
        }
    }

    private void HandlePacketReceived(ulong remoteUserId, byte channel, byte[] packet)
    {
        if (!GalaxyFragmentCodec.TryDecode(packet, out var fragment)) return;

        Connection tracked = null;
        bool connecting = false;
        bool closed = false;
        lock (gate)
        {
            if (disposed) return;

            foreach (var candidate in connections.Values)
            {
                if (candidate.RemoteUserId == remoteUserId && candidate.ReceiveChannel == channel)
                {
                    tracked = candidate;
                    break;
                }
            }

            if (tracked == null)
            {
                if (listeningChannel != channel || fragment.Close) return;

                tracked = new Connection
                {
                    Id = ++nextConnection,
                    RemoteUserId = remoteUserId,
                    SendChannel = checked((byte)(channel + 1)),
                    ReceiveChannel = channel,
                };
                connections.Add(tracked.Id, tracked);
                connecting = true;
            }

            if (fragment.Close)
            {
                connections.Remove(tracked.Id);
                closed = true;
            }
        }

        if (connecting)
            ConnectionStateChanged?.Invoke(tracked.Id, ProviderConnectionState.Connecting);
        if (closed)
        {
            ConnectionStateChanged?.Invoke(tracked.Id, ProviderConnectionState.Closed);
            return;
        }

        lock (gate)
        {
            if (!connections.TryGetValue(tracked.Id, out var current) || !current.Accepted)
                return;
            if (current.Reassembler.TryAdd(fragment, out byte[] datagram))
                current.Datagrams.Enqueue(datagram);
        }
    }

    public void Dispose()
    {
        long[] activeConnections;
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            activeConnections = new long[connections.Count];
            connections.Keys.CopyTo(activeConnections, 0);
        }

        foreach (long connection in activeConnections) Close(connection);
        sdk.PacketReceived -= HandlePacketReceived;
    }

    private void ThrowIfDisposed()
    {
        if (disposed) throw new ObjectDisposedException(nameof(GalaxyDatagramTransport));
    }

    private static ulong ParseGalaxyIdentity(PlatformIdentity identity)
    {
        if (!string.Equals(identity.Provider, GalaxySessionProvider.ProviderId, StringComparison.Ordinal) ||
            !ulong.TryParse(
                identity.UserId,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out ulong galaxyId) ||
            galaxyId == 0)
        {
            throw new ArgumentException("The tunnel target is not a valid GOG identity", nameof(identity));
        }

        return galaxyId;
    }

    private static byte ToRequestChannel(int channel)
    {
        if (channel < 0 || channel > byte.MaxValue / 2)
            throw new ArgumentOutOfRangeException(nameof(channel));

        return checked((byte)(channel * 2));
    }

    internal static PlatformIdentity GalaxyIdentity(ulong galaxyId) => galaxyId == 0
        ? default
        : new PlatformIdentity(
            GalaxySessionProvider.ProviderId,
            galaxyId.ToString(CultureInfo.InvariantCulture));
}
