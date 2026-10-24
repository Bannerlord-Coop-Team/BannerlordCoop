using Common.Logging;
using GameInterface.Services.Players;
using Missions.Battles;
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
/// Vanilla <see cref="NavalBattleEndLogic"/> where only the battle host detects the end, over replicated hull state; other
/// clients end with the host's result. A side retreats only once its players' hulls left with them and none is left.
/// </summary>
public class CoopNavalBattleEndLogic : NavalBattleEndLogic
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopNavalBattleEndLogic>();

    private readonly IBattleSession session;
    private readonly IBattleDeploymentCoordinator deployment;
    private readonly IBattleResultCommitter resultCommitter;
    private readonly INavalNpcFleetSpawner npcFleet;
    private readonly IBattleShipReplicator shipReplicator;
    private readonly INavalSideTallyReader tallyReader;
    private readonly IPlayerManager playerManager;
    private readonly NavalPlayerHeroGrace heroGrace = new NavalPlayerHeroGrace();

    private bool endChecksReleased;

    // Takes the battle's session, deployment, committer and AI fleet, the replicator's departed hulls and the side tally.
    public CoopNavalBattleEndLogic(IBattleSession session, IBattleDeploymentCoordinator deployment, IBattleResultCommitter resultCommitter,
        INavalNpcFleetSpawner npcFleet, IBattleShipReplicator shipReplicator, INavalSideTallyReader tallyReader,
        IPlayerManager playerManager)
    {
        this.session = session;
        this.deployment = deployment;
        this.resultCommitter = resultCommitter;
        this.npcFleet = npcFleet;
        this.shipReplicator = shipReplicator;
        this.tallyReader = tallyReader;
        this.playerManager = playerManager;
    }

    // Holds vanilla's end checks at deployment finish; the tick releases them.
    public override void OnDeploymentFinished()
    {
        base.OnDeploymentFinished();
        CanCheckForEndCondition = false;
    }

    // Lets only the battle host run vanilla's end checks, once both sides fielded, and cancels retreats coop doesn't keep.
    public override void OnMissionTick(float dt)
    {
        if (Mission.IsDeploymentFinished)
        {
            // One-shot hold, like the land controller's: a side whose troops arrive later must not read as depleted. Every
            // client latches it, so a successor that takes over the end checks after a host change starts released.
            if (!endChecksReleased)
                endChecksReleased = BothSidesFielded();

            CanCheckForEndCondition = session.IsLocalHost && endChecksReleased;
        }

        base.OnMissionTick(dt);
        ClearRetreat();
    }

    // Ends the mission with the resolved result where there is one; a non-host never concludes by itself.
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
    internal bool IsSideDepletedHere(BattleSideEnum side) => Tally(side, NavalSideTallyReader.NoCrew).IsDepleted;

    /// <summary>
    /// [Game thread] Coop replacement for vanilla's per-hull out-of-action check; a side's out-of-action hulls defeat it only
    /// once its player heroes' grace is over.
    /// </summary>
    internal bool AreAnySideHullsOutOfAction(BattleSideEnum playerSide, BattleSideEnum enemySide,
        out bool playerHullsOutOfAction, out bool enemyHullsOutOfAction)
    {
        var crewByHull = tallyReader.CrewByHull(Mission.Agents);
        bool playerOutOfAction = Tally(playerSide, crewByHull).IsOutOfAction;
        bool enemyOutOfAction = Tally(enemySide, crewByHull).IsOutOfAction;

        // Vanilla graces only the local main agent's side, so in PvP the other player's side lost while its hero fought.
        // The result stays raw, so vanilla's contested-island timing runs as it does for its own graced side.
        ObserveLivePlayerHeroes();
        float now = Mission.CurrentTime;
        playerHullsOutOfAction = playerOutOfAction && heroGrace.HasExpired(playerSide, now);
        enemyHullsOutOfAction = enemyOutOfAction && heroGrace.HasExpired(enemySide, now);
        return playerOutOfAction || enemyOutOfAction;
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
        var crewByHull = tallyReader.CrewByHull(Mission.Agents);
        var result = Mission.MissionResult;
        return new
        {
            isHost = session.IsLocalHost,
            checksReleased = endChecksReleased,
            checking = CanCheckForEndCondition,
            sides = new[] { BattleSideEnum.Attacker, BattleSideEnum.Defender }.Select(side =>
            {
                var tally = Tally(side, crewByHull);
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

    // Names the mission result for the inspect dump and the DEBUG win/lose commands.
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

    // Cancels vanilla's retreat of a side that coop doesn't count as retreated.
    private void ClearRetreat()
    {
        var playerSide = Mission.PlayerTeam?.Side ?? BattleSideEnum.None;
        var enemySide = playerSide == BattleSideEnum.None ? BattleSideEnum.None : playerSide.GetOppositeSide();
        if (!IsRetreatedSide(enemySide)) IsEnemySideRetreating = false;
        if (!IsRetreatedSide(playerSide)) _isPlayerSideRetreating = false;
    }

    // Whether hulls left the side with their owners and none of its hulls is left here.
    private bool IsRetreatedSide(BattleSideEnum side)
    {
        if (side != BattleSideEnum.Attacker && side != BattleSideEnum.Defender) return false;

        int hullsLeft = _navalShipsLogic.AllShips.Count(ship => ship.Team != null && ship.Team.Side == side);
        return KeepsRetreat(shipReplicator.DepartedHulls(side), hullsLeft);
    }

    // The land hold's release rule, where a side whose AI fleet failed to field counts like a land side whose reserve was
    // abandoned: it may start empty, so its depletion ends the battle instead of the hold keeping it open forever.
    private bool BothSidesFielded()
    {
        CoopBattleController.SidesFielded(Mission.Teams, out bool attackerFielded, out bool defenderFielded);
        var failedFleetSide = npcFleet.FailedSide;
        bool released = CoopBattleController.ShouldReleaseEndConditionHold(deployment.IsActivated, attackerFielded, defenderFielded,
            failedFleetSide == BattleSideEnum.Attacker, failedFleetSide == BattleSideEnum.Defender);
        if (released && failedFleetSide != BattleSideEnum.None)
            Logger.Warning("[NavalBattle] End checks released for {MapEventId} although the {Side} AI fleet failed to field",
                session.InstanceId, failedFleetSide);
        return released;
    }

    // Records each side with a live player hero here; a player's hero is the agent of its registered character.
    private void ObserveLivePlayerHeroes()
    {
        var heroCharacterIds = new HashSet<string>(playerManager.Players.Select(player => player.CharacterObjectId));
        float now = Mission.CurrentTime;
        foreach (var agent in Mission.Agents)
        {
            if (!agent.IsHuman || !agent.IsActive() || agent.Team == null || agent.Character == null) continue;

            if (heroCharacterIds.Contains(agent.Character.StringId)) heroGrace.ObserveLiveHero(agent.Team.Side, now);
        }
    }

    // One side's tally over this client's hulls, with every live human of its teams, swimmers included.
    private NavalSideTally Tally(BattleSideEnum side, IReadOnlyDictionary<MissionObject, int> crewByHull)
    {
        int liveHumans = 0;
        foreach (var team in Mission.Teams)
        {
            if (team.Side == side) liveHumans += team.ActiveAgents.Count(agent => agent.IsHuman);
        }

        return tallyReader.Count(side, liveHumans, crewByHull);
    }
}
