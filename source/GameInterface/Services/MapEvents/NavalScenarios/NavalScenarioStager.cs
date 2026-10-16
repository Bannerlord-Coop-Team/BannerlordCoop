#if DEBUG
using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party.Commands;
using GameInterface.Services.Villages.Commands;
using Serilog;
using System;
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
using StartNavalCoopCommand = GameInterface.Services.Villages.Commands.MapEventDebugCommands.StartNavalCoopCommand;

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

    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly INavalScenarioState state;

    public NavalScenarioStager(IObjectManager objectManager, INetwork network, INavalScenarioState state)
    {
        this.objectManager = objectManager;
        this.network = network;
        this.state = state;
    }

    public bool TryStage(NavalScenarioConfig config, out string output)
    {
        var players = new MobileParty[config.ControllerIds.Length];
        for (int i = 0; i < players.Length; i++)
        {
            if (!MapEventDebugCommands.TryGetPlayerParty(config.ControllerIds[i], requireReady: true, out _, out players[i], out output))
                return false;
        }

        var hull = MBObjectManager.Instance.GetObject<ShipHull>(config.HullId);
        if (hull == null)
        {
            output = $"No ship hull has id {config.HullId}.";
            return false;
        }

        var log = new StringBuilder($"NAVAL_SCENARIO preset={config.Preset} scenario={config.ScenarioId}");

        foreach (var party in players)
        {
            TopUpShips(party.Party, config.ShipsPerPlayer, hull);
            TopUpTroops(party, config.TroopTemplatesPerPlayer);
        }
        log.Append(" players=").Append(string.Join(",", config.ControllerIds.Zip(players,
            (id, party) => $"{id}(ships={party.Ships.Count},troops={party.MemberRoster.TotalManCount})")));

        MobileParty engagingEnemy = null;
        bool started;
        if (config.Duel) started = TryStartDuel(config, players, log, out output);
        else if (config.CampaignEngage) started = TryStageEncounter(config, players, hull, log, out engagingEnemy, out output);
        else if (config.Blockade) started = TryStartBlockade(config, players, hull, log, out output);
        else started = TryStartAgainstAiFleet(config, players, hull, log, out output);
        if (!started)
        {
            output = log + " FAILED: " + output;
            Logger.Error("[NavalScenario] {Output}", output);
            RecordServerStepFailure(config, output);
            return false;
        }

        state.Start(config, localControllerId: null, isServer: true);
        if (config.Blockade) state.Complete(NavalScenarioState.BlockadeStep, true, log.ToString());

        // The encounter's map event comes from the campaign AI later; the clients hear of the scenario once it exists.
        if (engagingEnemy != null)
        {
            output = log.Append(" mapEvent=pending(engage)").ToString();
            Logger.Information("[NavalScenario] {Output}", output);
            WatchEngage(config, engagingEnemy, players, DateTime.UtcNow.AddSeconds(EngageWaitSeconds));
            return true;
        }

        network.SendAll(new NetworkNavalScenarioStarted(config));

        output = log.Append(" mapEvent=").Append(config.MapEventId).ToString();
        Logger.Information("[NavalScenario] {Output}", output);
        return true;
    }

    // A failed encounter or blockade stays visible in scenario_status with the reason.
    private void RecordServerStepFailure(NavalScenarioConfig config, string output)
    {
        string step = config.CampaignEngage ? NavalScenarioState.EngageStep : config.Blockade ? NavalScenarioState.BlockadeStep : null;
        if (step == null) return;

        state.Start(config, localControllerId: null, isServer: true);
        state.Complete(step, false, output);
        state.Advance(NavalScenarioPhase.Ended);
    }

    // The nearest hostile AI fleet at sea, a pirate (bandit) one when there is one.
    private static MobileParty FindEncounterEnemy(MobileParty player)
    {
        var position = player.Position.ToVec2();
        return MobileParty.All
                .Where(party => party.IsBandit && StartNavalCoopCommand.IsNavalEnemy(party, player))
                .OrderBy(party => party.Position.ToVec2().DistanceSquared(position))
                .FirstOrDefault()
            ?? StartNavalCoopCommand.FindNavalEnemy(player);
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
        foreach (var player in players)
            StartNavalCoopCommand.PlaceAtSea(player, enemy.Position);

        enemy.SetMoveEngageParty(players[0], MobileParty.NavigationType.Naval);
        enemy.Ai.DoNotMakeNewDecisions = true;
        MessageBroker.Instance.Publish(this, new PartyBehaviorChangeAttempted(enemy, forcePosition: true));

        log.Append($" enemy={enemy.StringId}(bandit={enemy.IsBandit},ships={enemyShipsBefore}->{enemy.Ships.Count}," +
            $"troops={enemy.MemberRoster.TotalManCount}) engages={config.ControllerIds[0]}");
        error = null;
        return true;
    }

    private void WatchEngage(NavalScenarioConfig config, MobileParty enemy, MobileParty[] players, DateTime deadline)
    {
        Task.Delay(EngagePollMilliseconds).ContinueWith(_ =>
            GameThread.RunSafe(() => PollEngage(config, enemy, players, deadline), context: nameof(WatchEngage)));
    }

    private void PollEngage(NavalScenarioConfig config, MobileParty enemy, MobileParty[] players, DateTime deadline)
    {
        if (state.Current?.ScenarioId != config.ScenarioId || !state.IsPending(NavalScenarioState.EngageStep)) return;

        var mapEvent = players[0].MapEvent;
        if (mapEvent == null || enemy.MapEvent != mapEvent)
        {
            if (DateTime.UtcNow < deadline)
            {
                WatchEngage(config, enemy, players, deadline);
                return;
            }

            string detail = $"no map event between {enemy.StringId} and {config.ControllerIds[0]} after {EngageWaitSeconds:0} s " +
                $"(enemy behavior {enemy.DefaultBehavior}, target {enemy.TargetParty?.StringId ?? "none"}, distance " +
                $"{enemy.Position.ToVec2().Distance(players[0].Position.ToVec2()):0.00}, enemy in a map event {enemy.MapEvent != null}, " +
                $"player in a map event {mapEvent != null}); check the client for an open conversation or encounter menu";
            state.Complete(NavalScenarioState.EngageStep, false, detail);
            state.Advance(NavalScenarioPhase.Ended);
            Logger.Error("[NavalScenario] Encounter {ScenarioId}: {Detail}", config.ScenarioId, detail);
            return;
        }

        if (!objectManager.TryGetId(mapEvent, out string mapEventId))
        {
            state.Complete(NavalScenarioState.EngageStep, false, "the encounter's map event is not registered");
            return;
        }

        config.MapEventId = mapEventId;
        var joined = new StringBuilder();
        for (int i = 1; i < players.Length; i++)
        {
            bool ok = StartNavalCoopCommand.TryJoinNavalAlly(objectManager, mapEventId, enemy, players[0], players[i], out var error);
            joined.Append($" {config.ControllerIds[i]}={(ok ? "joined" : error)}");
        }

        string outcome = $"the campaign AI started {mapEventId} (naval={mapEvent.IsNavalMapEvent}, type={mapEvent.EventType})" +
            (joined.Length > 0 ? ";" + joined : "");
        state.Complete(NavalScenarioState.EngageStep, true, outcome);
        network.SendAll(new NetworkNavalScenarioStarted(config));
        Logger.Information("[NavalScenario] Encounter {ScenarioId}: {Outcome}", config.ScenarioId, outcome);
    }

    // The players attack an AI siege's active blockade from the sea, as a player choosing to attack it would: the server
    // creates the blockade battle through the same factory a client's ForceBlockadeAttack request reaches.
    private bool TryStartBlockade(NavalScenarioConfig config, MobileParty[] players, ShipHull hull, StringBuilder log,
        out string error)
    {
        var player = players[0];
        if (!TryFindBlockade(config, player, hull, log, out var town, out var besieger, out error)) return false;

        foreach (var party in players)
            StartNavalCoopCommand.PlaceAtSea(party, town.PortPosition);

        // BlockadeBattleMapEvent.OnInitialize lifts the blockade with no battle when the attackers are this much stronger.
        float attackerPower = players.Sum(party =>
            party.Party.GetCustomStrength(BattleSideEnum.Attacker, MapEvent.PowerCalculationContext.SeaBattle));
        float blockadePower = town.SiegeEvent.BesiegerCamp.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.BlockadeBattle)
            .Sum(party => party.GetCustomStrength(BattleSideEnum.Defender, MapEvent.PowerCalculationContext.SeaBattle));
        log.Append($" seaPower={attackerPower:0}vs{blockadePower:0}");
        if (IsBlockadeLiftedWithoutBattle(attackerPower, blockadePower))
        {
            error = $"the players' sea power {attackerPower:0} is over {LiftBlockadePowerRatio} x the blockade's {blockadePower:0}, so " +
                "BlockadeBattleMapEvent.CheckLiftingBlockade would lift the blockade with no battle; raise hulls=n";
            return false;
        }

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
            if (!StartNavalCoopCommand.TryJoinNavalAlly(objectManager, mapEventId, besieger, player, players[i], out error)) return false;
        }

        error = null;
        return true;
    }

    internal const float LiftBlockadePowerRatio = 1.2f;

    internal static bool IsBlockadeLiftedWithoutBattle(float attackerPower, float blockadePower) =>
        attackerPower > blockadePower * LiftBlockadePowerRatio;

    // An AI siege already blockading a port, or a new one: the lord at war with the town and the players nearest to a coastal
    // town gets its hulls and besieges it, which activates the blockade since a besieger has ships (SiegeEvent's constructor).
    private bool TryFindBlockade(NavalScenarioConfig config, MobileParty player, ShipHull hull, StringBuilder log,
        out Settlement town, out MobileParty besieger, out string error)
    {
        town = null;
        besieger = null;
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

        foreach (var candidate in towns.Where(settlement => settlement.SiegeEvent == null && settlement.MapFaction != player.MapFaction))
        {
            var lord = MobileParty.AllLordParties
                .Where(party => IsFreeLord(party) && party.MapFaction.IsAtWarWith(candidate.MapFaction)
                    && party.MapFaction.IsAtWarWith(player.MapFaction))
                .OrderBy(party => party.Position.ToVec2().DistanceSquared(candidate.Position.ToVec2()))
                .FirstOrDefault();
            if (lord == null) continue;

            SizeFleet(lord.Party, Math.Max(1, config.EnemyHulls), hull);
            lord.Position = candidate.GatePosition;
            lord.SetMoveBesiegeSettlement(candidate, MobileParty.NavigationType.Default);
            Campaign.Current.SiegeEventManager.StartSiegeEvent(candidate, lord);
            if (candidate.SiegeEvent?.IsBlockadeActive != true)
            {
                error = $"{lord.StringId} besieged {candidate.StringId} with {lord.Ships.Count} ship(s), but the siege started with no " +
                    $"active blockade (siege started {candidate.SiegeEvent != null})";
                return false;
            }

            town = candidate;
            besieger = lord;
            log.Append($" blockade={town.StringId}(new siege, besieger={besieger.StringId},ships={besieger.Ships.Count}," +
                $"troops={besieger.MemberRoster.TotalManCount})");
            error = null;
            return true;
        }

        error = "No free AI lord party is at war with both a coastal town's faction and the players' faction " +
            $"{player.MapFaction?.Name}, and no AI siege already blockades a port the players can attack.";
        return false;
    }

    private static bool IsBlockadeAttackable(MobileParty besieger, MobileParty player) =>
        !besieger.IsPlayerParty() && besieger.MapEvent == null && besieger.MapFaction != null &&
        FactionManager.IsAtWarAgainstFaction(besieger.MapFaction, player.MapFaction);

    private static bool IsFreeLord(MobileParty party) =>
        party.IsActive && !party.IsPlayerParty() && party.LeaderHero != null && party.CurrentSettlement == null &&
        party.MapEvent == null && party.BesiegerCamp == null && party.Army == null && party.MapFaction != null &&
        party.MemberRoster.TotalManCount > 0;

    private bool TryStartAgainstAiFleet(NavalScenarioConfig config, MobileParty[] players, ShipHull hull, StringBuilder log,
        out string error)
    {
        var enemy = StartNavalCoopCommand.FindNavalEnemy(players[0]);
        if (enemy == null)
        {
            error = $"No hostile AI party with ships is at sea for player {config.ControllerIds[0]}.";
            return false;
        }

        int enemyShipsBefore = enemy.Ships.Count;
        SizeFleet(enemy.Party, config.EnemyHulls, hull);
        log.Append($" enemy={enemy.StringId}(ships={enemyShipsBefore}->{enemy.Ships.Count},troops={enemy.MemberRoster.TotalManCount})");

        if (config.SinkingEnemyHull && enemy.Ships.Count > 0)
        {
            // The HitPoints setter replicates, so the battle host builds this AI hull from the damaged ship.
            var ship = enemy.Ships[0];
            ship.HitPoints = ship.MaxHitPoints * NavalScenarioPresets.SinkingHullHitPointRatio;
            log.Append($" sinkingShip={DescribeId(ship)}(hp={ship.HitPoints:0}/{ship.MaxHitPoints:0})");
        }

        var starters = config.LateJoin ? players.Take(1).ToArray() : players;
        if (!StartNavalCoopCommand.TryStart(objectManager, starters, enemy, out var mapEvent, out var mapEventId, out error))
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
        var seaAnchor = StartNavalCoopCommand.FindNavalEnemy(defender);
        if (seaAnchor == null)
        {
            error = "No AI party with ships is at sea to mark a sea position for the duel.";
            return false;
        }

        StartNavalCoopCommand.PlaceAtSea(defender, seaAnchor.Position);
        StartNavalCoopCommand.PlaceAtSea(attacker, seaAnchor.Position);
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

    private void ScheduleLateJoin(NavalScenarioConfig config, MobileParty enemy, MobileParty initiator, MobileParty joiner)
    {
        Task.Delay(TimeSpan.FromSeconds(config.DelaySeconds)).ContinueWith(_ =>
            GameThread.RunSafe(() => JoinLateJoiner(config, enemy, initiator, joiner), context: nameof(ScheduleLateJoin)));
    }

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
            StartNavalCoopCommand.PlaceAtSea(joiner, enemy.Position);
            StartNavalCoopCommand.TryJoinNavalAlly(objectManager, config.MapEventId, enemy, initiator, joiner, out error);
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
        StartNavalCoopCommand.PlaceAtSea(ally, enemy.Position);
        ally.Party.MapEventSide = player.Party.MapEventSide;
        return ally.MapEvent == mapEvent
            ? $"{ally.StringId}(ships={ally.Ships.Count},troops={ally.MemberRoster.TotalManCount})"
            : $"{ally.StringId}(not added to the map event)";
    }

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

    private static void SizeFleet(PartyBase party, int count, ShipHull hull)
    {
        TopUpShips(party, count, hull);
        while (party.Ships.Count > count)
            DestroyShipAction.Apply(party.Ships[party.Ships.Count - 1]);
    }

    private void TopUpTroops(MobileParty party, int templates)
    {
        var log = new StringBuilder();
        for (int i = TemplatesMissing(party.MemberRoster.TotalRegulars, templates); i > 0; i--)
            PartyCommands.AddTroopTemplate(party.MemberRoster, objectManager, log);
        if (log.Length > 0) Logger.Warning("[NavalScenario] Troop top-up of {PartyId}: {Log}", party.StringId, log.ToString());
    }

    internal static int ShipsMissing(int ships, int wanted) => Math.Max(0, wanted - ships);

    /// <summary>How many add_troops templates bring <paramref name="regulars"/> up to <paramref name="templates"/> templates' worth.</summary>
    internal static int TemplatesMissing(int regulars, int templates) =>
        Math.Max(0, ((templates * TroopsPerTemplate) - regulars + TroopsPerTemplate - 1) / TroopsPerTemplate);

    private string DescribeId(Ship ship) => objectManager.TryGetId(ship, out var shipId) ? shipId : "unregistered";
}
#endif
