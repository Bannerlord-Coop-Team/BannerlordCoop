using Common;
using Common.Logging;
using Common.Network.Session;
using Serilog;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;

namespace Coop.Steam;

/// <summary>
/// Composes the existing Steam tunnel pumps into one mission listener and one outgoing pump per
/// lower-ID peer, exposing only loopback UDP endpoints to the Missions assembly.
/// </summary>
public class SteamMissionBridge : ISteamMissionBridge
{
    private static readonly ILogger Logger = LogManager.GetLogger<SteamMissionBridge>();
    private const int MissionVirtualPort = 1;

    private readonly object gate = new object();
    private readonly object lifecycleGate = new object();
    private readonly ISteamTunnelTransport hostTransport;
    private readonly SteamTunnelHost host;
    private readonly Func<ISteamTunnelTransport> clientTransportFactory;
    private readonly Action<Action> retiredCleanupScheduler;
    private readonly Dictionary<ulong, SteamTunnelClient> clients = new Dictionary<ulong, SteamTunnelClient>();
    private readonly List<SteamTunnelClient> retiredClients = new List<SteamTunnelClient>();
    private bool started;
    private bool disposed;

    public SteamMissionBridge() : this(
        SteamUser.GetSteamID().m_SteamID,
        new SteamNetworkingTunnelTransport(),
        () => new SteamNetworkingTunnelTransport(),
        ScheduleRetiredCleanup)
    {
    }

    internal SteamMissionBridge(
        ulong localSteamId,
        ISteamTunnelTransport hostTransport,
        Func<ISteamTunnelTransport> clientTransportFactory,
        Action<Action> retiredCleanupScheduler)
    {
        LocalSteamId = localSteamId;
        this.hostTransport = hostTransport ?? throw new ArgumentNullException(nameof(hostTransport));
        this.clientTransportFactory = clientTransportFactory
            ?? throw new ArgumentNullException(nameof(clientTransportFactory));
        this.retiredCleanupScheduler = retiredCleanupScheduler
            ?? throw new ArgumentNullException(nameof(retiredCleanupScheduler));
        host = new SteamTunnelHost(hostTransport);
        host.PeerDisconnected += HandlePeerDisconnected;
    }

    public ulong LocalSteamId { get; }

    public event Action<ulong> PeerDisconnected;

    public void Start(int missionPort)
    {
        lock (lifecycleGate)
        {
            lock (gate)
            {
                if (disposed || started) return;
            }

            try
            {
                host.Start(missionPort, MissionVirtualPort);
                lock (gate)
                {
                    started = true;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Steam mission listener unavailable; mission traffic will use the server relay");
            }
        }
    }

    public bool TryConnect(ulong remoteSteamId, out IPEndPoint endpoint)
    {
        endpoint = null;
        if (SteamMissionPeerRoles.Resolve(LocalSteamId, remoteSteamId) != SteamMissionPeerRole.Connect)
            return false;

        lock (lifecycleGate)
        {
            DisposeRetiredClients();

            SteamTunnelClient client;
            lock (gate)
            {
                if (disposed || !started) return false;
                if (clients.TryGetValue(remoteSteamId, out client))
                {
                    endpoint = new IPEndPoint(IPAddress.Loopback, client.LocalPort);
                    return true;
                }
            }

            client = new SteamTunnelClient(clientTransportFactory());
            client.Closed += () => HandleClientClosed(remoteSteamId, client);

            bool cannotStart;
            lock (gate)
            {
                cannotStart = disposed || !started;
                if (!cannotStart)
                {
                    clients.Add(remoteSteamId, client);
                }
            }

            if (cannotStart)
            {
                client.Dispose();
                return false;
            }

            try
            {
                client.Start(remoteSteamId, MissionVirtualPort);
            }
            catch (Exception ex)
            {
                lock (gate)
                {
                    if (clients.TryGetValue(remoteSteamId, out var activeClient)
                        && ReferenceEquals(activeClient, client))
                    {
                        clients.Remove(remoteSteamId);
                    }
                    retiredClients.Remove(client);
                }

                client.Dispose();
                Logger.Warning(ex,
                    "Steam mission connection to {RemoteSteamId} unavailable; using the server relay",
                    remoteSteamId.ToString());
                return false;
            }

            lock (gate)
            {
                if (!disposed && started
                    && clients.TryGetValue(remoteSteamId, out var activeClient)
                    && ReferenceEquals(activeClient, client))
                {
                    endpoint = new IPEndPoint(IPAddress.Loopback, client.LocalPort);
                    return true;
                }

                clients.Remove(remoteSteamId);
                retiredClients.Remove(client);
            }

            client.Dispose();
            return false;
        }
    }

    public void Disconnect(ulong remoteSteamId)
    {
        lock (lifecycleGate)
        {
            DisposeRetiredClients();

            SteamTunnelClient client = null;
            lock (gate)
            {
                if (disposed) return;

                if (clients.TryGetValue(remoteSteamId, out client))
                {
                    clients.Remove(remoteSteamId);
                }
            }

            client?.Dispose();
            host.ClosePeer(remoteSteamId);
        }
    }

    public bool TryGetRemoteSteamId(IPEndPoint endpoint, out ulong remoteSteamId)
        => host.TryGetRemoteSteamId(endpoint, out remoteSteamId);

    public void Stop()
    {
        lock (lifecycleGate)
        {
            StopCore();
        }
    }

    private void StopCore()
    {
        SteamTunnelClient[] remaining;
        lock (gate)
        {
            started = false;
            remaining = clients.Values.Concat(retiredClients).ToArray();
            clients.Clear();
            retiredClients.Clear();
        }

        foreach (var client in remaining) client.Dispose();
        host.Stop();
    }

    private void HandleClientClosed(ulong remoteSteamId, SteamTunnelClient client)
    {
        bool notify;
        lock (gate)
        {
            notify = !disposed
                && clients.TryGetValue(remoteSteamId, out var activeClient)
                && ReferenceEquals(activeClient, client);

            if (notify)
            {
                clients.Remove(remoteSteamId);
                retiredClients.Add(client);
            }
        }

        if (!notify) return;

        try
        {
            PeerDisconnected?.Invoke(remoteSteamId);
        }
        finally
        {
            retiredCleanupScheduler(() => DisposeRetiredClient(client));
        }
    }

    private static void ScheduleRetiredCleanup(Action cleanup)
    {
        ThreadPool.QueueUserWorkItem(_ => GameThread.RunSafe(
            cleanup, context: "DisposeClosedSteamMissionClient"));
    }

    private void HandlePeerDisconnected(ulong remoteSteamId)
    {
        lock (gate)
        {
            if (disposed) return;
        }

        PeerDisconnected?.Invoke(remoteSteamId);
    }

    private void DisposeRetiredClients()
    {
        SteamTunnelClient[] retired;
        lock (gate)
        {
            retired = retiredClients.ToArray();
            retiredClients.Clear();
        }

        foreach (var client in retired) client.Dispose();
    }

    private void DisposeRetiredClient(SteamTunnelClient client)
    {
        lock (gate)
        {
            if (!retiredClients.Remove(client)) return;
        }

        client.Dispose();
    }

    public void Dispose()
    {
        lock (lifecycleGate)
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
            }

            StopCore();
            host.PeerDisconnected -= HandlePeerDisconnected;
            host.Dispose();
            hostTransport.Dispose();
        }
    }
}
