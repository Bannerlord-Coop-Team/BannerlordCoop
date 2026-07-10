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
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Missions;

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

    // Non-deployers skip the vanilla Start Battle teardown (see SiegeDeploymentPatches) and instead
    // sweep undeployed weapons once the deployer announces its own deployment finished — its placements
    // ride the same ReliableOrdered channel, so they all precede the announcement. Held while pending
    // placements wait for their point to load. Game-thread only.
    private bool sweepRequested;
    private bool sweepDone;

    public SiegeEngineDeploymentReplicator(IBattleNetwork network, IMessageBroker messageBroker, IBattleSession session)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.session = session;

        messageBroker.Subscribe<SiegeEnginePlacementChanged>(Handle_PlacementChanged);
        messageBroker.Subscribe<NetworkSiegeEnginePlacement>(Handle_NetworkPlacement);
        messageBroker.Subscribe<NetworkBattleDeploymentFinished>(Handle_NetworkDeploymentFinished);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SiegeEnginePlacementChanged>(Handle_PlacementChanged);
        messageBroker.Unsubscribe<NetworkSiegeEnginePlacement>(Handle_NetworkPlacement);
        messageBroker.Unsubscribe<NetworkBattleDeploymentFinished>(Handle_NetworkDeploymentFinished);
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
    // while the mission or the DeploymentPoint has not loaded yet, so the caller buffers and retries.
    private bool TryApplyPlacement(int pointId, string weaponTypeName)
    {
        // Pre-AfterStart the deployment points exist but their weapon lists are still empty (vanilla
        // fills them in DeploymentPoint.AfterMissionStart), so an early apply would drop the placement
        // as "no deployable weapon"; buffer until the mission is running.
        if (Mission.Current.CurrentState != Mission.State.Continuing) return false;

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
                    // The weapon was swept as undeployed before this placement landed; the deployable
                    // list filters on !IsDisabled, so restore the swept weapon instead of dropping the
                    // placement forever.
                    weaponType = RestoreSweptWeapon(point, weaponTypeName);
                }

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
        if (Mission.Current == null) return;

        for (int i = pending.Count - 1; i >= 0; i--)
        {
            if (TryApplyPlacement(pending[i].Key, pending[i].Value))
            {
                pending.RemoveAt(i);
            }
        }

        // The sweep must also wait for the mission to be running: pre-AfterStart there is no PlayerTeam
        // and the weapon lists are empty, so an early sweep would silently do nothing and be lost.
        if (sweepRequested && !sweepDone && pending.Count == 0
            && Mission.Current.CurrentState == Mission.State.Continuing)
        {
            sweepDone = SweepUndeployedWeapons();
        }
    }

    // Un-disable and re-activate a weapon the vanilla teardown swept (SetDisabledSynched — invisible,
    // IsDisabled, physics off, out of ActiveMissionObjects, tick withdrawn), so a placement that raced
    // the sweep still deploys instead of leaving the machine dead on this client only.
    private static System.Type RestoreSweptWeapon(DeploymentPoint point, string weaponTypeName)
    {
        foreach (var candidate in point._weapons)
        {
            if (!(candidate is SiegeWeapon weapon)) continue;
            var type = MissionSiegeWeaponsController.GetWeaponType(weapon);
            if (type?.Name != weaponTypeName) continue;

            weapon.IsDisabled = false;
            Mission.Current.ActivateMissionObject(weapon);
            weapon.GameEntity.SetVisibilityExcludeParents(true);
            weapon.GameEntity.SetPhysicsState(true, false);
            weapon.SetScriptComponentToTick(weapon.GetTickRequirement());
            Logger.Information("[BattleSync] Restored swept weapon {Type} at point {PointId}", weaponTypeName, point.Id.Id);
            return type;
        }

        return null;
    }

    // The deployer finished deploying: every placement it will ever send precedes this announcement on
    // the ReliableOrdered channel, so the set is final — run the teardown vanilla would have run at our
    // own Start Battle (deferred by SiegeDeploymentPatches so it can't race the placements).
    private void Handle_NetworkDeploymentFinished(MessagePayload<NetworkBattleDeploymentFinished> payload)
    {
        if (!session.IsHostController(payload.What.ControllerId)) return;

        GameThread.RunSafe(() =>
        {
            if (session.IsLocalHost) return;
            if (Mission.Current == null || !Mission.Current.IsSiegeBattle) return;

            sweepRequested = true;
            DrainPending();
        });
    }

    // Mirrors the vanilla teardown for BOTH sides (RemoveDeploymentPoints for the player side and
    // the one DeployAllSiegeWeaponsOfAi runs for the AI side — both blocked on non-deployers),
    // applied under suppress so the disables don't re-capture. Returns false while the teams
    // aren't up yet.
    private bool SweepUndeployedWeapons()
    {
        var playerTeam = Mission.Current.PlayerTeam;
        if (playerTeam == null) return false;

        SiegeMissionAuthorityGate.SuppressCapture = true;
        try
        {
            int swept = 0;
            foreach (var missionObject in Mission.Current.ActiveMissionObjects.ToArray())
            {
                if (!(missionObject is DeploymentPoint point)) continue;

                foreach (var weapon in point.DeployableWeapons.ToArray())
                {
                    if ((point.DeployedWeapon == null || !weapon.GameEntity.IsVisibleIncludeParents()) && weapon is SiegeWeapon siegeWeapon)
                    {
                        siegeWeapon.SetDisabledSynched();
                        swept++;
                    }
                }

                point.SetDisabledSynched();
            }

            Logger.Information("[BattleSync] Swept {Count} undeployed siege weapon(s) after the deployer finished", swept);
        }
        finally
        {
            SiegeMissionAuthorityGate.SuppressCapture = false;
        }

        return true;
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
