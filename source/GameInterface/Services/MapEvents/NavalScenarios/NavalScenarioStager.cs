#if DEBUG
using Common;
using Common.Logging;
using Common.Network;
using GameInterface.Services.MobileParties.Extensions;
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

        bool started = config.Duel
            ? TryStartDuel(config, players, log, out output)
            : TryStartAgainstAiFleet(config, players, hull, log, out output);
        if (!started)
        {
            output = log + " FAILED: " + output;
            Logger.Error("[NavalScenario] {Output}", output);
            return false;
        }

        state.Start(config, localControllerId: null, isServer: true);
        network.SendAll(new NetworkNavalScenarioStarted(config));

        output = log.Append(" mapEvent=").Append(config.MapEventId).ToString();
        Logger.Information("[NavalScenario] {Output}", output);
        return true;
    }

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
