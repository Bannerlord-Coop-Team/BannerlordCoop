using Common.Logging;
using Common.Network.Data;
using LiteNetLib;
using Serilog;
using System.Collections.Generic;
using System.Net;

namespace Coop.Core.Server.Services.Instances;

/// <summary>
/// Server-side NAT-punch rendezvous, co-hosted in the Coop server so the authoritative server is
/// also the P2P matchmaker. Tracks the internal + external endpoints each peer presents per
/// instance and introduces every co-located pair so they can open a direct connection.
///
/// Driven purely by NAT-introduction requests: a peer does not need a separate registered
/// connection, only to send <c>SendNatIntroduceRequest</c> with a <see cref="ConnectionToken"/>
/// whose instance name is the server-issued instance id.
/// </summary>
internal class InstanceIntroducer
{
    private static readonly ILogger Logger = LogManager.GetLogger<InstanceIntroducer>();

    private readonly object gate = new object();
    private readonly Dictionary<string, List<Endpoints>> byInstance = new Dictionary<string, List<Endpoints>>();

    private readonly struct Endpoints
    {
        public readonly IPEndPoint Internal;
        public readonly IPEndPoint External;

        public Endpoints(IPEndPoint @internal, IPEndPoint external)
        {
            Internal = @internal;
            External = external;
        }
    }

    public void HandleIntroductionRequest(
        NatPunchModule natPunchModule, IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
    {
        if (ConnectionToken.TryParse(token, out var connectionToken) == false)
        {
            Logger.Warning("Discarding NAT introduction with unparseable token from {Endpoint}", remoteEndPoint);
            return;
        }

        string instance = connectionToken.InstanceName;

        lock (gate)
        {
            // A punch = (re)entering now. Drop any earlier slot for this endpoint first, else a re-joiner
            // (same endpoint, since the socket persists) is mistaken for a duplicate and never reconnected.
            RemoveEndpointEverywhere(remoteEndPoint);

            if (byInstance.TryGetValue(instance, out var peers) == false)
            {
                peers = new List<Endpoints>();
                byInstance[instance] = peers;
            }

            foreach (var existing in peers)
            {
                Logger.Information("Introducing {Newcomer} <-> {Existing} for instance {Instance}",
                    remoteEndPoint, existing.External, instance);

                natPunchModule.NatIntroduce(
                    existing.Internal, existing.External, // host side
                    localEndPoint, remoteEndPoint,        // newcomer side
                    token);
            }

            peers.Add(new Endpoints(localEndPoint, remoteEndPoint));
        }
    }

    // A peer is in at most one instance, so any prior listing for this endpoint is stale on a new punch.
    private void RemoveEndpointEverywhere(IPEndPoint external)
    {
        foreach (var peers in byInstance.Values)
        {
            peers.RemoveAll(e => e.External.Equals(external));
        }
    }

    /// <summary>Drop a retired instance's endpoints so they do not accumulate for the server's lifetime.</summary>
    public void ClearInstance(string instanceId)
    {
        lock (gate)
        {
            byInstance.Remove(instanceId);
        }
    }
}
