#if DEBUG
using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Missions;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party.Commands;
using GameInterface.Services.Rafts.Interfaces;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.MapEvents.NavalScenarios;

/// <summary>
/// [Game thread, server] DEBUG: stages a resolved naval scenario: tops up each player's ships and troops, sizes the AI
/// fleet, starts the naval map event and tells the clients the scenario, which run its automatic steps.
/// </summary>
public interface INavalScenarioStager
{
    /// <summary>Stages <paramref name="config"/>; on success its map event id is set and the output is a one-line summary.</summary>
    bool TryStage(NavalScenarioConfig config, out string output);
}

/// <inheritdoc cref="INavalScenarioStager"/>
public class NavalScenarioStager : INavalScenarioStager
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalScenarioStager>();

    // PartyCommands' add_troops template gives 11 troops.
    internal const int TroopsPerTemplate = 11;

    internal const float EngageWaitSeconds = 120f;
    private const int EngagePollMilliseconds = 500;
    // With no map event this long after staging, the server starts the encounter itself (R41b P4).
    internal const float EngageFallbackSeconds = 10f;

    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly INavalBattleStaging staging;
    private readonly INavalScenarioState state;
    private readonly INavalEngageResetTracer resetTracer;
    private readonly IPlayerRaftFleetInterface raftFleets;
    // Only the server registers mission membership; a client container resolves this command without it.
    private readonly IMissionMembershipRegistry missionMembership;

    public NavalScenarioStager(IObjectManager objectManager, INetwork network, IMessageBroker messageBroker,
        INavalBattleStaging staging, INavalScenarioState state, INavalEngageResetTracer resetTracer,
        IPlayerRaftFleetInterface raftFleets, IMissionMembershipRegistry missionMembership = null)
    {
        this.objectManager = objectManager;
        this.network = network;
        this.messageBroker = messageBroker;
        this.staging = staging;
        this.state = state;
        this.resetTracer = resetTracer;
        this.raftFleets = raftFleets;
        this.missionMembership = missionMembership;
    }

    public bool TryStage(NavalScenarioConfig config, out string output)
    {
        output = PlayersStillInBattle(config.ControllerIds);
        if (output != null)
        {
            Logger.Warning("[NavalScenario] {Output}", output);
            return false;
        }

        var players = new MobileParty[config.ControllerIds.Length];
        for (int i = 0; i < players.Length; i++)
        {
            if (!staging.TryGetReadyPlayerParty(config.ControllerIds[i], out players[i], out output))
                return false;
        }

        var hull = MBObjectManager.Instance.GetObject<ShipHull>(config.HullId);
        if (hull == null)
        {
            output = $"No ship hull has id {config.HullId}.";
            return false;
        }

        var log = new StringBuilder($"NAVAL_SCENARIO preset={config.Preset} scenario={config.ScenarioId}");

        // The fleet cached at a stranding comes back first, so the top-up counts it.
        LeaveRafts(config, players, log);
        foreach (var party in players)
        {
            TopUpShips(party.Party, config.ShipsPerPlayer, hull);
            TopUpTroops(party, config.TroopTemplatesPerPlayer);
            TopUpReserve(party, config.ReserveTroops);
        }
        HealHeroes(players, log);
        log.Append(" players=").Append(string.Join(",", config.ControllerIds.Zip(players,
            (id, party) => $"{id}(ships={party.Ships.Count},troops={party.MemberRoster.TotalManCount})")));
        if (config.ArmedShipsPerParty > 0)
            log.Append(" armedPlayers=").Append(string.Join(",", players.Select(party => ArmShips(party.Party, config.ArmedShipsPerParty))));
        if (config.RammedShipsPerParty > 0)
            log.Append(" rammedPlayers=").Append(string.Join(",", players.Select(party => FitRams(party.Party, config.RammedShipsPerParty))));

        MobileParty engagingEnemy = null;
        bool started;
        switch (config.Entry)
        {
            case NavalScenarioEntry.Duel:
                started = TryStartDuel(config, players, log, out output);
                break;
            case NavalScenarioEntry.Encounter:
                started = TryStageEncounter(config, players, hull, log, out engagingEnemy, out output);
                break;
            case NavalScenarioEntry.Blockade:
                started = TryStartBlockade(config, players, hull, log, out output);
                break;
            default:
                started = TryStartAgainstAiFleet(config, players, hull, log, out output);
                break;
        }
        if (!started)
        {
            output = log + " FAILED: " + output;
            Logger.Error("[NavalScenario] {Output}", output);
            RecordServerStepFailure(config, output);
            return false;
        }

        state.Start(config, localControllerId: null, isServer: true);
        if (config.Entry == NavalScenarioEntry.Blockade) state.Complete(NavalScenarioState.BlockadeStep, true, log.ToString());

        // The encounter's map event comes from the campaign AI later; the clients hear of the scenario once it exists, and
        // until then only answer the encounter conversation that comes first.
        if (engagingEnemy != null)
        {
            output = log.Append(" mapEvent=pending(engage)").ToString();
            Logger.Information("[NavalScenario] {Output}", output);
            resetTracer.Watch(engagingEnemy);
            network.SendAll(new NetworkNavalScenarioEngaging(config));
            WatchEngage(new EngageWait(config, engagingEnemy, players, DateTime.UtcNow));
            return true;
        }

        network.SendAll(new NetworkNavalScenarioStarted(config));

        output = log.Append(" mapEvent=").Append(config.MapEventId).ToString();
        Logger.Information("[NavalScenario] {Output}", output);
        return true;
    }

    /// <summary>The refusal while a listed player is still in a battle mission, else null: its client would take the new
    /// map event on top of the old encounter, never enter it, and throw in the encounter menu on leaving (R43).</summary>
    internal string PlayersStillInBattle(IEnumerable<string> controllerIds)
    {
        if (missionMembership == null) return null;

        var inMission = controllerIds.Where(missionMembership.IsControllerInMission).ToList();
        if (inMission.Count == 0) return null;

        return $"Not staged: {string.Join(",", inMission)} still in a battle mission. Leave it " +
            "(coop.debug.map_event.leave_battle) and finish the post-battle screens, then stage again.";
    }

    // Heals each player's hero, whose last mission exit may have reported it wounded (R43: 51/100), so its party isn't left out
    // of the battle as wounded; the synced HitPoints carry it to every client.
    private static void HealHeroes(MobileParty[] players, StringBuilder log)
    {
        var healed = new List<string>();
        foreach (var hero in players.Select(party => party.LeaderHero))
        {
            if (hero == null || hero.HitPoints >= hero.MaxHitPoints) continue;

            healed.Add($"{hero.StringId}({hero.HitPoints}->{hero.MaxHitPoints})");
            hero.HitPoints = hero.MaxHitPoints;
        }

        if (healed.Count > 0) log.Append(" healed=").Append(string.Join(",", healed));
    }

    // A failed encounter or blockade stays visible in scenario_status with the reason.
    private void RecordServerStepFailure(NavalScenarioConfig config, string output)
    {
        string step = config.Entry switch
        {
            NavalScenarioEntry.Encounter => NavalScenarioState.EngageStep,
            NavalScenarioEntry.Blockade => NavalScenarioState.BlockadeStep,
            _ => null,
        };
        if (step == null) return;

        state.Start(config, localControllerId: null, isServer: true);
        state.Complete(step, false, output);
        state.Advance(NavalScenarioPhase.Ended);
    }

    // A player released at sea sails on a raft until it lands, and PlayerRaftVisibilityPatches turns an engage order on a
    // player's raft into GoToPoint, which left the encounter preset's fleet idle (R41b P4). Ends the raft as landing does.
    private void LeaveRafts(NavalScenarioConfig config, MobileParty[] players, StringBuilder log)
    {
        var rafted = config.ControllerIds.Where((id, i) => players[i].IsInRaftState).ToList();
        if (rafted.Count == 0) return;

        foreach (var party in players.Where(party => party.IsInRaftState))
            LeaveRaft(party);
        log.Append(" leftRaft=").Append(string.Join(",", rafted));
    }

    // RaftStateChangeAction.DeactivateRaftStateForParty without the short-term behavior and AI rethink for a party its client
    // moves; EnableAi restores vanilla's non-raft value of the synced _isDisabled, which the raft's DisableAi set.
    private void LeaveRaft(MobileParty party)
    {
        party.IsInRaftState = false;
        party.Ai.EnableAi();
        CampaignEventDispatcher.Instance.OnMobilePartyRaftStateChanged(party);
        raftFleets.ReturnFleet(party);
        // ReturnFleet restores the coast anchor cached at the stranding, but a party at sea sails with its fleet, unanchored.
        party.Anchor.ResetPosition();
    }

    // The nearest hostile AI fleet at sea, a pirate (bandit) one when there is one.
    private MobileParty FindEncounterEnemy(MobileParty player)
    {
        var position = player.Position.ToVec2();
        return MobileParty.All
                .Where(party => party.IsBandit && staging.IsNavalEnemy(party, player))
                .OrderBy(party => party.Position.ToVec2().DistanceSquared(position))
                .FirstOrDefault()
            ?? staging.FindNavalEnemy(player);
    }

    // Like coop.debug.map_event.engage_nearest_bandit at sea: the players go to the fleet, which is ordered to engage the
    // first one and kept on that order, so the campaign AI tick starts the encounter.
    private bool TryStageEncounter(NavalScenarioConfig config, MobileParty[] players, ShipHull hull, StringBuilder log,
        out MobileParty enemy, out string error)
    {
        enemy = FindEncounterEnemy(players[0]);
        if (enemy == null)
        {
            error = $"No hostile AI party with ships is at sea for player {config.ControllerIds[0]}.";
            return false;
        }

        if (enemy.Ai == null)
        {
            error = $"{enemy.StringId} has no campaign AI to engage with.";
            return false;
        }

        int enemyShipsBefore = enemy.Ships.Count;
        SizeFleet(enemy.Party, config.EnemyHulls, hull);
        if (config.ArmedShipsPerParty > 0) log.Append(" armedEnemy=").Append(ArmShips(enemy.Party, config.ArmedShipsPerParty));
        foreach (var player in players)
            staging.PlaceAtSea(player, enemy.Position);

        OrderEngage(enemy, players[0]);

        log.Append($" enemy={enemy.StringId}(bandit={enemy.IsBandit},ships={enemyShipsBefore}->{enemy.Ships.Count}," +
            $"troops={enemy.MemberRoster.TotalManCount}) engages={config.ControllerIds[0]}");
        error = null;
        return true;
    }

    // DoNotMakeNewDecisions keeps the order. Unforced, so the change keeps the fleet's sea state; a forced one would carry
    // isCurrentlyAtSea false to every machine.
    private void OrderEngage(MobileParty enemy, MobileParty target)
    {
        enemy.SetMoveEngageParty(target, MobileParty.NavigationType.Naval);
        enemy.Ai.DoNotMakeNewDecisions = true;
        messageBroker.Publish(this, new PartyBehaviorChangeAttempted(enemy));
    }

    // The encounter preset's wait for its map event: the parties, the deadlines and what the poll did so far.
    private sealed class EngageWait
    {
        public EngageWait(NavalScenarioConfig config, MobileParty enemy, MobileParty[] players, DateTime startedUtc)
        {
            Config = config;
            Enemy = enemy;
            Players = players;
            Deadline = startedUtc.AddSeconds(EngageWaitSeconds);
            FallbackAt = startedUtc.AddSeconds(EngageFallbackSeconds);
        }

        public NavalScenarioConfig Config { get; }
        public MobileParty Enemy { get; }
        public MobileParty[] Players { get; }
        public DateTime Deadline { get; }
        public DateTime FallbackAt { get; }
        public int Reissues { get; set; }
        public bool FallbackStarted { get; set; }

        public string Summary => $"engage re-issued {Reissues} time(s), fallback encounter {(FallbackStarted ? "started" : "not started")}";
    }

    // Polls the engage again on the game thread after a short delay.
    private void WatchEngage(EngageWait wait)
    {
        Task.Delay(EngagePollMilliseconds).ContinueWith(_ =>
            GameThread.RunSafe(() => PollEngage(wait), context: nameof(WatchEngage)));
    }

    // Keeps the engage going until the players' map event exists, then joins the others and tells the clients the scenario.
    private void PollEngage(EngageWait wait)
    {
        var config = wait.Config;
        var enemy = wait.Enemy;
        var players = wait.Players;
        if (state.Current?.ScenarioId != config.ScenarioId || !state.IsPending(NavalScenarioState.EngageStep))
        {
            // A newer encounter holding the same fleet puts the hold back at its next poll.
            ReleaseEngage(enemy);
            return;
        }

        var mapEvent = players[0].MapEvent;
        if (mapEvent == null || enemy.MapEvent != mapEvent)
        {
            if (DateTime.UtcNow < wait.Deadline)
            {
                KeepEngageOrder(wait);
                TryFallbackEncounter(wait);
                WatchEngage(wait);
                return;
            }

            string detail = $"no map event between {enemy.StringId} and {config.ControllerIds[0]} after {EngageWaitSeconds:0} s " +
                $"(enemy behavior {enemy.DefaultBehavior}, target {enemy.TargetParty?.StringId ?? "none"}, distance " +
                $"{enemy.Position.ToVec2().Distance(players[0].Position.ToVec2()):0.00}, enemy in a map event {enemy.MapEvent != null}, " +
                $"player in a map event {mapEvent != null}, {wait.Summary}); check the client for an open conversation or encounter menu";
            FailEngage(config, enemy, detail);
            return;
        }

        if (!objectManager.TryGetId(mapEvent, out string mapEventId))
        {
            FailEngage(config, enemy, "the encounter's map event is not registered");
            return;
        }

        config.MapEventId = mapEventId;
        var joined = new StringBuilder();
        for (int i = 1; i < players.Length; i++)
        {
            bool ok = staging.TryJoinNavalAlly(mapEventId, enemy, players[0], players[i], out var error);
            joined.Append($" {config.ControllerIds[i]}={(ok ? "joined" : error)}");
        }

        string outcome = $"the campaign AI started {mapEventId} (naval={mapEvent.IsNavalMapEvent}, type={mapEvent.EventType}, " +
            $"{wait.Summary})" + (joined.Length > 0 ? ";" + joined : "");
        resetTracer.Stop(enemy);
        state.Complete(NavalScenarioState.EngageStep, true, outcome);
        network.SendAll(new NetworkNavalScenarioStarted(config));
        Logger.Information("[NavalScenario] Encounter {ScenarioId}: {Outcome}", config.ScenarioId, outcome);
    }

    // Ends the encounter scenario with the engage step failed for detail.
    private void FailEngage(NavalScenarioConfig config, MobileParty enemy, string detail)
    {
        ReleaseEngage(enemy);
        state.Complete(NavalScenarioState.EngageStep, false, detail);
        state.Advance(NavalScenarioPhase.Ended);
        Logger.Error("[NavalScenario] Encounter {ScenarioId}: {Detail}", config.ScenarioId, detail);
    }

    // Gives a fleet the scenario stopped engaging its campaign AI back, as a conversation hold's release does; the flag only
    // matters to the server's campaign AI, so nothing is sent.
    private void ReleaseEngage(MobileParty enemy)
    {
        resetTracer.Stop(enemy);
        // A player conversation's hold owns the flag now and gives it back when the conversation ends.
        if (ConversationPartyHold.IsInPlayerConversation(enemy)) return;

        enemy.Ai.DoNotMakeNewDecisions = false;
        enemy.Ai.RethinkAtNextHourlyTick = true;
    }

    // Puts the fleet back on its engage order when something replaced it; the reset tracer logs what did.
    private void KeepEngageOrder(EngageWait wait)
    {
        var enemy = wait.Enemy;
        var target = wait.Players[0];
        if (IsEncounterUnderWay(enemy, target)) return;
        if (enemy.DefaultBehavior == AiBehavior.EngageParty && enemy.TargetParty == target)
        {
            // An older scenario's release of the same fleet must not let the campaign AI drop this engage.
            enemy.Ai.DoNotMakeNewDecisions = true;
            return;
        }

        string before = $"{enemy.DefaultBehavior}, target {enemy.TargetParty?.StringId ?? "none"}";
        OrderEngage(enemy, target);
        wait.Reissues++;
        if (wait.Reissues > 1) return;

        Logger.Warning("[NavalScenario] Encounter {ScenarioId}: re-issued {Enemy}'s engage order (was {Before}, now {After})",
            wait.Config.ScenarioId, enemy.StringId, before, enemy.DefaultBehavior);
    }

    // A held conversation sets the fleet to hold with its AI off, so pushing engage or a second encounter would fight it.
    private static bool IsEncounterUnderWay(MobileParty enemy, MobileParty player) => IsEncounterUnderWay(
        enemy.MapEvent != null, player.MapEvent != null, ConversationPartyHold.IsInPlayerConversation(enemy));

    /// <summary>Whether the fleet's encounter already started: a map event for either party or a player conversation with the fleet.</summary>
    internal static bool IsEncounterUnderWay(bool enemyInMapEvent, bool playerInMapEvent, bool enemyInConversation) =>
        enemyInMapEvent || playerInMapEvent || enemyInConversation;

    // Within the naval encounter distance the server starts the encounter the AI tick's OnPartyInteraction would, which
    // EncounterManagerPatches turns into the server-detected player conversation.
    private void TryFallbackEncounter(EngageWait wait)
    {
        var enemy = wait.Enemy;
        var player = wait.Players[0];
        if (wait.FallbackStarted || DateTime.UtcNow < wait.FallbackAt) return;
        if (IsEncounterUnderWay(enemy, player)) return;

        float distance = enemy.Position.Distance(player.Position);
        float encounterDistance = Campaign.Current.Models.EncounterModel.NeededMaximumNavalDistanceForEncounteringMobileParty;
        if (distance > encounterDistance) return;

        wait.FallbackStarted = true;
        EncounterManager.StartPartyEncounter(enemy.Party, player.Party);
        Logger.Information("[NavalScenario] Encounter {ScenarioId}: no map event after {Seconds:0} s, so the server started " +
            "{Enemy}'s encounter with {Player} (distance {Distance:0.00} <= {EncounterDistance:0.00}, behavior {Behavior})",
            wait.Config.ScenarioId, EngageFallbackSeconds, enemy.StringId, wait.Config.ControllerIds[0], distance, encounterDistance,
            enemy.DefaultBehavior);
    }

    // The players attack an AI siege's active blockade from the sea, as a player choosing to attack it would: the server
    // creates the blockade battle through the same factory a client's ForceBlockadeAttack request reaches.
    private bool TryStartBlockade(NavalScenarioConfig config, MobileParty[] players, ShipHull hull, StringBuilder log,
        out string error)
    {
        var player = players[0];
        if (!TryFindBlockade(config, players, hull, log, out var town, out var besieger, out bool isNewSiege, out error)) return false;

        // BlockadeBattleMapEvent.OnInitialize lifts the blockade with no battle when the attackers are this much stronger.
        float attackerPower = players.Sum(party =>
            party.Party.GetCustomStrength(BattleSideEnum.Attacker, MapEvent.PowerCalculationContext.SeaBattle));
        float blockadePower = town.SiegeEvent.BesiegerCamp.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.BlockadeBattle)
            .Sum(party => party.GetCustomStrength(BattleSideEnum.Defender, MapEvent.PowerCalculationContext.SeaBattle));
        log.Append($" seaPower={attackerPower:0}vs{blockadePower:0}");
        if (IsBlockadeLiftedWithoutBattle(attackerPower, blockadePower))
        {
            error = $"the players' sea power {attackerPower:0} is over {LiftBlockadePowerRatio} x the blockade's {blockadePower:0}, so " +
                "BlockadeBattleMapEvent.CheckLiftingBlockade would lift the blockade with no battle; " +
                (isNewSiege ? "raise hulls=n" : $"hulls=n cannot grow {town.StringId}'s existing blockade, pick town=<id> without one");
            return false;
        }

        foreach (var party in players)
            staging.PlaceAtSea(party, town.PortPosition);

        var flags = new BattleCreationFlags(forceRaid: false, forceSallyOut: false, forceVolunteers: false, forceSupplies: false,
            isSallyOutAmbush: false, forceBlockadeAttack: true, forceBlockadeSallyOutAttack: false, forceHideoutSendTroops: false);
        var mapEvent = MapEventBattleFactory.CreateMapEvent(player.Party, besieger.Party, flags);
        if (mapEvent == null || player.MapEvent != mapEvent)
        {
            error = mapEvent == null
                ? "MapEventBattleFactory refused the blockade attack (an attack prevention rule or a party already in a map event)"
                : $"the blockade battle ended at once (state {mapEvent.State}, blockade active {town.SiegeEvent?.IsBlockadeActive})";
            return false;
        }

        if (!objectManager.TryGetId(mapEvent, out string mapEventId))
        {
            error = "The blockade map event is not registered.";
            return false;
        }

        config.MapEventId = mapEventId;
        config.BlockadeTownId = town.StringId;
        log.Append($" naval={mapEvent.IsNavalMapEvent} type={mapEvent.EventType}");
        for (int i = 1; i < players.Length; i++)
        {
            if (!staging.TryJoinNavalAlly(mapEventId, besieger, player, players[i], out error)) return false;
        }

        error = null;
        return true;
    }

    internal const float LiftBlockadePowerRatio = 1.2f;

    /// <summary>Whether vanilla would lift the blockade with no battle against attackers this much stronger.</summary>
    internal static bool IsBlockadeLiftedWithoutBattle(float attackerPower, float blockadePower) =>
        attackerPower > blockadePower * LiftBlockadePowerRatio;

    // An AI siege already blockading a port, or a new one: the lord at war with the town and the players nearest to a coastal
    // town, else the nearest third-faction lord after its missing wars are declared, gets its hulls and besieges it, which
    // activates the blockade since a besieger has ships (SiegeEvent's constructor).
    private bool TryFindBlockade(NavalScenarioConfig config, MobileParty[] players, ShipHull hull, StringBuilder log,
        out Settlement town, out MobileParty besieger, out bool isNewSiege, out string error)
    {
        town = null;
        besieger = null;
        isNewSiege = false;
        var player = players[0];
        var position = player.Position.ToVec2();
        var towns = Settlement.All
            .Where(settlement => settlement.IsTown && settlement.HasPort)
            .Where(settlement => string.IsNullOrEmpty(config.BlockadeTownId) || settlement.StringId == config.BlockadeTownId)
            .OrderBy(settlement => settlement.Position.ToVec2().DistanceSquared(position))
            .ToList();
        if (towns.Count == 0)
        {
            error = string.IsNullOrEmpty(config.BlockadeTownId) ? "No town has a port." : $"{config.BlockadeTownId} is not a town with a port.";
            return false;
        }

        foreach (var candidate in towns)
        {
            var leader = candidate.SiegeEvent?.BesiegerCamp?.LeaderParty;
            if (candidate.SiegeEvent?.IsBlockadeActive != true || leader == null || !IsBlockadeAttackable(leader, player)) continue;

            town = candidate;
            besieger = leader;
            log.Append($" blockade={town.StringId}(existing, besieger={besieger.StringId},ships={besieger.Ships.Count})");
            error = null;
            return true;
        }

        var playerFactions = players.Select(party => party.MapFaction).Where(faction => faction != null).Distinct().ToArray();
        var freeTowns = towns.Where(settlement => settlement.SiegeEvent == null && !playerFactions.Contains(settlement.MapFaction));
        if (!TryPickBlockadeSiege(freeTowns.ToList(), MobileParty.AllLordParties.Where(IsFreeLord).ToList(), playerFactions,
                out var siegeTown, out var lord))
        {
            error = "No free AI lord party is at war with both a coastal town's faction and the players' faction " +
                $"{player.MapFaction?.Name} or belongs to a third faction, and no AI siege already blockades a port the players can attack.";
            return false;
        }

        if (!TryDeclareBlockadeWars(lord, siegeTown, playerFactions, log, out error)) return false;

        SizeFleet(lord.Party, Math.Max(1, config.EnemyHulls), hull);
        lord.Position = siegeTown.GatePosition;
        lord.SetMoveBesiegeSettlement(siegeTown, MobileParty.NavigationType.Default);
        Campaign.Current.SiegeEventManager.StartSiegeEvent(siegeTown, lord);
        if (siegeTown.SiegeEvent?.IsBlockadeActive != true)
        {
            error = $"{lord.StringId} besieged {siegeTown.StringId} with {lord.Ships.Count} ship(s), but the siege started with no " +
                $"active blockade (siege started {siegeTown.SiegeEvent != null})";
            return false;
        }

        town = siegeTown;
        besieger = lord;
        isNewSiege = true;
        log.Append($" blockade={town.StringId}(new siege, besieger={besieger.StringId},ships={besieger.Ships.Count}," +
            $"troops={besieger.MemberRoster.TotalManCount})");
        error = null;
        return true;
    }

    // A lord already at war with both sides near any town wins over a third-faction lord, so the towns are tried twice.
    private static bool TryPickBlockadeSiege(List<Settlement> towns, List<MobileParty> lords, IFaction[] playerFactions,
        out Settlement town, out MobileParty lord)
    {
        foreach (bool allowThirdFaction in new[] { false, true })
        {
            foreach (var candidate in towns)
            {
                var townPosition = candidate.Position.ToVec2();
                lord = PickBlockadeLord(lords, party => StandingOf(party.MapFaction, candidate.MapFaction, playerFactions),
                    party => party.Position.ToVec2().DistanceSquared(townPosition), allowThirdFaction);
                if (lord == null) continue;

                town = candidate;
                return true;
            }
        }

        town = null;
        lord = null;
        return false;
    }

    // Like duel, the scenario declares the wars a third-faction besieger lacks with the town and with each player's faction.
    private static bool TryDeclareBlockadeWars(MobileParty lord, Settlement town, IFaction[] playerFactions, StringBuilder log,
        out string error)
    {
        var declared = new List<string>();
        foreach (var enemy in new[] { town.MapFaction }.Concat(playerFactions).Distinct())
        {
            if (FactionManager.IsAtWarAgainstFaction(lord.MapFaction, enemy)) continue;

            DeclareWarAction.ApplyByDefault(lord.MapFaction, enemy);
            declared.Add($"{lord.MapFaction.Name}->{enemy.Name}");
            if (!FactionManager.IsAtWarAgainstFaction(lord.MapFaction, enemy))
            {
                error = $"DeclareWarAction did not put {lord.MapFaction.Name} at war with {enemy.Name} for {lord.StringId}'s blockade.";
                return false;
            }
        }

        if (declared.Count > 0) log.Append(" declaredWar=").Append(string.Join(",", declared));
        error = null;
        return true;
    }

    // Reads the lord's wars with the town's and every player's faction for ClassifyBlockadeLord.
    private static BlockadeLordStanding StandingOf(IFaction lordFaction, IFaction townFaction, IFaction[] playerFactions) =>
        ClassifyBlockadeLord(lordFaction == townFaction || playerFactions.Contains(lordFaction),
            FactionManager.IsAtWarAgainstFaction(lordFaction, townFaction) &&
            playerFactions.All(faction => FactionManager.IsAtWarAgainstFaction(lordFaction, faction)));

    /// <summary>How a lord's faction stands to a blockade: usable as it is, usable once wars are declared, or never.</summary>
    public enum BlockadeLordStanding
    {
        Excluded = 0,
        AtWarWithBoth = 1,
        ThirdFaction = 2,
    }

    /// <summary>The town's and the players' own factions never besiege; any other is at war with both already or a third faction.</summary>
    internal static BlockadeLordStanding ClassifyBlockadeLord(bool isTownOrPlayersFaction, bool atWarWithTownAndPlayers)
    {
        if (isTownOrPlayersFaction) return BlockadeLordStanding.Excluded;

        return atWarWithTownAndPlayers ? BlockadeLordStanding.AtWarWithBoth : BlockadeLordStanding.ThirdFaction;
    }

    /// <summary>
    /// The nearest lord already at war with the town and the players, else, when <paramref name="allowThirdFaction"/>, the
    /// nearest third-faction lord; null when none qualifies.
    /// </summary>
    internal static T PickBlockadeLord<T>(IEnumerable<T> lords, Func<T, BlockadeLordStanding> standingOf,
        Func<T, float> distanceSquaredOf, bool allowThirdFaction) where T : class
    {
        var candidates = lords
            .Select(lord => (Lord: lord, Standing: standingOf(lord)))
            .Where(candidate => candidate.Standing == BlockadeLordStanding.AtWarWithBoth ||
                (allowThirdFaction && candidate.Standing == BlockadeLordStanding.ThirdFaction))
            .ToList();

        return candidates
            .OrderBy(candidate => candidate.Standing == BlockadeLordStanding.AtWarWithBoth ? 0 : 1)
            .ThenBy(candidate => distanceSquaredOf(candidate.Lord))
            .Select(candidate => candidate.Lord)
            .FirstOrDefault();
    }

    // Whether the players can attack this besieger's blockade: an AI party at war with them, not already fighting.
    private static bool IsBlockadeAttackable(MobileParty besieger, MobileParty player) =>
        !besieger.IsPlayerParty() && besieger.MapEvent == null && besieger.MapFaction != null &&
        FactionManager.IsAtWarAgainstFaction(besieger.MapFaction, player.MapFaction);

    // An active AI lord party with troops, outside any settlement, map event, siege camp and army, free to start a siege.
    private static bool IsFreeLord(MobileParty party) =>
        party.IsActive && !party.IsPlayerParty() && party.LeaderHero != null && party.CurrentSettlement == null &&
        party.MapEvent == null && party.BesiegerCamp == null && party.Army == null && party.MapFaction != null &&
        party.MemberRoster.TotalManCount > 0;

    // start_naval's entry: sizes and arms the nearest hostile AI fleet and starts the battle against it.
    private bool TryStartAgainstAiFleet(NavalScenarioConfig config, MobileParty[] players, ShipHull hull, StringBuilder log,
        out string error)
    {
        var enemy = staging.FindNavalEnemy(players[0]);
        if (enemy == null)
        {
            error = $"No hostile AI party with ships is at sea for player {config.ControllerIds[0]}.";
            return false;
        }

        int enemyShipsBefore = enemy.Ships.Count;
        SizeFleet(enemy.Party, config.EnemyHulls, hull);
        TopUpReserve(enemy, config.ReserveTroops);
        log.Append($" enemy={enemy.StringId}(ships={enemyShipsBefore}->{enemy.Ships.Count},troops={enemy.MemberRoster.TotalManCount})");
        if (config.ReserveTroops > 0)
            log.Append($" reserve={config.ReserveTroops}(crewCapacity players={string.Join(",", players.Select(CrewCapacityOf))} " +
                $"enemy={CrewCapacityOf(enemy)})");
        if (config.ArmedShipsPerParty > 0) log.Append(" armedEnemy=").Append(ArmShips(enemy.Party, config.ArmedShipsPerParty));
        if (config.RammedShipsPerParty > 0) log.Append(" rammedEnemy=").Append(FitRams(enemy.Party, config.RammedShipsPerParty));

        if (config.SinkingEnemyHull && enemy.Ships.Count > 0)
        {
            // The HitPoints setter replicates, so the battle host builds this AI hull from the damaged ship.
            var ship = enemy.Ships[0];
            ship.HitPoints = ship.MaxHitPoints * NavalScenarioPresets.SinkingHullHitPointRatio;
            log.Append($" sinkingShip={DescribeId(ship)}(hp={ship.HitPoints:0}/{ship.MaxHitPoints:0})");
        }

        var starters = config.LateJoin ? players.Take(1).ToArray() : players;
        if (!staging.TryStart(starters, enemy, out var mapEvent, out var mapEventId, out error))
            return false;

        config.MapEventId = mapEventId;
        log.Append($" naval={mapEvent.IsNavalMapEvent}");

        if (config.LateJoin)
        {
            ScheduleLateJoin(config, enemy, players[0], players[1]);
            log.Append($" lateJoin={config.LateJoinerControllerId}@{config.DelaySeconds:0}s");
        }

        if (config.AlliedNpc)
            log.Append(" alliedNpc=").Append(JoinAlliedNpc(mapEvent, players[0], enemy, hull));

        error = null;
        return true;
    }

    // Co-op may refuse a player party attacking another one at sea; every refusal is reported with the step that failed.
    private bool TryStartDuel(NavalScenarioConfig config, MobileParty[] players, StringBuilder log, out string error)
    {
        var defender = players[0];
        var attacker = players[1];
        if (attacker.MapFaction == defender.MapFaction)
        {
            error = $"Players {config.ControllerIds[0]} and {config.ControllerIds[1]} share the faction {defender.MapFaction?.Name}.";
            return false;
        }

        if (!FactionManager.IsAtWarAgainstFaction(attacker.MapFaction, defender.MapFaction))
        {
            DeclareWarAction.ApplyByDefault(attacker.MapFaction, defender.MapFaction);
            log.Append($" declaredWar={attacker.MapFaction.Name}->{defender.MapFaction.Name}");
            if (!FactionManager.IsAtWarAgainstFaction(attacker.MapFaction, defender.MapFaction))
            {
                error = "DeclareWarAction did not put the two players' factions at war.";
                return false;
            }
        }

        // Any AI fleet at sea marks a sea position; it takes no part in the duel.
        var seaAnchor = staging.FindNavalEnemy(defender);
        if (seaAnchor == null)
        {
            error = "No AI party with ships is at sea to mark a sea position for the duel.";
            return false;
        }

        staging.PlaceAtSea(defender, seaAnchor.Position);
        staging.PlaceAtSea(attacker, seaAnchor.Position);
        StartBattleAction.Apply(attacker.Party, defender.Party);

        var mapEvent = defender.MapEvent;
        if (mapEvent == null)
        {
            error = $"StartBattleAction did not create a map event for {config.ControllerIds[1]} attacking {config.ControllerIds[0]}.";
            return false;
        }

        if (attacker.MapEvent != mapEvent || attacker.Party.Side == defender.Party.Side)
        {
            error = $"The players are not on opposite sides of one map event (attacker side {attacker.Party.Side}, defender side {defender.Party.Side}).";
            return false;
        }

        if (!objectManager.TryGetId(mapEvent, out string mapEventId))
        {
            error = "The duel map event is not registered.";
            return false;
        }

        config.MapEventId = mapEventId;
        log.Append($" naval={mapEvent.IsNavalMapEvent}");
        error = null;
        return true;
    }

    // Joins the late joiner delay seconds after staging, on the game thread.
    private void ScheduleLateJoin(NavalScenarioConfig config, MobileParty enemy, MobileParty initiator, MobileParty joiner)
    {
        Task.Delay(TimeSpan.FromSeconds(config.DelaySeconds)).ContinueWith(_ =>
            GameThread.RunSafe(() => JoinLateJoiner(config, enemy, initiator, joiner), context: nameof(ScheduleLateJoin)));
    }

    // Adds the late joiner to the running battle, or fails the step when the battle or its parties moved on.
    private void JoinLateJoiner(NavalScenarioConfig config, MobileParty enemy, MobileParty initiator, MobileParty joiner)
    {
        if (state.Current?.ScenarioId != config.ScenarioId) return;

        string error = null;
        if (!objectManager.TryGetObject(config.MapEventId, out MapEvent mapEvent) || mapEvent.IsFinalized)
            error = $"map event {config.MapEventId} has ended";
        else if (initiator.MapEvent != mapEvent || enemy.MapEvent != mapEvent)
            error = "the first player or the AI fleet left the map event";
        else if (joiner.MapEvent != null)
            error = $"player {config.LateJoinerControllerId} is already in a map event";

        if (error == null)
        {
            staging.PlaceAtSea(joiner, enemy.Position);
            staging.TryJoinNavalAlly(config.MapEventId, enemy, initiator, joiner, out error);
        }

        state.Complete(NavalScenarioState.LateJoinStep, error == null,
            error ?? $"player {config.LateJoinerControllerId} joined {config.MapEventId}");
        Logger.Information("[NavalScenario] Late join of {ControllerId}: {Outcome}", config.LateJoinerControllerId, error ?? "joined");
    }

    // Allied NPC fleets are not fielded yet, so this only records how the battle treats the joined party.
    private string JoinAlliedNpc(MapEvent mapEvent, MobileParty player, MobileParty enemy, ShipHull hull)
    {
        var playerPosition = player.Position.ToVec2();
        var ally = MobileParty.All
            .Where(party => IsAlliedLord(party, player, enemy))
            .OrderBy(party => party.Position.ToVec2().DistanceSquared(playerPosition))
            .FirstOrDefault();
        if (ally == null) return "none(no friendly lord party is free)";

        TopUpShips(ally.Party, 1, hull);
        staging.PlaceAtSea(ally, enemy.Position);
        ally.Party.MapEventSide = player.Party.MapEventSide;
        return ally.MapEvent == mapEvent
            ? $"{ally.StringId}(ships={ally.Ships.Count},troops={ally.MemberRoster.TotalManCount})"
            : $"{ally.StringId}(not added to the map event)";
    }

    // A free AI lord party friendly to the player and at war with the AI fleet.
    private static bool IsAlliedLord(MobileParty party, MobileParty player, MobileParty enemy) =>
        party.IsActive &&
        party.IsLordParty &&
        !party.IsPlayerParty() &&
        party.LeaderHero != null &&
        party.MapEvent == null &&
        party.Army == null &&
        party.SiegeEvent == null &&
        party.MemberRoster.TotalManCount > 0 &&
        party.MapFaction != null &&
        !FactionManager.IsAtWarAgainstFaction(party.MapFaction, player.MapFaction) &&
        FactionManager.IsAtWarAgainstFaction(party.MapFaction, enemy.MapFaction);

    // Same ownership path as coop.debug.party.add_ship, so each new ship replicates to the clients.
    private static void TopUpShips(PartyBase party, int count, ShipHull hull)
    {
        for (int i = ShipsMissing(party.Ships.Count, count); i > 0; i--)
            ChangeShipOwnerAction.ApplyByProduction(party, new Ship(hull));
    }

    // Tops the fleet up to count ships, or destroys the extra ones.
    private static void SizeFleet(PartyBase party, int count, ShipHull hull)
    {
        TopUpShips(party, count, hull);
        while (party.Ships.Count > count)
            DestroyShipAction.Apply(party.Ships[party.Ships.Count - 1]);
    }

    // weapons=n: vanilla mounts a hull's ShipBallista from the piece in its fore slot (MissionShipFactory keeps only the
    // equipped slot prefabs), so a ship whose hull has no fore slot becomes an armed Battle Knarr.
    private static string ArmShips(PartyBase party, int count) => FitPiece(party, count, NavalScenarioPresets.WeaponSlotTag,
        NavalScenarioPresets.WeaponPieceId, NavalScenarioPresets.WeaponHullId);

    // ram=n: MissionShip finds its MissionShipRam on the bow piece's prefab. Ships carrying a fore ballista are skipped,
    // since the bow hull swapped in has no fore slot.
    private static string FitRams(PartyBase party, int count) => FitPiece(party, count, NavalScenarioPresets.RamSlotTag,
        NavalScenarioPresets.RamPieceId, NavalScenarioPresets.RamHullId, ship => !IsArmed(ship));

    // Fits the piece in the slot of the first count ships the filter takes (any ship without one); a ship whose hull lacks
    // the slot is swapped for a new ship of the fitting hull carrying it, so the count stays.
    private static string FitPiece(PartyBase party, int count, string slotTag, string pieceId, string hullId,
        Func<Ship, bool> filter = null)
    {
        var piece = MBObjectManager.Instance.GetObject<ShipUpgradePiece>(pieceId);
        var fittingHull = MBObjectManager.Instance.GetObject<ShipHull>(hullId);
        if (piece == null || fittingHull == null) return $"{party.Id}(none: no piece {pieceId} or hull {hullId})";

        var candidates = filter == null ? party.Ships : party.Ships.Where(filter);
        int equipped = 0, swapped = 0;
        foreach (var ship in candidates.Take(count).ToList())
        {
            if (ship.HasSlot(slotTag))
            {
                ship.EquipUpgradePiece(slotTag, piece);
                equipped++;
                continue;
            }

            var fitted = new Ship(fittingHull);
            ChangeShipOwnerAction.ApplyByProduction(party, fitted);
            fitted.EquipUpgradePiece(slotTag, piece);
            DestroyShipAction.Apply(ship);
            swapped++;
        }

        return $"{party.Id}(equipped={equipped},swapped={swapped},ships={party.Ships.Count})";
    }

    // Whether the ship carries the fore ballista weapons=n fits, whoever fitted it.
    private static bool IsArmed(Ship ship) =>
        ship.HasSlot(NavalScenarioPresets.WeaponSlotTag)
        && ship.GetPieceAtSlot(NavalScenarioPresets.WeaponSlotTag)?.StringId == NavalScenarioPresets.WeaponPieceId;

    // Tops the party's regulars up to the given number of add_troops templates.
    private void TopUpTroops(MobileParty party, int templates)
    {
        var log = new StringBuilder();
        for (int i = TemplatesMissing(party.MemberRoster.TotalRegulars, templates); i > 0; i--)
            PartyCommands.AddTroopTemplate(party.MemberRoster, objectManager, log);
        if (log.Length > 0) Logger.Warning("[NavalScenario] Troop top-up of {PartyId}: {Log}", party.StringId, log.ToString());
    }

    // reserve=n: tops the healthy troops (heroes too, wounded left out, as deployment counts them) up past every ship's crew
    // capacity, so at least n wait in reserve whatever ships deploy.
    private void TopUpReserve(MobileParty party, int reserve)
    {
        if (reserve <= 0) return;

        var log = new StringBuilder();
        for (int i = ReserveTemplatesMissing(party.MemberRoster.TotalHealthyCount, CrewCapacityOf(party), reserve); i > 0; i--)
            PartyCommands.AddTroopTemplate(party.MemberRoster, objectManager, log);
        if (log.Length > 0) Logger.Warning("[NavalScenario] Reserve top-up of {PartyId}: {Log}", party.StringId, log.ToString());
    }

    // Every crew place on a party's ships, the most troops its hulls can take on deck and below.
    private static int CrewCapacityOf(MobileParty party) => party.Ships.Sum(ship => ship.TotalCrewCapacity);

    /// <summary>How many ships bring <paramref name="ships"/> up to <paramref name="wanted"/>.</summary>
    internal static int ShipsMissing(int ships, int wanted) => Math.Max(0, wanted - ships);

    /// <summary>How many add_troops templates bring <paramref name="healthy"/> troops to <paramref name="reserve"/> past <paramref name="crewCapacity"/>.</summary>
    internal static int ReserveTemplatesMissing(int healthy, int crewCapacity, int reserve) =>
        Math.Max(0, (crewCapacity + reserve - healthy + TroopsPerTemplate - 1) / TroopsPerTemplate);

    /// <summary>How many add_troops templates bring <paramref name="regulars"/> up to <paramref name="templates"/> templates' worth.</summary>
    internal static int TemplatesMissing(int regulars, int templates) =>
        Math.Max(0, ((templates * TroopsPerTemplate) - regulars + TroopsPerTemplate - 1) / TroopsPerTemplate);

    // The ship's registry id for the log, or "unregistered".
    private string DescribeId(Ship ship) => objectManager.TryGetId(ship, out var shipId) ? shipId : "unregistered";
}
#endif
