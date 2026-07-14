using Coop.Steam;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Coop.Tests.Steam
{
    /// <summary>
    /// Scriptable <see cref="ISteamTunnelTransport"/>: records calls, queues inbound
    /// datagrams, and lets tests raise connection-state events. The pumps poll from a
    /// background thread, so all state is lock-protected.
    /// </summary>
    public class FakeSteamTunnelTransport : ISteamTunnelTransport, ISteamTunnelConnectionIdentityResolver
    {
        private readonly object gate = new object();
        private readonly List<(uint Connection, byte[] Data)> sentDatagrams = new List<(uint, byte[])>();
        private readonly Dictionary<uint, Queue<byte[]>> pendingReceives = new Dictionary<uint, Queue<byte[]>>();
        private readonly Dictionary<uint, ulong> remoteSteamIds = new Dictionary<uint, ulong>();

        public uint NextConnection = 501;
        public ulong ConnectedHost;
        public bool Listening;
        public bool Disposed;
        public int RelayAccessCalls;
        public int FailSendsRemaining;
        public int RejectedSends;
        public readonly List<uint> AcceptedConnections = new List<uint>();
        public readonly List<uint> ClosedConnections = new List<uint>();

        public event Action<uint, TunnelConnectionState> ConnectionStateChanged;

        public void RaiseConnectionState(uint connection, TunnelConnectionState state) =>
            ConnectionStateChanged?.Invoke(connection, state);

        public (uint Connection, byte[] Data)[] SentDatagrams
        {
            get
            {
                lock (gate)
                {
                    return sentDatagrams.ToArray();
                }
            }
        }

        public void EnqueueReceive(uint connection, byte[] data)
        {
            lock (gate)
            {
                if (!pendingReceives.TryGetValue(connection, out var queue))
                {
                    queue = new Queue<byte[]>();
                    pendingReceives[connection] = queue;
                }

                queue.Enqueue(data);
            }
        }

        public void SetRemoteSteamId(uint connection, ulong steamId)
        {
            lock (gate)
            {
                if (steamId == 0)
                {
                    remoteSteamIds.Remove(connection);
                }
                else
                {
                    remoteSteamIds[connection] = steamId;
                }
            }
        }

        public void EnsureRelayAccess() => RelayAccessCalls++;

        public uint ConnectToHost(ulong hostSteamId, int virtualPort)
        {
            lock (gate)
            {
                ConnectedHost = hostSteamId;
                remoteSteamIds[NextConnection] = hostSteamId;
                return NextConnection;
            }
        }

        public void ListenForClients(int virtualPort) => Listening = true;

        public void StopListening() => Listening = false;

        public void AcceptConnection(uint connection) => AcceptedConnections.Add(connection);

        public void CloseConnection(uint connection)
        {
            lock (gate)
            {
                remoteSteamIds.Remove(connection);
                ClosedConnections.Add(connection);
            }
        }

        public bool TryGetRemoteSteamId(uint connection, out ulong steamId)
        {
            lock (gate)
            {
                return remoteSteamIds.TryGetValue(connection, out steamId);
            }
        }

        public bool SendDatagram(uint connection, byte[] data, int length, bool droppable)
        {
            lock (gate)
            {
                if (FailSendsRemaining > 0)
                {
                    FailSendsRemaining--;
                    RejectedSends++;
                    // Mirrors the real transport: a droppable datagram is lost, not retried.
                    return droppable;
                }

                sentDatagrams.Add((connection, data.Take(length).ToArray()));
            }

            return true;
        }

        public int ReceiveDatagram(uint connection, byte[] buffer)
        {
            lock (gate)
            {
                if (!pendingReceives.TryGetValue(connection, out var queue) || queue.Count == 0) return 0;

                var data = queue.Dequeue();
                Array.Copy(data, buffer, data.Length);
                return data.Length;
            }
        }

        public string DescribeConnection(uint connection) => "fake";

        public void Dispose()
        {
            lock (gate)
            {
                remoteSteamIds.Clear();
                Disposed = true;
            }
        }
    }
}
