using Missions.Battles;
using NavalDLC.Missions.Objects;
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
/// live on each hull's owner; other clients end with the host's replicated result. Retreat and ship capture stay off.
/// </summary>
public class CoopNavalBattleEndLogic : NavalBattleEndLogic
{
    private readonly IBattleSession session;
    private readonly IBattleDeploymentCoordinator deployment;
    private readonly IBattleResultCommitter resultCommitter;

    private bool endChecksReleased;

    public CoopNavalBattleEndLogic(IBattleSession session, IBattleDeploymentCoordinator deployment, IBattleResultCommitter resultCommitter)
    {
        this.session = session;
        this.deployment = deployment;
        this.resultCommitter = resultCommitter;
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
    internal bool IsSideDepletedHere(BattleSideEnum side)
    {
        var tally = Tally(side, LiveHumansByFormation());
        return IsSideDepleted(tally.LiveHumans, tally.Reserve);
    }

    /// <summary>[Game thread] Coop replacement for vanilla's per-hull out-of-action check.</summary>
    internal bool AreAnySideHullsOutOfAction(BattleSideEnum playerSide, BattleSideEnum enemySide,
        out bool playerHullsOutOfAction, out bool enemyHullsOutOfAction)
    {
        var crewByFormation = LiveHumansByFormation();
        var player = Tally(playerSide, crewByFormation);
        var enemy = Tally(enemySide, crewByFormation);
        playerHullsOutOfAction = IsSideOutOfAction(player.Hulls, player.HullsOutOfAction);
        enemyHullsOutOfAction = IsSideOutOfAction(enemy.Hulls, enemy.HullsOutOfAction);
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

    /// <summary>Vanilla's spawn-logic rule: no live troops and no spawnable reserve.</summary>
    internal static bool IsSideDepleted(int liveHumans, int reserve) => liveHumans + reserve == 0;

    /// <summary>Vanilla's per-hull rule: sunk, or at most three troops aboard and in reserve.</summary>
    internal static bool IsHullOutOfAction(bool isSunk, int liveCrew, int reserve) =>
        isSunk || liveCrew + reserve <= MinTroopCountForOutOfActionCheck;

    /// <summary>A side is out of action only when it has hulls and every one of them is.</summary>
    internal static bool IsSideOutOfAction(int hulls, int hullsOutOfAction) => hulls > 0 && hullsOutOfAction == hulls;

    /// <summary>Only reserve on a hull this client simulates and that has not sunk can still field, so only it keeps a side alive.</summary>
    internal static bool CountsReserve(bool simulatedHere, bool isSunk) => simulatedHere && !isSunk;

    internal static string DescribeResult(MissionResult result)
    {
        if (result?.PlayerVictory == true) return "victory";
        if (result?.PlayerDefeated == true) return "defeat";
        return "none";
    }

    // Retreat is disabled in coop naval, and vanilla reads a side with no hull left as one that retreated.
    private void ClearRetreat()
    {
        IsEnemySideRetreating = false;
        _isPlayerSideRetreating = false;
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

        return CoopBattleController.ShouldReleaseEndConditionHold(deployment.IsActivated, attackerFielded, defenderFielded,
            attackerMissingReserveAccepted: false, defenderMissingReserveAccepted: false);
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

    private SideTally Tally(BattleSideEnum side, Dictionary<Formation, int> crewByFormation)
    {
        var tally = new SideTally();
        foreach (var team in Mission.Teams)
        {
            if (team.Side == side) tally.LiveHumans += team.ActiveAgents.Count(agent => agent.IsHuman);
        }

        foreach (MissionShip ship in _navalShipsLogic.AllShips)
        {
            if (ship.Team == null || ship.Team.Side != side) continue;

            int crew = ship.Formation != null && crewByFormation.TryGetValue(ship.Formation, out int count) ? count : 0;
            int reserve = CountsReserve(!NavalForeignHulls.Contains(ship), ship.IsSunk) ? SpawnableReserveOf(ship) : 0;
            tally.Hulls++;
            if (!ship.IsSunk) tally.HullsAfloat++;
            if (IsHullOutOfAction(ship.IsSunk, crew, reserve)) tally.HullsOutOfAction++;
            tally.Reserve += reserve;
        }

        return tally;
    }

    private int SpawnableReserveOf(MissionShip ship)
    {
        int active = _navalAgentsLogic.GetActiveAgentsOfShip(ship)?.Count ?? 0;
        return Math.Max(0, _navalAgentsLogic.GetTotalTroopCountOfShip(ship, spawnableReservesOnly: true) - active);
    }

    private struct SideTally
    {
        public int LiveHumans;
        public int Hulls;
        public int HullsAfloat;
        public int HullsOutOfAction;
        public int Reserve;
    }
}
