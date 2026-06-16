using Common.Logging;
using Common.Network.Data;
using LiteNetLib;
using Serilog;
using System.Collections.Generic;
using System.Net;

namespace Coop.Core.Server.Services.Instances;

/// <summary>
/// Co-hosted NAT-punch rendezvous for P2P mission instances (taverns etc.). Instance ids are derived
/// client-side from (settlement, location), so co-located clients independently arrive at the same id.
/// The server simply introduces every peer that punches into a given instance to the others, creating
/// the instance on the first punch — there is no server-issued assignment.
/// </summary>
public interface IMissionManager
{
    /// <summary>
    /// Handle a NAT-introduction request: introduce the requesting peer to every other peer already
    /// punched into the same instance. Driven purely by the request's <see cref="ConnectionToken"/>,
    /// whose instance name is the client-derived instance id.
    /// </summary>
    void HandleIntroductionRequest(NatPunchModule natPunchModule, IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token);
}

/// <inheritdoc cref="IMissionManager"/>
public class MissionManager : IMissionManager
{
    private static readonly ILogger Logger = LogManager.GetLogger<MissionManager>();

    private readonly object gate = new object();
    private readonly Dictionary<string, MissionInstance> byInstanceId = new Dictionary<string, MissionInstance>();

    public void HandleIntroductionRequest(
        NatPunchModule natPunchModule, IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
    {
        if (ConnectionToken.TryParse(token, out var connectionToken) == false)
        {
            Logger.Warning("Discarding NAT introduction with unparseable token from {Endpoint}", remoteEndPoint);
            return;
        }

        string instanceId = connectionToken.InstanceId;

        lock (gate)
        {
            // Instance ids are derived client-side from (settlement, location), so co-located clients
            // independently arrive at the same id. The first punch for an id creates the instance; the
            // rest are introduced into it. No separate server-assignment round-trip is needed.
            if (byInstanceId.TryGetValue(instanceId, out var instance) == false)
            {
                instance = new MissionInstance(instanceId);
                byInstanceId[instanceId] = instance;
                Logger.Information("Created instance {Instance} on first NAT punch from {Endpoint}",
                    instanceId, remoteEndPoint);
            }

            // A punch = (re)entering now. Drop any earlier slot for this endpoint first, else a re-joiner
            // (same endpoint, since the socket persists) is mistaken for a duplicate and never reconnected.
            RemoveEndpointEverywhere(remoteEndPoint);

            foreach (var existing in instance.PunchEndpoints)
            {
                Logger.Information("Introducing {Newcomer} <-> {Existing} for instance {Instance}",
                    remoteEndPoint, existing.External, instanceId);

                natPunchModule.NatIntroduce(
                    existing.Internal, existing.External, // host side
                    localEndPoint, remoteEndPoint,        // newcomer side
                    token);
            }

            instance.PunchEndpoints.Add(new MissionInstance.Endpoints(localEndPoint, remoteEndPoint));
        }
    }

    // A peer is in at most one instance, so any prior listing for this endpoint is stale on a new punch.
    private void RemoveEndpointEverywhere(IPEndPoint external)
    {
        foreach (var instance in byInstanceId.Values)
        {
            instance.PunchEndpoints.RemoveAll(e => e.External.Equals(external));
        }
    }
}
