using Common;
using Common.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;

namespace Common.Network.Session;

/// <summary>Composes provider tunnels into one mission listener and pairwise outgoing links.</summary>
public sealed class ProviderMissionPeerTransport : IMissionPeerTransport
{
    private static readonly ILogger Logger = LogManager.GetLogger<ProviderMissionPeerTransport>();

    private readonly object gate = new object();
    private readonly object lifecycleGate = new object();
    private readonly IProviderDatagramTransport hostTransport;
    private readonly ProviderTunnelHost host;
    private readonly Func<IProviderDatagramTransport> clientTransportFactory;
    private readonly Action<Action> retiredCleanupScheduler;
    private readonly Dictionary<PlatformIdentity, ProviderTunnelClient> clients =
        new Dictionary<PlatformIdentity, ProviderTunnelClient>();
    private readonly List<ProviderTunnelClient> retiredClients = new List<ProviderTunnelClient>();
    private bool started;
    private bool disposed;

    public ProviderMissionPeerTransport(
        IProviderDatagramTransport hostTransport,
        Func<IProviderDatagramTransport> clientTransportFactory)
        : this(hostTransport, clientTransportFactory, ScheduleRetiredCleanup)
    {
    }

    internal ProviderMissionPeerTransport(
        IProviderDatagramTransport hostTransport,
        Func<IProviderDatagramTransport> clientTransportFactory,
        Action<Action> retiredCleanupScheduler)
    {
        if (hostTransport == null) throw new ArgumentNullException(nameof(hostTransport));
        if (clientTransportFactory == null) throw new ArgumentNullException(nameof(clientTransportFactory));
        if (retiredCleanupScheduler == null) throw new ArgumentNullException(nameof(retiredCleanupScheduler));

        this.hostTransport = hostTransport;
        this.clientTransportFactory = clientTransportFactory;
        this.retiredCleanupScheduler = retiredCleanupScheduler;
        host = new ProviderTunnelHost(hostTransport, ProviderTunnel.MissionChannel);
        host.PeerDisconnected += HandlePeerDisconnected;
    }

    public PlatformIdentity LocalIdentity => hostTransport.LocalIdentity;

    public event Action<PlatformIdentity> PeerDisconnected;

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
                host.Start(missionPort);
                lock (gate)
                {
                    started = true;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Provider mission listener unavailable; mission traffic will use the server relay");
            }
        }
    }

    public bool TryConnect(PlatformIdentity remoteIdentity, out IPEndPoint endpoint)
    {
        endpoint = null;
        if (MissionPeerRoles.Resolve(LocalIdentity, remoteIdentity) != MissionPeerRole.Connect)
            return false;

        lock (lifecycleGate)
        {
            DisposeRetiredClients();

            ProviderTunnelClient client;
            lock (gate)
            {
                if (disposed || !started) return false;
                if (clients.TryGetValue(remoteIdentity, out client))
                {
                    endpoint = new IPEndPoint(IPAddress.Loopback, client.LocalPort);
                    return true;
                }
            }

            client = new ProviderTunnelClient(clientTransportFactory());
            client.Closed += () => HandleClientClosed(remoteIdentity, client);

            lock (gate)
            {
                if (disposed || !started)
                {
                    client.Dispose();
                    return false;
                }

                clients.Add(remoteIdentity, client);
            }

            try
            {
                client.Start(remoteIdentity, ProviderTunnel.MissionChannel);
            }
            catch (Exception ex)
            {
                lock (gate)
                {
                    if (clients.TryGetValue(remoteIdentity, out var activeClient) &&
                        ReferenceEquals(activeClient, client))
                    {
                        clients.Remove(remoteIdentity);
                    }
                    retiredClients.Remove(client);
                }

                client.Dispose();
                Logger.Warning(ex,
                    "Provider mission connection to {RemoteIdentity} unavailable; using the server relay",
                    remoteIdentity);
                return false;
            }

            lock (gate)
            {
                if (!disposed && started &&
                    clients.TryGetValue(remoteIdentity, out var activeClient) &&
                    ReferenceEquals(activeClient, client))
                {
                    endpoint = new IPEndPoint(IPAddress.Loopback, client.LocalPort);
                    return true;
                }

                clients.Remove(remoteIdentity);
                retiredClients.Remove(client);
            }

            client.Dispose();
            return false;
        }
    }

    public void Disconnect(PlatformIdentity remoteIdentity)
    {
        lock (lifecycleGate)
        {
            DisposeRetiredClients();

            ProviderTunnelClient client = null;
            lock (gate)
            {
                if (disposed) return;
                if (clients.TryGetValue(remoteIdentity, out client)) clients.Remove(remoteIdentity);
            }

            client?.Dispose();
            host.ClosePeer(remoteIdentity);
        }
    }

    public bool TryGetRemoteIdentity(IPEndPoint endpoint, out PlatformIdentity remoteIdentity) =>
        host.TryGetIdentity(endpoint, out remoteIdentity);

    public void Stop()
    {
        lock (lifecycleGate)
        {
            StopCore();
        }
    }

    private void StopCore()
    {
        ProviderTunnelClient[] remaining;
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

    private void HandleClientClosed(PlatformIdentity remoteIdentity, ProviderTunnelClient client)
    {
        bool notify;
        lock (gate)
        {
            notify = !disposed &&
                clients.TryGetValue(remoteIdentity, out var activeClient) &&
                ReferenceEquals(activeClient, client);

            if (notify)
            {
                clients.Remove(remoteIdentity);
                retiredClients.Add(client);
            }
        }

        if (!notify) return;

        try
        {
            PeerDisconnected?.Invoke(remoteIdentity);
        }
        finally
        {
            retiredCleanupScheduler(() => DisposeRetiredClient(client));
        }
    }

    private static void ScheduleRetiredCleanup(Action cleanup)
    {
        ThreadPool.QueueUserWorkItem(_ => GameThread.RunSafe(
            cleanup, context: "DisposeClosedProviderMissionClient"));
    }

    private void HandlePeerDisconnected(PlatformIdentity remoteIdentity)
    {
        lock (gate)
        {
            if (disposed) return;
        }

        PeerDisconnected?.Invoke(remoteIdentity);
    }

    private void DisposeRetiredClients()
    {
        ProviderTunnelClient[] retired;
        lock (gate)
        {
            retired = retiredClients.ToArray();
            retiredClients.Clear();
        }

        foreach (var client in retired) client.Dispose();
    }

    private void DisposeRetiredClient(ProviderTunnelClient client)
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
