using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using Missions.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Replicates the mission host's siege engine placement to every other client's mission. The host is
/// the single engine deployer (its auto-deploys and UI edits are authoritative for both sides); peers
/// apply the same Deploy/Disband on their scene-matched deployment points, and a joiner gets the whole
/// current placement replayed.
/// </summary>
public interface ISiegeEngineDeploymentReplicator : IDisposable
{
    /// <summary>[Host] Replay every placement so far to a joining controller.</summary>
    void CatchUpJoiner(string controllerId);

    /// <summary>[Game thread] Retry placements that arrived before their deployment point had loaded.</summary>
    void DrainPending();
}

/// <inheritdoc cref="ISiegeEngineDeploymentReplicator"/>
public class SiegeEngineDeploymentReplicator : ISiegeEngineDeploymentReplicator
{
    private static readonly ILogger Logger = LogManager.GetLogger<SiegeEngineDeploymentReplicator>();

    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly IBattleSession session;

    // The current placement per point, in first-touch order so a replay applies in the original order
    // and the receivers' HP-by-type mapping matches. Recorded on every client (not just the deployer)
    // so a successor promoted after host migration can still catch joiners up. Game-thread only.
    private readonly List<KeyValuePair<int, string>> placements = new List<KeyValuePair<int, string>>();

    // Placements that arrived before their DeploymentPoint registered (catch-up during a peer's scene load);
    // re-applied by DrainPending once the point appears. Per-point, latest wins; cleared with the replicator.
    private readonly List<KeyValuePair<int, string>> pending = new List<KeyValuePair<int, string>>();

    public SiegeEngineDeploymentReplicator(IBattleNetwork network, IMessageBroker messageBroker, IBattleSession session)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.session = session;

        messageBroker.Subscribe<SiegeEnginePlacementChanged>(Handle_PlacementChanged);
        messageBroker.Subscribe<NetworkSiegeEnginePlacement>(Handle_NetworkPlacement);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SiegeEnginePlacementChanged>(Handle_PlacementChanged);
        messageBroker.Unsubscribe<NetworkSiegeEnginePlacement>(Handle_NetworkPlacement);
        pending.Clear();
        // SuppressCapture is toggled around this replicator's own applies, so it resets here; the
        // authority flags are owned by CoopBattleController (which sets them each tick) and reset there.
        SiegeMissionAuthorityGate.SuppressCapture = false;
    }

    private void Handle_PlacementChanged(MessagePayload<SiegeEnginePlacementChanged> payload)
    {
        if (!session.IsLocalHost) return;

        int pointId = payload.What.Point.Id.Id;
        Record(pointId, payload.What.WeaponTypeName);
        network.SendAll(new NetworkSiegeEnginePlacement(pointId, payload.What.WeaponTypeName));
    }

    private void Record(int pointId, string weaponTypeName)
    {
        for (int i = 0; i < placements.Count; i++)
        {
            if (placements[i].Key == pointId)
            {
                placements[i] = new KeyValuePair<int, string>(pointId, weaponTypeName);
                return;
            }
        }

        placements.Add(new KeyValuePair<int, string>(pointId, weaponTypeName));
    }

    private void Handle_NetworkPlacement(MessagePayload<NetworkSiegeEnginePlacement> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null)
            {
                Logger.Warning("[BattleSync] Dropping siege engine placement for point {PointId}: no mission", obj.PointId);
                return;
            }

            if (!TryApplyPlacement(obj.PointId, obj.WeaponTypeName))
            {
                // The DeploymentPoint isn't registered yet (catch-up during scene load); buffer and retry from
                // the controller tick once it appears, instead of dropping it permanently.
                StashPending(obj.PointId, obj.WeaponTypeName);
            }
        });
    }

    // Returns true once the point is resolved (whether or not the weapon type turned out valid); false only
    // while the DeploymentPoint has not loaded yet, so the caller buffers and retries.
    private bool TryApplyPlacement(int pointId, string weaponTypeName)
    {
        var point = Mission.Current.MissionObjects
            .OfType<DeploymentPoint>()
            .FirstOrDefault(candidate => candidate.Id.Id == pointId);
        if (point == null) return false;

        Record(pointId, weaponTypeName);

        SiegeMissionAuthorityGate.SuppressCapture = true;
        try
        {
            if (string.IsNullOrEmpty(weaponTypeName))
            {
                if (point.DeployedWeapon != null) point.Disband();
            }
            else
            {
                var weaponType = point.DeployableWeaponTypes.FirstOrDefault(type => type.Name == weaponTypeName);
                if (weaponType == null)
                {
                    Logger.Error("[BattleSync] Point {PointId} has no deployable weapon of type {Type}", pointId, weaponTypeName);
                    return true;
                }

                if (point.DeployedWeapon != null) point.Disband();
                point.Deploy(weaponType);
            }
        }
        finally
        {
            SiegeMissionAuthorityGate.SuppressCapture = false;
        }

        return true;
    }

    private void StashPending(int pointId, string weaponTypeName)
    {
        for (int i = 0; i < pending.Count; i++)
        {
            if (pending[i].Key == pointId)
            {
                pending[i] = new KeyValuePair<int, string>(pointId, weaponTypeName);
                return;
            }
        }

        pending.Add(new KeyValuePair<int, string>(pointId, weaponTypeName));
    }

    public void DrainPending()
    {
        if (pending.Count == 0 || Mission.Current == null) return;

        for (int i = pending.Count - 1; i >= 0; i--)
        {
            if (TryApplyPlacement(pending[i].Key, pending[i].Value))
            {
                pending.RemoveAt(i);
            }
        }
    }

    public void CatchUpJoiner(string controllerId)
    {
        if (!session.IsLocalHost) return;

        GameThread.RunSafe(() =>
        {
            foreach (var placement in placements)
            {
                network.Send(controllerId, new NetworkSiegeEnginePlacement(placement.Key, placement.Value));
            }

            if (placements.Count > 0)
                Logger.Information("[BattleSync] Replayed {Count} siege engine placement(s) to joining {Controller}", placements.Count, controllerId);
        });
    }
}
