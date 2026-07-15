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
    void DrainPending(float dt);

    /// <summary>[Game thread] The local player committed deployment; recorded so a joiner catch-up can
    /// re-tell it the deployer finished (the one-shot broadcast predates the join).</summary>
    void MarkLocalDeploymentFinished();
}

/// <inheritdoc cref="ISiegeEngineDeploymentReplicator"/>
public class SiegeEngineDeploymentReplicator : ISiegeEngineDeploymentReplicator
{
    private static readonly ILogger Logger = LogManager.GetLogger<SiegeEngineDeploymentReplicator>();

    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly IBattleSession session;

    // Every placement transition, in wire order. Deployment/Disband mutates vanilla's ordered
    // _undeployedWeapons list, so collapsing this to the latest value per point can bind a different
    // same-type campaign weapon (and therefore different HP/index) on a late loader or joiner. The
    // deployment phase is bounded, so retain its complete transition timeline. Recorded on every
    // client so a successor promoted after host migration can still catch joiners up. Game-thread only.
    private readonly List<KeyValuePair<int, string>> placements = new List<KeyValuePair<int, string>>();

    // Transitions that arrived before their DeploymentPoint registered (catch-up during a peer's scene load).
    // This is also a complete FIFO: later points must not overtake an earlier blocked transition, because the
    // global Deploy/Disband order is what preserves vanilla's _undeployedWeapons identity mapping.
    private readonly List<KeyValuePair<int, string>> pending = new List<KeyValuePair<int, string>>();

    // Non-deployers skip the vanilla Start Battle teardown (see SiegeDeploymentPatches) and instead
    // sweep undeployed weapons once the deployer announces its own deployment finished — its placements
    // ride the same ReliableOrdered channel, so they all precede the announcement. Held while pending
    // placements wait for their point to load. Game-thread only.
    private bool sweepRequested;
    private bool sweepDone;
    private bool localDeploymentFinished;
    // How long placements have sat unappliable while the mission runs; past the deadline they are dropped
    // (with an error naming them) so one never-registering point can't hold the sweep off forever.
    private float pendingStallSeconds;
    private const float PendingStallDeadlineSeconds = 15f;

    public SiegeEngineDeploymentReplicator(IBattleNetwork network, IMessageBroker messageBroker, IBattleSession session)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.session = session;

        messageBroker.Subscribe<SiegeEnginePlacementChanged>(Handle_PlacementChanged);
        messageBroker.Subscribe<NetworkSiegeEnginePlacement>(Handle_NetworkPlacement);
        messageBroker.Subscribe<NetworkBattleDeploymentFinished>(Handle_NetworkDeploymentFinished);
        messageBroker.Subscribe<BattleHostMigrated>(Handle_BattleHostMigrated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SiegeEnginePlacementChanged>(Handle_PlacementChanged);
        messageBroker.Unsubscribe<NetworkSiegeEnginePlacement>(Handle_NetworkPlacement);
        messageBroker.Unsubscribe<NetworkBattleDeploymentFinished>(Handle_NetworkDeploymentFinished);
        messageBroker.Unsubscribe<BattleHostMigrated>(Handle_BattleHostMigrated);
        pending.Clear();
        // SuppressCapture is toggled around this replicator's own applies, so it resets here; the
        // authority flags are owned by CoopBattleController (which sets them each tick) and reset there.
        SiegeMissionAuthorityGate.SuppressCapture = false;
    }

    private void Handle_PlacementChanged(MessagePayload<SiegeEnginePlacementChanged> payload)
    {
        if (!session.IsLocalHost) return;

        BroadcastPlacement(payload.What.Point.Id.Id, payload.What.WeaponTypeName);
    }

    // [Host] Record one placement transition into the authoritative history and announce it, stamped
    // with our hosting generation (BR-102) so receivers can drop it if we turn out to be deposed.
    private void BroadcastPlacement(int pointId, string weaponTypeName)
    {
        Record(pointId, weaponTypeName);
        network.SendAll(new NetworkSiegeEnginePlacement(pointId, weaponTypeName, session.HostEpoch));
    }

    private void Record(int pointId, string weaponTypeName)
    {
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

            // BR-102: a deposed host's in-flight placement must not corrupt vanilla's ordered
            // undeployed-weapon mapping NOR enter the history below (it would be replayed to joiners).
            if (DropStaleHostEpoch(obj.HostEpoch, nameof(NetworkSiegeEnginePlacement))) return;

            // Record at receipt, not successful apply: a client promoted while its own scene is still loading
            // must nevertheless retain the complete authoritative history for later joiners.
            Record(obj.PointId, obj.WeaponTypeName);

            // Always enqueue first. If an older transition is still waiting for its point, applying this one
            // directly would overtake it and change vanilla's ordered undeployed-weapon mapping.
            StashPending(obj.PointId, obj.WeaponTypeName);
            DrainPending(0f);
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
        pending.Add(new KeyValuePair<int, string>(pointId, weaponTypeName));
    }

    public void MarkLocalDeploymentFinished()
    {
        localDeploymentFinished = true;
    }

    public void DrainPending(float dt)
    {
        if (Mission.Current == null) return;

        int pendingBeforeDrain = pending.Count;
        DrainPendingInOrder(TryApplyPlacement);
        if (pending.Count < pendingBeforeDrain)
            pendingStallSeconds = 0f;

        // A placement still buffered while the mission runs means its DeploymentPoint never registered
        // here; drop it after the deadline instead of holding the sweep off for the whole battle.
        if (pending.Count > 0 && Mission.Current.CurrentState == Mission.State.Continuing)
        {
            pendingStallSeconds += dt;
            if (pendingStallSeconds >= PendingStallDeadlineSeconds)
            {
                DropStalledHeadsAndDrain(TryApplyPlacement);
                pendingStallSeconds = 0f;
            }
        }
        else
        {
            pendingStallSeconds = 0f;
        }

        // The sweep must also wait for the mission to be running: pre-AfterStart there is no PlayerTeam
        // and the weapon lists are empty, so an early sweep would silently do nothing and be lost.
        if (sweepRequested && !sweepDone && pending.Count == 0
            && Mission.Current.CurrentState == Mission.State.Continuing)
        {
            sweepDone = SweepUndeployedWeapons();
        }
    }

    // BR-102: drop a host-authority message stamped by an earlier hosting generation (a deposed host
    // in flight across a migration). Unstamped (0) senders, an unassigned (0) receiver, and epochs at
    // or ahead of our assignment are accepted — see HostEpochPolicy for the convergence rationale.
    private bool DropStaleHostEpoch(int messageEpoch, string messageName)
    {
        int localEpoch = session.HostEpoch;
        if (!HostEpochPolicy.IsStale(messageEpoch, localEpoch)) return false;

        Logger.Information("[BattleSync] Dropped {Message} stamped with stale host epoch {Stale} (current {Current})",
            messageName, messageEpoch, localEpoch);
        return true;
    }

    // Apply one FIFO prefix only. A blocked transition is a global ordering barrier: even if a later point is
    // already registered, letting it pass can reorder same-type MissionSiegeWeapon identities across clients.
    // Kept as a separate pure queue operation so the ordering invariant has focused unit coverage.
    private void DrainPendingInOrder(Func<int, string, bool> tryApply)
    {
        int appliedCount = 0;
        while (appliedCount < pending.Count)
        {
            var transition = pending[appliedCount];
            if (!tryApply(transition.Key, transition.Value)) break;
            appliedCount++;
        }

        if (appliedCount > 0)
            pending.RemoveRange(0, appliedCount);
    }

    // Once the ordering barrier has timed out, discard only transitions that still cannot resolve. After each
    // discarded head, immediately retry the new head: valid later transitions keep their relative order and are
    // applied instead of being lost merely because an unrelated scene point never registered on this client.
    private void DropStalledHeadsAndDrain(Func<int, string, bool> tryApply)
    {
        while (pending.Count > 0)
        {
            var transition = pending[0];
            if (tryApply(transition.Key, transition.Value))
            {
                pending.RemoveAt(0);
                continue;
            }

            Logger.Error("[BattleSync] Dropping siege engine placement for point {PointId} ({Type}): its deployment point never registered",
                transition.Key, transition.Value);
            pending.RemoveAt(0);
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

            // Vanilla's own inverse of the teardown's SetDisabledAndMakeInvisible: activate, un-disable,
            // visibility and physics back on, tick requirements refreshed down the entity tree.
            weapon.SetEnabledAndMakeVisible();
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
            DrainPending(0f);
        });
    }

    // A successor can finish deployment while it is still a peer. Its completion is deliberately ignored above
    // because it was not authoritative then, and its local vanilla teardown was suppressed. If the old host leaves
    // before finishing, replay that already-completed transition now that this client is authoritative: sweep our
    // own undeployed machines and re-announce completion so every remaining peer does the same.
    private void Handle_BattleHostMigrated(MessagePayload<BattleHostMigrated> payload)
    {
        GameThread.RunSafe(() =>
        {
            TryReplayFinishedDeploymentAfterMigration(
                payload.What.MapEventId,
                session.InstanceId,
                localDeploymentFinished,
                session.IsLocalHost,
                Mission.Current?.IsSiegeBattle == true,
                requestSweep: () =>
                {
                    sweepRequested = true;
                    DrainPending(0f);
                },
                rebroadcastCompletion: () =>
                    network.SendAll(new NetworkBattleDeploymentFinished(session.OwnControllerId)));
        });
    }

    // Pure decision/effect seam: migration arrives off the game thread, while the native sweep itself needs the
    // live mission. Keeping the gate here gives direct coverage that an already-finished promoted successor does
    // both required actions exactly once: local teardown and authoritative completion replay.
    private static bool TryReplayFinishedDeploymentAfterMigration(
        string migratedMapEventId,
        string localMapEventId,
        bool localDeploymentFinished,
        bool isLocalHost,
        bool isSiegeBattle,
        Action requestSweep,
        Action rebroadcastCompletion)
    {
        if (migratedMapEventId != localMapEventId
            || !localDeploymentFinished
            || !isLocalHost
            || !isSiegeBattle)
            return false;

        requestSweep();
        rebroadcastCompletion();
        return true;
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
            // BR-102: the replay asserts deployment authority NOW, so it carries the CURRENT epoch —
            // not the epoch each transition was minted under — or a joiner holding a newer assignment
            // than a promoted successor's original one would drop the whole replay.
            int hostEpoch = session.HostEpoch;
            foreach (var placement in placements)
            {
                network.Send(controllerId, new NetworkSiegeEnginePlacement(placement.Key, placement.Value, hostEpoch));
            }

            if (placements.Count > 0)
                Logger.Information("[BattleSync] Replayed {Count} siege engine placement(s) to joining {Controller}", placements.Count, controllerId);

            // The deployment-finished broadcast is one-shot, so a client joining after it never sweeps its
            // undeployed engines (or re-latches its siege tactic); re-tell the joiner, after the placements
            // above so the set it sweeps against is final.
            if (localDeploymentFinished)
            {
                network.Send(controllerId, new NetworkBattleDeploymentFinished(session.OwnControllerId));
            }
        });
    }
}
