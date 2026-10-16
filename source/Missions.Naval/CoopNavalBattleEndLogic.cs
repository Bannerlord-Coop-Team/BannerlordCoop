using Common.Logging;
using Missions.Battles;
using NavalDLC.Missions.Objects;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>How a coop naval battle ended for the local player.</summary>
internal enum NavalBattleOutcome
{
    None,
    PlayerVictory,
    PlayerDefeat,
}

/// <summary>
/// Vanilla <see cref="NavalBattleEndLogic"/> where only the battle host detects the end, since crew tracking and reserves
/// live on each hull's owner; other clients end with the host's replicated result. A side retreats only once its players'
/// hulls left with them and none is left; ship capture stays off.
/// </summary>
public class CoopNavalBattleEndLogic : NavalBattleEndLogic
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopNavalBattleEndLogic>();

    private readonly IBattleSession session;
    private readonly IBattleDeploymentCoordinator deployment;
    private readonly IBattleResultCommitter resultCommitter;
    private readonly INavalNpcFleetSpawner npcFleet;
    private readonly IBattleShipReplicator shipReplicator;
    private readonly INavalMigrationReserveFielder migrationReserve;

    private bool endChecksReleased;

    public CoopNavalBattleEndLogic(IBattleSession session, IBattleDeploymentCoordinator deployment, IBattleResultCommitter resultCommitter,
        INavalNpcFleetSpawner npcFleet, IBattleShipReplicator shipReplicator, INavalMigrationReserveFielder migrationReserve)
    {
        this.session = session;
        this.deployment = deployment;
        this.resultCommitter = resultCommitter;
        this.npcFleet = npcFleet;
        this.shipReplicator = shipReplicator;
        this.migrationReserve = migrationReserve;
    }

    public override void OnDeploymentFinished()
    {
        base.OnDeploymentFinished();
        CanCheckForEndCondition = false;
    }

    public override void OnMissionTick(float dt)
    {
        if (Mission.IsDeploymentFinished)
        {
            // One-shot hold, like the land controller's: a side whose troops arrive later must not read as depleted.
            if (!endChecksReleased && session.IsLocalHost)
                endChecksReleased = BothSidesFielded();

            CanCheckForEndCondition = ShouldCheckEnd(session.IsLocalHost, endChecksReleased);
        }

        base.OnMissionTick(dt);
        ClearRetreat();
    }

    public override bool MissionEnded(ref MissionResult missionResult)
    {
        ClearRetreat();

        bool hasResolvedState = resultCommitter.TryGetResolvedState(out var resolvedState);
        var outcome = ReplicatedOutcome(session.IsLocalHost, Mission.IsDeploymentFinished, hasResolvedState, resolvedState,
            Mission.PlayerTeam?.Side ?? BattleSideEnum.None);
        if (outcome.HasValue)
        {
            _isEnemySideDepleted = outcome.Value == NavalBattleOutcome.PlayerVictory;
            _isPlayerSideDepleted = outcome.Value == NavalBattleOutcome.PlayerDefeat;
        }

        return base.MissionEnded(ref missionResult);
    }

    /// <summary>[Game thread] Coop replacement for the spawn logic's side depletion.</summary>
    internal bool IsSideDepletedHere(BattleSideEnum side) => Tally(side, LiveHumansByFormation()).IsDepleted;

    /// <summary>[Game thread] Coop replacement for vanilla's per-hull out-of-action check.</summary>
    internal bool AreAnySideHullsOutOfAction(BattleSideEnum playerSide, BattleSideEnum enemySide,
        out bool playerHullsOutOfAction, out bool enemyHullsOutOfAction)
    {
        var crewByFormation = LiveHumansByFormation();
        var player = Tally(playerSide, crewByFormation);
        var enemy = Tally(enemySide, crewByFormation);
        playerHullsOutOfAction = player.IsOutOfAction;
        enemyHullsOutOfAction = enemy.IsOutOfAction;
        return playerHullsOutOfAction || enemyHullsOutOfAction;
    }

#if DEBUG
    /// <summary>[Game thread, battle host] Latches one side as depleted, so the next end check concludes the battle.</summary>
    internal void ForceOutcome(bool playerVictory)
    {
        if (playerVictory) _isEnemySideDepleted = true;
        else _isPlayerSideDepleted = true;
    }
#endif

    /// <summary>[Game thread] End state for coop.debug.naval.inspect.</summary>
    internal object Inspect()
    {
        var crewByFormation = LiveHumansByFormation();
        var result = Mission.MissionResult;
        return new
        {
            isHost = session.IsLocalHost,
            checksReleased = endChecksReleased,
            checking = CanCheckForEndCondition,
            sides = new[] { BattleSideEnum.Attacker, BattleSideEnum.Defender }.Select(side =>
            {
                var tally = Tally(side, crewByFormation);
                return new
                {
                    side = side.ToString(),
                    aliveAgents = tally.LiveHumans,
                    crewAboard = tally.CrewAboard,
                    offHullAgents = Math.Max(0, tally.LiveHumans - tally.CrewAboard),
                    hulls = tally.Hulls,
                    afloatHulls = tally.HullsAfloat,
                    hullsOutOfAction = tally.HullsOutOfAction,
                    reserve = tally.Reserve,
                };
            }).ToArray(),
            enemyDepleted = _isEnemySideDepleted,
            playerDepleted = _isPlayerSideDepleted,
            resolved = Mission.MissionEnded && result?.BattleResolved == true,
            result = DescribeResult(result),
            committed = resultCommitter.TryGetResolvedState(out var committedState) ? committedState.ToString() : null,
        };
    }

    /// <summary>
    /// The land hold's release rule, where a side whose AI fleet failed to field counts like a land side whose reserve was
    /// abandoned: it may start empty, so its depletion ends the battle instead of the hold keeping it open forever.
    /// </summary>
    internal static bool ShouldReleaseHold(bool deploymentActivated, bool attackerFielded, bool defenderFielded,
        BattleSideEnum failedFleetSide) =>
        CoopBattleController.ShouldReleaseEndConditionHold(deploymentActivated, attackerFielded, defenderFielded,
            failedFleetSide == BattleSideEnum.Attacker, failedFleetSide == BattleSideEnum.Defender);

    /// <summary>Whether this client runs the end checks: the battle host, once both sides have fielded troops.</summary>
    internal static bool ShouldCheckEnd(bool isLocalHost, bool endChecksReleased) => isLocalHost && endChecksReleased;

    /// <summary>
    /// The outcome this client's mission ends with: a resolved result once deployment is over, otherwise null on the host
    /// (its own detection decides) and none on any other client.
    /// </summary>
    internal static NavalBattleOutcome? ReplicatedOutcome(bool isLocalHost, bool deploymentFinished, bool hasResolvedState,
        BattleState resolvedState, BattleSideEnum playerSide)
    {
        if (hasResolvedState && deploymentFinished)
        {
            var outcome = OutcomeOf(resolvedState, playerSide);
            if (outcome != NavalBattleOutcome.None) return outcome;
        }

        return isLocalHost ? null : NavalBattleOutcome.None;
    }

    /// <summary>The local player's outcome for a shared battle result.</summary>
    internal static NavalBattleOutcome OutcomeOf(BattleState state, BattleSideEnum playerSide)
    {
        if (playerSide != BattleSideEnum.Attacker && playerSide != BattleSideEnum.Defender) return NavalBattleOutcome.None;

        if (state == BattleState.AttackerVictory)
            return playerSide == BattleSideEnum.Attacker ? NavalBattleOutcome.PlayerVictory : NavalBattleOutcome.PlayerDefeat;
        if (state == BattleState.DefenderVictory)
            return playerSide == BattleSideEnum.Defender ? NavalBattleOutcome.PlayerVictory : NavalBattleOutcome.PlayerDefeat;
        return NavalBattleOutcome.None;
    }

    internal static string DescribeResult(MissionResult result)
    {
        if (result?.PlayerVictory == true) return "victory";
        if (result?.PlayerDefeated == true) return "defeat";
        return "none";
    }

    /// <summary>
    /// Whether vanilla's retreat of a side stands: it reads any side with no hull left as retreated, which in coop also holds
    /// before a side's hulls spawn here; so it counts only once hulls left that side with their owner and none is left.
    /// </summary>
    internal static bool KeepsRetreat(int departedHulls, int hullsLeft) => departedHulls > 0 && hullsLeft == 0;

    private void ClearRetreat()
    {
        var playerSide = Mission.PlayerTeam?.Side ?? BattleSideEnum.None;
        var enemySide = playerSide == BattleSideEnum.None ? BattleSideEnum.None : playerSide.GetOppositeSide();
        if (!IsRetreatedSide(enemySide)) IsEnemySideRetreating = false;
        if (!IsRetreatedSide(playerSide)) _isPlayerSideRetreating = false;
    }

    private bool IsRetreatedSide(BattleSideEnum side)
    {
        if (side != BattleSideEnum.Attacker && side != BattleSideEnum.Defender) return false;

        int hullsLeft = _navalShipsLogic.AllShips.Count(ship => ship.Team != null && ship.Team.Side == side);
        return KeepsRetreat(shipReplicator.DepartedHulls(side), hullsLeft);
    }

    private bool BothSidesFielded()
    {
        bool attackerFielded = false;
        bool defenderFielded = false;
        foreach (var team in Mission.Teams)
        {
            if (!team.ActiveAgents.Any(agent => agent.IsHuman)) continue;
            if (team.Side == BattleSideEnum.Attacker) attackerFielded = true;
            else if (team.Side == BattleSideEnum.Defender) defenderFielded = true;
        }

        var failedFleetSide = npcFleet.FailedSide;
        bool released = ShouldReleaseHold(deployment.IsActivated, attackerFielded, defenderFielded, failedFleetSide);
        if (released && failedFleetSide != BattleSideEnum.None)
            Logger.Warning("[NavalBattle] End checks released for {MapEventId} although the {Side} AI fleet failed to field",
                session.InstanceId, failedFleetSide);
        return released;
    }

    // A copy's crew are puppets in the hull's formation, which the copy's NavalShipAgents never tracks.
    private Dictionary<Formation, int> LiveHumansByFormation()
    {
        var counts = new Dictionary<Formation, int>();
        foreach (var agent in Mission.Agents)
        {
            if (!agent.IsHuman || !agent.IsActive() || agent.Formation == null) continue;

            counts.TryGetValue(agent.Formation, out int count);
            counts[agent.Formation] = count + 1;
        }

        return counts;
    }

    private NavalSideTally Tally(BattleSideEnum side, Dictionary<Formation, int> crewByFormation)
    {
        int liveHumans = 0;
        foreach (var team in Mission.Teams)
        {
            if (team.Side == side) liveHumans += team.ActiveAgents.Count(agent => agent.IsHuman);
        }

        // Reserve a promoted host recovered still waits for a hull, yet keeps the side alive.
        return NavalSideTally.Count(side, liveHumans, HullCounts(crewByFormation), migrationReserve.PendingReserve(side));
    }

    private IEnumerable<NavalHullCount> HullCounts(Dictionary<Formation, int> crewByFormation)
    {
        foreach (MissionShip ship in _navalShipsLogic.AllShips)
        {
            if (ship.Team == null) continue;

            int crew = ship.Formation != null && crewByFormation.TryGetValue(ship.Formation, out int count) ? count : 0;
            yield return new NavalHullCount(ship.Team.Side, ship.IsSunk, crew, shipReplicator.HullReserve(ship));
        }
    }
}
