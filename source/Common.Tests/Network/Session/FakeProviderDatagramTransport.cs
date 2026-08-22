using Common.Network.Session;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Common.Tests.Network.Session;

internal sealed class FakeProviderDatagramTransport : IProviderDatagramTransport
{
    internal readonly struct SentDatagram
    {
        public SentDatagram(long connection, byte[] data, bool droppable)
        {
            Connection = connection;
            Data = data;
            Droppable = droppable;
        }

        public long Connection { get; }
        public byte[] Data { get; }
        public bool Droppable { get; }
    }

    private readonly object gate = new object();
    private readonly Dictionary<long, Queue<byte[]>> received = new Dictionary<long, Queue<byte[]>>();
    private readonly Dictionary<long, PlatformIdentity> remoteIdentities =
        new Dictionary<long, PlatformIdentity>();
    private readonly List<SentDatagram> sent = new List<SentDatagram>();
    private readonly List<long> accepted = new List<long>();
    private readonly List<long> closed = new List<long>();
    private long nextConnection;

    public PlatformIdentity LocalIdentity { get; set; } = new PlatformIdentity("gog", "999");
    public int PrepareCalls { get; private set; }
    public int? ListeningChannel { get; private set; }
    public PlatformIdentity ConnectedIdentity { get; private set; }
    public int ConnectedChannel { get; private set; }
    public int FailSendsRemaining { get; set; }
    public int RejectedSends { get; private set; }
    public bool Disposed { get; private set; }
    public long NextConnection => nextConnection;

    public SentDatagram[] SentDatagrams
    {
        get
        {
            lock (gate) return sent.ToArray();
        }
    }

    public long[] AcceptedConnections
    {
        get
        {
            lock (gate) return accepted.ToArray();
        }
    }

    public long[] ClosedConnections
    {
        get
        {
            lock (gate) return closed.ToArray();
        }
    }

    public event Action<long, ProviderConnectionState> ConnectionStateChanged;

    public void Prepare() => PrepareCalls++;

    public long Connect(PlatformIdentity remoteIdentity, int channel)
    {
        ConnectedIdentity = remoteIdentity;
        ConnectedChannel = channel;
        long connection = ++nextConnection;
        SetRemoteIdentity(connection, remoteIdentity);
        return connection;
    }

    public void Listen(int channel) => ListeningChannel = channel;
    public void StopListening() => ListeningChannel = null;

    public void Accept(long connection)
    {
        lock (gate) accepted.Add(connection);
    }

    public void Close(long connection)
    {
        lock (gate) closed.Add(connection);
    }

    public bool TryGetRemoteIdentity(long connection, out PlatformIdentity identity)
    {
        lock (gate) return remoteIdentities.TryGetValue(connection, out identity);
    }

    public bool Send(long connection, byte[] data, int length, bool droppable)
    {
        lock (gate)
        {
            if (FailSendsRemaining > 0)
            {
                FailSendsRemaining--;
                RejectedSends++;
                return droppable;
            }

            sent.Add(new SentDatagram(
                connection,
                data.Take(length).ToArray(),
                droppable));
            return true;
        }
    }

    public int Receive(long connection, byte[] buffer)
    {
        lock (gate)
        {
            if (!received.TryGetValue(connection, out var queue) || queue.Count == 0) return 0;

            byte[] data = queue.Dequeue();
            Array.Copy(data, buffer, data.Length);
            return data.Length;
        }
    }

    public string Describe(long connection) => "fake:" + connection;

    public void SetRemoteIdentity(long connection, PlatformIdentity identity)
    {
        lock (gate) remoteIdentities[connection] = identity;
    }

    public void EnqueueReceive(long connection, byte[] data)
    {
        lock (gate)
        {
            if (!received.TryGetValue(connection, out var queue))
            {
                queue = new Queue<byte[]>();
                received.Add(connection, queue);
            }
            queue.Enqueue(data);
        }
    }

    public void RaiseConnectionState(long connection, ProviderConnectionState state) =>
        ConnectionStateChanged?.Invoke(connection, state);

    public void Dispose() => Disposed = true;
}
