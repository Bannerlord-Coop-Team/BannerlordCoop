using Autofac;
using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Registry.Auto;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Villages.Interfaces;
using Helpers;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Villages.Commands;

public class MapEventDebugCommands
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEventDebugCommands>();
    private static WoundedAlliedFixture woundedAlliedFixture;

    private sealed class WoundedAlliedFixture
    {
        public string ControllerId;
        public Hero PlayerHero;
        public MobileParty PlayerParty;
        public MapEvent MapEvent;
        public PartyBase[] InvolvedParties;
        public int OriginalHitPoints;
        public float OriginalRecentEventsMorale;
        public TroopRosterElement[] OriginalRoster;
        public CampaignVec2 OriginalPosition;
    }

    /// <summary>
    /// Attempts to get the ObjectManager
    /// </summary>
    /// <param name="objectManager">Resolved ObjectManager, will be null if unable to resolve</param>
    /// <returns>True if ObjectManager was resolved, otherwise False</returns>
    private static bool TryGetObjectManager(out IObjectManager objectManager)
    {
        objectManager = null;
        if (ContainerProvider.TryGetContainer(out var container) == false) return false;

        return container.TryResolve(out objectManager);
    }

    private static bool MatchesPartyId(IObjectManager objectManager, MobileParty party, string id)
    {
        if (party == null || string.IsNullOrEmpty(id)) return false;
        if (party.StringId == id) return true;
        if (objectManager.TryGetId(party, out string mobilePartyId) && mobilePartyId == id) return true;

        return party.Party != null &&
               objectManager.TryGetId(party.Party, out string partyBaseId) &&
               partyBaseId == id;
    }

    /// <summary>
    /// Starts the current battle through the normal client/server mission-start gate.
    /// </summary>
    [CommandLineArgumentFunction("start_attack_mission", "coop.debug.mapevent")]
    public static string StartAttackMission(List<string> args)
    {
        if (ModInformation.IsServer)
        {
            return "Run this command on a client";
        }

        if (args.Count != 0)
        {
            return "Usage: coop.debug.mapevent.start_attack_mission";
        }

        var mainParty = MobileParty.MainParty;
        var mapEvent = mainParty?.MapEvent;
        if (mapEvent == null)
        {
            return "The main party has no replicated map event";
        }

        if (!TryGetObjectManager(out var objectManager)
            || !objectManager.TryGetId(mapEvent, out var mapEventId)
            || !objectManager.TryGetId(mainParty, out var partyId))
        {
            return "Unable to resolve the current battle ids";
        }

        var coordinator = BattleStartCoordinator.Instance;
        if (coordinator == null)
        {
            return "Battle start coordinator is unavailable";
        }

        return coordinator.RequestBlocking(BattleStartMode.Mission, mapEventId, partyId)
            ? $"Starting attack mission for {mapEventId}"
            : $"Server rejected attack mission for {mapEventId}";
    }

    // coop.debug.mapevent.start_looter
    /// <summary>
    /// Starts combat with looter
    /// </summary>
    [CommandLineArgumentFunction("start_looter", "coop.debug.mapevent")]
    public static string StartRandomLooterMapEvent(List<string> args)
    {
        //if (args.Count != 2)
        //{
        //    return "Usage: coop.debug.besiegercamp.set_number_of_troops_killed_on_side <besiegerCampId> <value> ";
        //}

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (!objectManager.TryGetObject("sea_raiders_1", out PartyBase partyBase))
        {
            return $"BesiegerCamp with ID: sea_raiders_1 not found";
        }

        EncounterManager.StartPartyEncounter(MobileParty.MainParty.Party, partyBase);


        return $"MapEvent Started";
    }

    // coop.debug.mapevent.start_nearest_looter
    /// <summary>
    /// Forces an encounter between the player's party and the nearest active bandit/looter party, so
    /// the bandit surrender/recruit dialogue can be reached without chasing one down. Run on a client
    /// (uses the player's main party). Bring a much larger party than the bandits so they offer to
    /// surrender or join.
    /// </summary>
    [CommandLineArgumentFunction("start_nearest_looter", "coop.debug.mapevent")]
    public static string StartNearestLooterMapEvent(List<string> args)
    {
        if (!TryGetObjectManager(out var objectManager))
        {
            return "Unable to resolve ObjectManager";
        }

        var mainParty = MobileParty.MainParty;
        if (mainParty == null)
        {
            return "No main party — run this on a client with a player party.";
        }

        var mainPos = mainParty.Position.ToVec2();
        var nearest = MobileParty.All
            .Where(p => p.IsActive && p.IsBandit && p != mainParty
                        && p.MapEvent == null && p.CurrentSettlement == null && p.MemberRoster.TotalManCount > 0)
            .OrderBy(p => p.Position.ToVec2().DistanceSquared(mainPos))
            .FirstOrDefault();

        if (nearest == null)
        {
            return "No active bandit/looter party found on the map.";
        }

        EncounterManager.StartPartyEncounter(mainParty.Party, nearest.Party);

        var partyId = objectManager.TryGetId(nearest, out string registryId) ? registryId : nearest.StringId;

        return $"Started encounter with {nearest.Name} (StringId {nearest.StringId}, registry id {partyId}), " +
               $"{nearest.MemberRoster.TotalManCount} troops, {nearest.Position.ToVec2().Distance(mainPos):0.0} away.";
    }

    // coop.debug.mapevent.start_nearest_bandit_attack PlayerOne [excludedPartyId]
    /// <summary>
    /// Starts a server-authoritative bandit attack encounter against a connected player.
    /// </summary>
    [CommandLineArgumentFunction("start_nearest_bandit_attack", "coop.debug.mapevent")]
    public static string StartNearestBanditAttack(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Run this command on the server.";
        }

        if (args.Count < 1 || args.Count > 2)
        {
            return "Usage: coop.debug.mapevent.start_nearest_bandit_attack <controllerId> [excludedPartyId]";
        }

        if (!TryGetPlayerParty(args[0], requireReady: true, out var objectManager, out var playerParty, out var error))
        {
            return error;
        }

        const int maximumFixtureTroops = 8;
        var remainingFixtureTroops = maximumFixtureTroops;
        var removedTroops = 0;
        for (var index = playerParty.MemberRoster.Count - 1; index >= 0; index--)
        {
            var element = playerParty.MemberRoster.GetElementCopyAtIndex(index);
            if (element.Character.IsHero)
                continue;

            var kept = Math.Min(element.Number, remainingFixtureTroops);
            var removed = element.Number - kept;
            if (removed > 0)
            {
                playerParty.MemberRoster.AddToCountsAtIndex(
                    index,
                    -removed,
                    -Math.Min(element.WoundedNumber, removed),
                    removeDepleted: false);
                removedTroops += removed;
            }
            remainingFixtureTroops -= kept;
        }
        playerParty.MemberRoster.RemoveZeroCounts();

        if (playerParty.CurrentSettlement != null)
        {
            LeaveSettlementAction.ApplyForParty(playerParty);
        }

        var excludedPartyId = args.Count == 2 ? args[1] : null;
        var playerPosition = playerParty.Position.ToVec2();
        var banditParty = MobileParty.All
            .Where(p => p.IsActive && p.IsBandit && p != playerParty
                        && p.MapEvent == null && p.CurrentSettlement == null && p.MemberRoster.TotalManCount > 0
                        && !MatchesPartyId(objectManager, p, excludedPartyId))
            .OrderBy(p => p.Position.ToVec2().DistanceSquared(playerPosition))
            .FirstOrDefault();

        if (banditParty == null)
        {
            return "No active bandit/looter party found on the map.";
        }

        StartBattleAction.Apply(banditParty.Party, playerParty.Party);

        var partyId = objectManager.TryGetId(banditParty, out string registryId)
            ? registryId
            : banditParty.StringId;
        var partyBaseId = objectManager.TryGetId(banditParty.Party, out string partyBaseRegistryId)
            ? partyBaseRegistryId
            : "<unregistered>";

        return $"Started attack by {banditParty.Name} (StringId {banditParty.StringId}, " +
               $"registry id {partyId}, PartyBase id {partyBaseId}) " +
               $"against player {args[0]} after removing {removedTroops} excess fixture troops.";
    }

    [CommandLineArgumentFunction("finish_non_battle_encounter", "coop.debug.mapevent")]
    public static string FinishNonBattleEncounter(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";

        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.finish_non_battle_encounter";

        if (PlayerEncounter.Current == null)
            return "No player encounter is active.";
        if (PlayerEncounter.Battle != null || MobileParty.MainParty?.MapEvent != null)
            return "Refusing to finish a battle encounter.";

        PlayerEncounter.Finish();
        return "Finished the current non-battle encounter.";
    }

    [CommandLineArgumentFunction("join_existing", "coop.debug.mapevent")]
    public static string JoinExistingBattle(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";

        if (args.Count != 2 ||
            !Enum.TryParse(args[1], true, out BattleSideEnum side) ||
            (side != BattleSideEnum.Attacker && side != BattleSideEnum.Defender))
        {
            return "Usage: coop.debug.mapevent.join_existing <mapEventId> <Attacker|Defender>";
        }

        if (PlayerEncounter.Current != null)
            return "A player encounter is already active.";
        if (!TryGetObjectManager(out var objectManager))
            return "Unable to resolve ObjectManager";
        if (!objectManager.TryGetObjectWithLogging<MapEvent>(args[0], out var mapEvent))
            return $"Unable to resolve map event {args[0]}.";
        if (mapEvent.IsFinalized || mapEvent.BattleState != BattleState.None)
            return $"Map event {args[0]} is already concluded.";

        var opposingParty = mapEvent.GetLeaderParty(
            side == BattleSideEnum.Attacker ? BattleSideEnum.Defender : BattleSideEnum.Attacker);
        if (opposingParty == null)
            return $"Map event {args[0]} has no opposing leader party.";

        PlayerEncounter.Start();
        if (side == BattleSideEnum.Attacker)
            PlayerEncounter.Current.SetupFields(MobileParty.MainParty.Party, opposingParty);
        else
            PlayerEncounter.Current.SetupFields(opposingParty, MobileParty.MainParty.Party);
        PlayerEncounter.JoinBattle(side);

        return $"Started the {side} join encounter for map event {args[0]}.";
    }

    // coop.debug.mapevent.wounded_allied_fixture_start PlayerOne
    /// <summary>Creates the wounded, troop-less player plus healthy allied force field encounter from #2097.</summary>
    [CommandLineArgumentFunction("wounded_allied_fixture_start", "coop.debug.mapevent")]
    public static string StartWoundedAlliedFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 1)
            return "Usage: coop.debug.mapevent.wounded_allied_fixture_start <controllerId>";

        if (woundedAlliedFixture != null)
            return $"Fixture already active for {woundedAlliedFixture.ControllerId}.";

        if (!TryGetPlayerParty(args[0], requireReady: true, out var objectManager, out var playerParty, out var error))
            return error;

        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !playerManager.TryGetPlayer(args[0], out var player) ||
            !objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var playerHero))
        {
            return $"Unable to resolve player hero for {args[0]}.";
        }

        if (!ContainerProvider.TryResolve<INetwork>(out var network))
            return "Unable to resolve network.";

        if (playerParty.PartyMoveMode != MoveModeType.Hold)
            return $"Player party {playerParty.StringId} must be holding before the fixture starts.";

        var playerPosition = playerParty.Position.ToVec2();
        var banditParty = MobileParty.All
            .Where(p => p.IsActive && p.IsBandit && p.MapEvent == null && p.CurrentSettlement == null &&
                        p.MemberRoster.TotalHealthyCount > 0)
            .OrderBy(p => p.Position.ToVec2().DistanceSquared(playerPosition))
            .FirstOrDefault();
        if (banditParty == null)
            return "No active healthy bandit party is available.";

        var alliedParty = MobileParty.All
            .Where(p => p.IsActive && !p.IsBandit && !p.IsPlayerParty() && p != playerParty &&
                        p.MapEvent == null && p.CurrentSettlement == null && p.MemberRoster.TotalHealthyCount > 0 &&
                        p.MapFaction != null &&
                        !VillageHostileFactionStanceHelper.HasWarStance(playerParty.MapFaction, p.MapFaction) &&
                        VillageHostileFactionStanceHelper.HasWarStance(banditParty.MapFaction, p.MapFaction))
            .OrderBy(p => p.Position.ToVec2().DistanceSquared(playerPosition))
            .FirstOrDefault();
        if (alliedParty == null)
            return "No active healthy AI party is available for the allied side.";

        var fixture = new WoundedAlliedFixture
        {
            ControllerId = args[0],
            PlayerHero = playerHero,
            PlayerParty = playerParty,
            OriginalHitPoints = playerHero.HitPoints,
            OriginalRecentEventsMorale = playerParty.RecentEventsMorale,
            OriginalRoster = playerParty.MemberRoster.GetTroopRoster().ToArray(),
            OriginalPosition = playerParty.Position,
        };

        try
        {
            playerHero.HitPoints = 1;
            RemoveHealthyPlayerTroops(fixture);
            playerParty.RecentEventsMorale = -1000f;

            fixture.MapEvent = MapEventBattleFactory.CreateMapEvent(
                banditParty.Party,
                playerParty.Party,
                default);
            if (fixture.MapEvent == null)
                throw new InvalidOperationException("The bandit encounter did not create a map event.");

            alliedParty.Party.MapEventSide = playerParty.Party.MapEventSide;
            fixture.InvolvedParties = fixture.MapEvent.InvolvedParties.ToArray();

            if (!objectManager.TryGetId(banditParty.Party, out string banditPartyId) ||
                !objectManager.TryGetId(playerParty.Party, out string playerPartyId) ||
                !objectManager.TryGetId(fixture.MapEvent, out string fixtureMapEventId))
            {
                throw new InvalidOperationException("Unable to resolve the fixture's network ids.");
            }

            network.SendAll(new NetworkPlayerPartyHostileEncounterStarted(
                $"debug-2097-{Guid.NewGuid():N}",
                banditPartyId,
                playerPartyId,
                fixtureMapEventId));
            woundedAlliedFixture = fixture;
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to create wounded allied force fixture");
            woundedAlliedFixture = fixture;
            if (TryRestoreWoundedAlliedFixture(fixture, out var restoreError))
                woundedAlliedFixture = null;
            else
                return $"Fixture setup failed: {e.Message}. Cleanup failed: {restoreError}. Run the restore command.";

            return $"Fixture setup failed: {e.Message}";
        }

        objectManager.TryGetId(fixture.MapEvent, out string mapEventId);
        return $"Wounded allied fixture started: controller={args[0]}, mapEvent={mapEventId}, " +
               $"playerHealthy={playerParty.Party.NumberOfHealthyMembers}, alliedParty={alliedParty.StringId}, " +
               $"alliedHealthy={alliedParty.Party.NumberOfHealthyMembers}, banditParty={banditParty.StringId}.";
    }

    // coop.debug.mapevent.wounded_allied_fixture_state PlayerOne
    /// <summary>Reports the #2097 fixture state and the local patched order-attack option when applicable.</summary>
    [CommandLineArgumentFunction("wounded_allied_fixture_state", "coop.debug.mapevent")]
    public static string GetWoundedAlliedFixtureState(List<string> args)
    {
        if (args.Count != 1)
            return "Usage: coop.debug.mapevent.wounded_allied_fixture_state <controllerId>";

        if (!TryGetPlayerParty(args[0], requireReady: false, out var objectManager, out var playerParty, out var error))
            return error;

        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !playerManager.TryGetPlayer(args[0], out var player) ||
            !objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var playerHero))
        {
            return $"Unable to resolve player hero for {args[0]}.";
        }

        var mapEvent = playerParty.MapEvent;
        var side = playerParty.Party.MapEventSide;
        var alliedHealthy = side?.Parties
            .Where(p => p.Party != playerParty.Party)
            .Sum(p => p.Party.NumberOfHealthyMembers) ?? 0;

        var option = "not-local";
        if (ModInformation.IsClient && playerParty == MobileParty.MainParty && PlayerEncounter.Current != null)
        {
            var callbackArgs = new MenuCallbackArgs((MenuContext)null, null);
            var shown = new EncounterGameMenuBehavior()
                .game_menu_encounter_order_attack_on_condition(callbackArgs);
            var renderedOption = Campaign.Current?.CurrentMenuContext?.GameMenu?.MenuOptions
                .FirstOrDefault(menuOption => menuOption.IdString == "str_order_attack");
            option = $"conditionShown={shown},conditionEnabled={callbackArgs.IsEnabled}," +
                     $"leaveType={callbackArgs.optionLeaveType},renderedRegistered={renderedOption != null}," +
                     $"renderedEnabled={renderedOption?.IsEnabled ?? false}";
        }

        objectManager.TryGetId(mapEvent, out string mapEventId);
        return $"Wounded allied fixture state: controller={args[0]}, local={playerParty == MobileParty.MainParty}, " +
               $"hitPoints={playerHero.HitPoints}, wounded={playerHero.IsWounded}, " +
               $"roster={playerParty.MemberRoster.TotalManCount}, playerHealthy={playerParty.Party.NumberOfHealthyMembers}, " +
               $"morale={playerParty.Morale:0.##}, recentEventsMorale={playerParty.RecentEventsMorale:0.##}, " +
               $"position={playerParty.Position.X:R}|{playerParty.Position.Y:R}, moveMode={playerParty.PartyMoveMode}, " +
               $"alliedHealthy={alliedHealthy}, mapEvent={mapEventId ?? "none"}, " +
               $"menu={Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "none"}, option={option}.";
    }

    // coop.debug.mapevent.wounded_allied_fixture_restore PlayerOne
    /// <summary>Finalizes the #2097 fixture and restores the player's original hero, morale, and roster state.</summary>
    [CommandLineArgumentFunction("wounded_allied_fixture_restore", "coop.debug.mapevent")]
    public static string RestoreWoundedAlliedFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 1)
            return "Usage: coop.debug.mapevent.wounded_allied_fixture_restore <controllerId>";

        if (woundedAlliedFixture == null || woundedAlliedFixture.ControllerId != args[0])
            return $"No active fixture exists for {args[0]}.";

        var fixture = woundedAlliedFixture;
        if (!TryRestoreWoundedAlliedFixture(fixture, out var error))
            return $"Fixture restore failed: {error}. Retry the restore command.";

        woundedAlliedFixture = null;

        return $"Wounded allied fixture restored: controller={args[0]}, hitPoints={fixture.PlayerHero.HitPoints}, " +
               $"roster={fixture.PlayerParty.MemberRoster.TotalManCount}.";
    }

    private static void RemoveHealthyPlayerTroops(WoundedAlliedFixture fixture)
    {
        var roster = fixture.PlayerParty.MemberRoster;
        for (int i = roster.Count - 1; i >= 0; i--)
        {
            var element = roster.GetElementCopyAtIndex(i);
            if (element.Character == fixture.PlayerHero.CharacterObject)
            {
                var woundedToAdd = element.Number - element.WoundedNumber;
                if (woundedToAdd > 0)
                    roster.AddToCounts(element.Character, 0, false, woundedToAdd);
                continue;
            }

            roster.AddToCountsAtIndex(i, -element.Number, -element.WoundedNumber, 0, false);
        }
    }

    private static void RestoreWoundedAlliedFixture(WoundedAlliedFixture fixture)
    {
        if (fixture.MapEvent != null)
        {
            if (!fixture.MapEvent.IsFinalized)
                fixture.MapEvent.FinalizeEvent();

            if (HasAttachedFixtureParties(fixture))
                RecoverPartiallyFinalizedMapEvent(fixture);
        }

        fixture.PlayerHero.HitPoints = fixture.OriginalHitPoints;
        fixture.PlayerParty.RecentEventsMorale = fixture.OriginalRecentEventsMorale;
        fixture.PlayerParty.Position = fixture.OriginalPosition;
        fixture.PlayerParty.SetMoveModeHold();
        fixture.PlayerParty.ResetNavigationToHold();
        MessageBroker.Instance.Publish(
            typeof(MapEventDebugCommands),
            new PartyBehaviorChangeAttempted(
                fixture.PlayerParty,
                forcePosition: true,
                isCurrentlyAtSea: fixture.PlayerParty.IsCurrentlyAtSea,
                resetMovementToHold: true));

        var roster = fixture.PlayerParty.MemberRoster;
        for (int i = roster.Count - 1; i >= 0; i--)
        {
            var element = roster.GetElementCopyAtIndex(i);
            roster.AddToCountsAtIndex(i, -element.Number, -element.WoundedNumber, 0, false);
        }

        foreach (var element in fixture.OriginalRoster)
        {
            roster.AddToCounts(element.Character, element.Number, false, element.WoundedNumber, element.Xp, true);
        }
    }

    private static bool HasAttachedFixtureParties(WoundedAlliedFixture fixture) =>
        fixture.InvolvedParties?.Any(p => p?._mapEventSide?.MapEvent == fixture.MapEvent) == true ||
        fixture.MapEvent.AttackerSide?.Parties.Count > 0 ||
        fixture.MapEvent.DefenderSide?.Parties.Count > 0;

    private static void RecoverPartiallyFinalizedMapEvent(WoundedAlliedFixture fixture)
    {
        foreach (var party in fixture.InvolvedParties ?? Array.Empty<PartyBase>())
        {
            if (party?._mapEventSide?.MapEvent != fixture.MapEvent) continue;

            party._mapEventSide = null;
            if (party.MobileParty != null)
                party.MobileParty.EventPositionAdder = TaleWorlds.Library.Vec2.Zero;
            party.SetVisualAsDirty();
        }

        fixture.MapEvent.AttackerSide?.Clear();
        fixture.MapEvent.DefenderSide?.Clear();
        if (HasAttachedFixtureParties(fixture))
            throw new InvalidOperationException("The partially finalized fixture still has attached parties.");

        MessageBroker.Instance.Publish(fixture.MapEvent, new MapEventFinalized(fixture.MapEvent));
        MessageBroker.Instance.Publish(fixture.MapEvent, new InstanceDestroyed<MapEvent>(fixture.MapEvent));
    }

    private static bool TryRestoreWoundedAlliedFixture(WoundedAlliedFixture fixture, out string error)
    {
        try
        {
            RestoreWoundedAlliedFixture(fixture);
            error = null;
            return true;
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to restore wounded allied force fixture");
            error = e.Message;
            return false;
        }
    }

    [CommandLineArgumentFunction("leave_settlement", "coop.debug.mapevent")]
    public static string LeaveSettlement(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 1)
            return "Usage: coop.debug.mapevent.leave_settlement <controllerId>";

        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
            return "Unable to resolve PlayerManager";

        if (!playerManager.TryGetPlayer(args[0], out var player))
            return $"No registered player has controller id {args[0]}.";

        if (!playerManager.IsConnected(player))
            return $"Player {args[0]} is not connected.";

        if (!TryGetObjectManager(out var objectManager) ||
            !objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var playerParty))
            return $"Unable to resolve player party {player.MobilePartyId}.";

        var settlement = playerParty.CurrentSettlement;
        if (settlement == null)
            return $"Player {args[0]} is already outside a settlement.";

        LeaveSettlementAction.ApplyForParty(playerParty);
        return $"Moved player {args[0]} out of {settlement.Name}.";
    }

    [CommandLineArgumentFunction("finish_current_encounter", "coop.debug.mapevent")]
    public static string FinishCurrentEncounter(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";

        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.finish_current_encounter";

        if (PlayerEncounter.Current == null)
            return "No active encounter.";

        PlayerEncounter.Finish();
        return "Finished the current local encounter.";
    }

    [CommandLineArgumentFunction("enter_current_battle", "coop.debug.mapevent")]
    public static string EnterCurrentBattle(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";

        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.enter_current_battle";

        if (PlayerEncounter.Current == null)
            return "No active encounter.";

        if (PlayerEncounter.Battle == null)
        {
            if (PlayerEncounter.StartBattle() == null)
                return "Unable to start the current battle.";

            GameMenu.SwitchToMenu("encounter");
        }

        var menuContext = Campaign.Current?.CurrentMenuContext;
        if (menuContext == null)
            return "No active encounter menu.";

        MenuHelper.EncounterAttackConsequence(new MenuCallbackArgs(menuContext, null));
        return "Requested entry into the current battle.";
    }

    // coop.debug.mapevent.finish_player_encounter PlayerOne
    /// <summary>
    /// Requests the connected client's encounter close through the existing authoritative leave message.
    /// </summary>
    [CommandLineArgumentFunction("finish_player_encounter", "coop.debug.mapevent")]
    public static string FinishPlayerEncounter(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Run this command on the server.";
        }

        if (args.Count != 1)
        {
            return "Usage: coop.debug.mapevent.finish_player_encounter <controllerId>";
        }

        if (!ContainerProvider.TryResolve<INetwork>(out var network))
        {
            return "Unable to resolve Network";
        }

        if (!TryGetPlayerParty(args[0], requireReady: true, out var objectManager, out var playerParty, out var error))
        {
            return error;
        }

        if (!objectManager.TryGetIdWithLogging(playerParty.Party, out var partyBaseId))
        {
            return $"Unable to resolve PartyBase for player {args[0]}.";
        }

        network.SendAll(new NetworkPartyLeftBattle(partyBaseId));
        return $"Requested encounter finish for player {args[0]} (PartyBase id {partyBaseId}).";
    }

    // coop.debug.mapevent.conversation_hold_state <partyBaseId>
    /// <summary>
    /// Reports whether the server currently holds an AI PartyBase for a conversation.
    /// </summary>
    [CommandLineArgumentFunction("conversation_hold_state", "coop.debug.mapevent")]
    public static string ConversationHoldState(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Run this command on the server.";
        }

        if (args.Count != 1)
        {
            return "Usage: coop.debug.mapevent.conversation_hold_state <partyBaseId>";
        }

        var held = ConversationPartyTracker.Instance?.TryGetEngagement(args[0], out _) == true;
        return $"Conversation hold for PartyBase id {args[0]}: {(held ? "held" : "released")}.";
    }

    // coop.debug.mapevent.peace_pursuit_fixture PlayerOne
    /// <summary>
    /// Finds a neutral AI party that can be used without changing its original movement state.
    /// </summary>
    [CommandLineArgumentFunction("peace_pursuit_fixture", "coop.debug.mapevent")]
    public static string GetPeacePursuitFixture(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Run this command on the server.";
        }

        if (args.Count != 1)
        {
            return "Usage: coop.debug.mapevent.peace_pursuit_fixture <controllerId>";
        }

        if (!TryGetPlayerParty(args[0], requireReady: true, out var objectManager, out var playerParty, out var error))
        {
            return error;
        }

        var neutralParty = FindPeacePursuitFixture(playerParty);
        if (neutralParty == null)
        {
            return "No active neutral AI party already holding on the map.";
        }

        return FormatPeacePursuitState("Peace pursuit fixture", objectManager, neutralParty, playerParty);
    }

    // coop.debug.mapevent.peace_pursuit_state PlayerOne mobileParty_1
    /// <summary>
    /// Reports the pursuit-test party state on the current machine.
    /// </summary>
    [CommandLineArgumentFunction("peace_pursuit_state", "coop.debug.mapevent")]
    public static string GetPeacePursuitState(List<string> args)
    {
        if (args.Count != 2)
        {
            return "Usage: coop.debug.mapevent.peace_pursuit_state <controllerId> <partyStringId>";
        }

        if (!TryGetPlayerParty(args[0], requireReady: false, out var objectManager, out var playerParty, out var error))
        {
            return error;
        }

        var neutralParty = Campaign.Current.CampaignObjectManager.Find<MobileParty>(args[1]);
        if (neutralParty == null)
        {
            return $"Party {args[1]} was not found.";
        }

        return FormatPeacePursuitState("Peace pursuit state", objectManager, neutralParty, playerParty);
    }

    // coop.debug.mapevent.test_peace_stops_pursuit PlayerOne mobileParty_1
    /// <summary>
    /// Makes a selected neutral AI party pursue a connected player, then makes peace.
    /// </summary>
    [CommandLineArgumentFunction("test_peace_stops_pursuit", "coop.debug.mapevent")]
    public static string TestPeaceStopsPursuit(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Run this command on the server.";
        }

        if (args.Count != 2)
        {
            return "Usage: coop.debug.mapevent.test_peace_stops_pursuit <controllerId> <partyStringId>";
        }

        if (!TryGetPlayerParty(args[0], requireReady: true, out var objectManager, out var playerParty, out var error))
        {
            return error;
        }

        var neutralParty = Campaign.Current.CampaignObjectManager.Find<MobileParty>(args[1]);
        if (neutralParty == null)
        {
            return $"Party {args[1]} was not found.";
        }

        if (!IsPeacePursuitFixture(neutralParty, playerParty))
        {
            return $"Party {args[1]} is not a neutral AI party already holding on the map.";
        }

        DeclareWarAction.ApplyByDefault(neutralParty.MapFaction, playerParty.MapFaction);
        if (!FactionManager.IsAtWarAgainstFaction(neutralParty.MapFaction, playerParty.MapFaction))
        {
            return $"Unable to establish war between {neutralParty.MapFaction.Name} and {playerParty.MapFaction.Name}.";
        }

        neutralParty.SetMoveGoAroundParty(playerParty, MobileParty.NavigationType.Default);
        MakePeaceAction.Apply(neutralParty.MapFaction, playerParty.MapFaction);

        var stopped = neutralParty.DefaultBehavior == AiBehavior.Hold &&
                      neutralParty.PartyMoveMode == MoveModeType.Hold &&
                      neutralParty.TargetParty == null &&
                      !FactionManager.IsAtWarAgainstFaction(neutralParty.MapFaction, playerParty.MapFaction);

        return FormatPeacePursuitState($"Peace pursuit test {(stopped ? "passed" : "failed")}",
            objectManager,
            neutralParty,
            playerParty);
    }

    private static bool TryGetPlayerParty(
        string controllerId,
        bool requireReady,
        out IObjectManager objectManager,
        out MobileParty playerParty,
        out string error)
    {
        objectManager = null;
        playerParty = null;
        error = null;

        if (!TryGetObjectManager(out objectManager))
        {
            error = "Unable to resolve ObjectManager";
            return false;
        }

        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
        {
            error = "Unable to resolve PlayerManager";
            return false;
        }

        if (!playerManager.TryGetPlayer(controllerId, out var player))
        {
            error = $"No registered player has controller id {controllerId}.";
            return false;
        }

        if (requireReady && ModInformation.IsServer && !playerManager.IsConnected(player))
        {
            error = $"Player {controllerId} is not connected.";
            return false;
        }

        if (!objectManager.TryGetObjectWithLogging(player.MobilePartyId, out playerParty))
        {
            error = $"Unable to resolve player party {player.MobilePartyId}.";
            return false;
        }

        if (requireReady && playerParty.MapEvent != null)
        {
            error = $"Player {controllerId} is already in a map event.";
            return false;
        }

        if (playerParty.MapFaction == null)
        {
            error = $"Player {controllerId} has no map faction.";
            return false;
        }

        return true;
    }

    private static MobileParty FindPeacePursuitFixture(MobileParty playerParty)
    {
        var playerPosition = playerParty.Position.ToVec2();
        return MobileParty.All
            .Where(p => IsPeacePursuitFixture(p, playerParty))
            .OrderBy(p => p.Position.ToVec2().DistanceSquared(playerPosition))
            .FirstOrDefault();
    }

    private static bool IsPeacePursuitFixture(MobileParty party, MobileParty playerParty)
    {
        return party.IsActive &&
               !party.IsBandit &&
               !party.IsPlayerParty() &&
               party != playerParty &&
               party.MapEvent == null &&
               party.CurrentSettlement == null &&
               party.MemberRoster.TotalManCount > 0 &&
               party.MapFaction != null &&
               party.MapFaction != playerParty.MapFaction &&
               !FactionManager.IsAtWarAgainstFaction(party.MapFaction, playerParty.MapFaction) &&
               party.DefaultBehavior == AiBehavior.Hold &&
               party.PartyMoveMode == MoveModeType.Hold &&
               party.TargetParty == null;
    }

    private static string FormatPeacePursuitState(
        string prefix,
        IObjectManager objectManager,
        MobileParty party,
        MobileParty playerParty)
    {
        var registryId = objectManager.TryGetId(party, out string partyId) ? partyId : "none";
        var target = party.TargetParty == null ? "none" : party.TargetParty.StringId;
        var atWar = FactionManager.IsAtWarAgainstFaction(party.MapFaction, playerParty.MapFaction);
        var mapEvent = party.MapEvent == null ? "none" : party.MapEvent.ToString();

        return $"{prefix}: party={party.StringId}, registryId={registryId}, behavior={party.DefaultBehavior}, " +
               $"moveMode={party.PartyMoveMode}, target={target}, atWar={atWar}, mapEvent={mapEvent}.";
    }

    /// <summary>
    /// Kills a random troop from the enemy side of the current map event.
    /// </summary>
    [CommandLineArgumentFunction("kill_random_troop", "coop.debug.mapevent")]
    public static string KillRandomTroop(List<string> args)
    {
        var mapEvent = MobileParty.MainParty.MapEvent;
        if (mapEvent is null)
        {
            return "Main party is not in a map event";
        }

        var mainPartySide = MobileParty.MainParty.MapEventSide;
        if (mainPartySide is null)
        {
            return "Main party has no map event side";
        }

        var enemySide = mapEvent._sides
            .SingleOrDefault(side => side != mainPartySide);

        if (enemySide is null)
        {
            return "Failed to get enemy map event side";
        }

        var party = enemySide.Parties[MBRandom.RandomInt(enemySide.Parties.Count)];
        if (party is null)
        {
            return "Enemy side has no parties";
        }

        var troops = party.Troops;
        if (troops is null || troops.Count() == 0)
        {
            return "Enemy party has no troops";
        }

        var entries = troops._elementDictionary.ToArray();

        if (entries.Length == 0)
        {
            return "Enemy party has no troops";
        }

        var randomEntry = entries[MBRandom.RandomInt(entries.Length)];

        UniqueTroopDescriptor descriptor = randomEntry.Key;
        FlattenedTroopRosterElement troopElement = randomEntry.Value;

        try
        {
            enemySide.OnTroopKilled(descriptor);
        }
        catch (Exception ex)
        {
            return $"Failed to kill random troop: {ex.Message}";
        }

        return $"Killed random troop: {troopElement.Troop?.Name}";
    }

    /// <summary>
    /// Kills all but one troop from the enemy side of the current map event.
    /// </summary>
    [CommandLineArgumentFunction("kill_all_but_one", "coop.debug.mapevent")]
    public static string KillAllButOneTroop(List<string> args)
    {
        var mapEvent = MobileParty.MainParty.MapEvent;
        if (mapEvent is null)
        {
            return "Main party is not in a map event";
        }

        var mainPartySide = MobileParty.MainParty.MapEventSide;
        if (mainPartySide is null)
        {
            return "Main party has no map event side";
        }

        var enemySide = mapEvent._sides
            .SingleOrDefault(side => side != mainPartySide);

        if (enemySide is null)
        {
            return "Failed to get enemy map event side";
        }

        if (enemySide.Parties is null || enemySide.Parties.Count == 0)
        {
            return "Enemy side has no parties";
        }

        var allTroops = new List<(MapEventParty Party, UniqueTroopDescriptor Descriptor, FlattenedTroopRosterElement Element)>();

        foreach (var party in enemySide.Parties)
        {
            if (party?.Troops?._elementDictionary is null)
                continue;

            foreach (var entry in party.Troops._elementDictionary)
            {
                var descriptor = entry.Key;
                var element = entry.Value;

                allTroops.Add((party, descriptor, element));
            }
        }

        if (allTroops.Count == 0)
        {
            return "Enemy side has no troops";
        }

        if (allTroops.Count == 1)
        {
            return $"Enemy side already has only one troop: {allTroops[0].Element.Troop?.Name}";
        }

        var survivorIndex = MBRandom.RandomInt(allTroops.Count);
        var survivor = allTroops[survivorIndex];

        var killedCount = 0;

        for (var i = 0; i < allTroops.Count; i++)
        {
            if (i == survivorIndex)
                continue;

            try
            {
                enemySide.OnTroopKilled(allTroops[i].Descriptor);
                killedCount++;
            }
            catch (Exception ex)
            {

            }
        }

        return $"Killed {killedCount} troops. Survivor: {survivor.Element.Troop?.Name}";
    }

    /// <summary>
    /// Lists the fields and properties of the current PlayerEncounter.
    /// </summary>
    [CommandLineArgumentFunction("list_player_encounter", "coop.debug.mapevent")]
    public static string ListPlayerEncounter(List<string> args)
    {
        var playerEncounter = PlayerEncounter.Current;
        if (playerEncounter == null)
        {
            return "No current PlayerEncounter";
        }

        var sb = new StringBuilder();

        sb.AppendLine("PlayerEncounter:");
        AppendObjectDetails(sb, playerEncounter, "\t", "PlayerEncounter Details");

        var result = sb.ToString();

        Logger.Debug("{PlayerEncounter}", result);

        return result;
    }

    /// <summary>
    /// Prints a compact, teardown-focused snapshot of the current <see cref="PlayerEncounter"/> and the main
    /// party's map-event state. Run on each client after a battle to spot an encounter that did not tear down —
    /// e.g. PlayerEncounter.Current still PRESENT, or MainParty.MapEvent lingering on an already-finalized event.
    /// Unlike <c>list_player_encounter</c> (full reflection dump) this is short enough to diff across clients.
    /// </summary>
    [CommandLineArgumentFunction("encounter_state", "coop.debug.mapevent")]
    public static string EncounterState(List<string> args)
    {
        TryGetObjectManager(out var objectManager);

        var sb = new StringBuilder();

        var encounter = PlayerEncounter.Current;
        sb.AppendLine($"PlayerEncounter.Current: {(encounter == null ? "<null> (torn down)" : "PRESENT")}");
        if (encounter != null)
        {
            sb.AppendLine($"\tBattle:           {FormatMapEvent(PlayerEncounter.Battle, objectManager)}");
            sb.AppendLine($"\t_mapEvent:        {FormatMapEvent(encounter._mapEvent, objectManager)}");
            sb.AppendLine($"\tEncounteredParty: {FormatPartyBaseWithId(PlayerEncounter.EncounteredParty, objectManager)}");
            sb.AppendLine($"\t_attackerParty:   {FormatPartyBaseWithId(encounter._attackerParty, objectManager)}");
            sb.AppendLine($"\t_defenderParty:   {FormatPartyBaseWithId(encounter._defenderParty, objectManager)}");
        }

        var mainParty = MobileParty.MainParty;
        sb.AppendLine($"MainParty.MapEvent:      {FormatMapEvent(mainParty?.MapEvent, objectManager)}");

        var side = mainParty?.Party?.MapEventSide;
        if (side == null)
            sb.AppendLine("MainParty.MapEventSide:  <null>");
        else
            sb.AppendLine($"MainParty.MapEventSide:  leader={FormatPartyBaseWithId(side.LeaderParty, objectManager)} mainPartyIsLeader={side.LeaderParty == mainParty?.Party}");

        sb.AppendLine($"CurrentMenu:             {Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "<none>"}");
        sb.AppendLine($"CurrentBattleSimulation: {(PlayerEncounter.CurrentBattleSimulation == null ? "<null>" : "PRESENT")}");
        sb.AppendLine($"MissionState.Current:    {(MissionState.Current == null ? "<null>" : "PRESENT")}");

        var result = sb.ToString();
        Logger.Debug("{EncounterState}", result);
        return result;
    }

    /// <summary>Shows, closes, or reports the live retreat confirmation for automated battle-exit testing.</summary>
    [CommandLineArgumentFunction("retreat_confirmation", "coop.debug.mapevent")]
    public static string RetreatConfirmation(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client in a battle mission.";

        if (args.Count != 1)
            return "Usage: coop.debug.mapevent.retreat_confirmation <show|accept|cancel|state>";

        var handler = Mission.Current?.GetMissionBehavior<BasicMissionHandler>();
        if (handler == null)
            return "No active battle retreat handler.";

        switch (args[0].ToLowerInvariant())
        {
            case "show":
                if (handler.IsWarningWidgetOpened)
                    return "Retreat confirmation already open: true";

                handler.CreateWarningWidgetForResult(BattleEndLogic.ExitResult.NeedsPlayerConfirmation);
                return $"Retreat confirmation open: {handler.IsWarningWidgetOpened}";
            case "accept":
                if (!handler.IsWarningWidgetOpened)
                    return "Retreat confirmation is not open.";

                InformationManager.HideInquiry();
                handler.OnEventAcceptSelectionWidget();
                return "Retreat confirmation accepted.";
            case "cancel":
                if (!handler.IsWarningWidgetOpened)
                    return "Retreat confirmation is not open.";

                InformationManager.HideInquiry();
                handler.OnEventCancelSelectionWidget();
                return $"Retreat confirmation open: {handler.IsWarningWidgetOpened}";
            case "state":
                return $"Retreat confirmation open: {handler.IsWarningWidgetOpened}";
            default:
                return "Usage: coop.debug.mapevent.retreat_confirmation <show|accept|cancel|state>";
        }
    }

    /// <summary>Closes the current encounter conversation so vanilla can advance to battle choices.</summary>
    [CommandLineArgumentFunction("complete_encounter_meeting", "coop.debug.mapevent")]
    public static string CompleteEncounterMeeting(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client at the encounter meeting.";

        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.complete_encounter_meeting";

        if (Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId != "encounter_meeting")
            return "The encounter meeting is not active.";

        var conversationManager = Campaign.Current.ConversationManager;
        if (!conversationManager.IsConversationInProgress)
            return "The encounter conversation is not active.";

        conversationManager.EndConversation();
        return "Encounter meeting completed.";
    }

    /// <summary>Runs the encounter menu's mission or simulation consequence for automated battle testing.</summary>
    [CommandLineArgumentFunction("choose_battle_mode", "coop.debug.mapevent")]
    public static string ChooseBattleMode(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client at the encounter menu.";

        if (args.Count != 1)
            return "Usage: coop.debug.mapevent.choose_battle_mode <mission|simulation>";

        if (PlayerEncounter.Current == null)
            return "No active player encounter.";

        var behavior = Campaign.Current?.GetCampaignBehavior<EncounterGameMenuBehavior>();
        if (behavior == null)
            return "Encounter menu behavior is unavailable.";

        switch (args[0].ToLowerInvariant())
        {
            case "mission":
                behavior.game_menu_encounter_attack_on_consequence(null);
                return "Mission battle requested.";
            case "simulation":
                behavior.game_menu_encounter_order_attack_on_consequence(null);
                return "Battle simulation requested.";
            default:
                return "Usage: coop.debug.mapevent.choose_battle_mode <mission|simulation>";
        }
    }

    private static string FormatMapEvent(MapEvent mapEvent, IObjectManager objectManager)
    {
        if (mapEvent == null) return "<null>";

        var id = "<no id>";
        if (!mapEvent.IsFinalized && objectManager != null && objectManager.TryGetId(mapEvent, out var resolved))
            id = resolved;

        return $"id={id} finalized={mapEvent.IsFinalized} state={mapEvent.BattleState} winner={mapEvent.WinningSide}";
    }

    [CommandLineArgumentFunction("get_events", "coop.debug.mapevent")]
    public static string GetEvents(List<string> args)
    {
        var sb = new StringBuilder();

        if(!TryGetObjectManager(out var objectManager))
        {
            return "Failed to get object manager";
        }

        foreach(var mapEvent in Campaign.Current.MapEventManager.MapEvents)
        {
            if (objectManager.TryGetIdWithLogging(mapEvent, out var id))
            {
                sb.AppendLine($"Map event id: {id}");
            }

            var partyNames = mapEvent.AttackerSide.Parties?
                .Select(party => party?.Party?.Name?.ToString() ?? "<null>")
                .ToArray() ?? Array.Empty<string>();
            sb.AppendLine($"\tAttacker: {string.Join(",", FormatSideNames(mapEvent.AttackerSide))}");
            sb.AppendLine($"\tDefender: {string.Join(",", FormatSideNames(mapEvent.DefenderSide))}");
        }

        return sb.ToString();
    }

    private static string[] FormatSideNames(MapEventSide side)
    {
        if (side == null)
            return new string[] { "<null>" };

        return side.Parties?
            .Select(party => party?.Party?.Name?.ToString() ?? "<null>")
            .ToArray() ?? Array.Empty<string>();
    }

    [CommandLineArgumentFunction("get_event", "coop.debug.mapevent")]
    public static string GetEvent(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.mapevent.get_event <mapEventId>";
        }

        if (!TryGetObjectManager(out var objectManager))
        {
            return "Failed to get object manager";
        }

        var mapEventId = args[0];

        if (!objectManager.TryGetObjectWithLogging<MapEvent>(mapEventId, out var mapEvent))
        {
            return $"Failed to find MapEvent with id: {mapEventId}";
        }

        var sb = new StringBuilder();

        sb.AppendLine($"Map event id: {mapEventId}");
        sb.AppendLine();

        AppendMapEventSummary(sb, mapEvent);
        sb.AppendLine();

        var result = sb.ToString();

        Logger.Debug("{MapEvent}", result);

        return result;
    }

    private static void AppendMapEventSummary(StringBuilder sb, MapEvent mapEvent)
    {
        sb.AppendLine("Summary:");

        AppendSideSummary(sb, "Attacker", mapEvent.AttackerSide);
        AppendSideSummary(sb, "Defender", mapEvent.DefenderSide);
    }

    private static void AppendSideSummary(StringBuilder sb, string sideName, MapEventSide side)
    {
        if (side == null)
        {
            sb.AppendLine($"\t{sideName}: <null>");
            return;
        }

        sb.AppendLine($"\t{sideName}: {string.Join(", ", FormatSideNames(side))}");

        AppendObjectDetails(sb, side, "\t\t", "Side Details");

        sb.AppendLine("\t\tParties:");

        var parties = side.Parties;
        if (parties == null)
        {
            sb.AppendLine("\t\t\t<null>");
            return;
        }

        var index = 0;
        foreach (var party in parties)
        {
            sb.AppendLine($"\t\t\tParty[{index}]:");

            if (party == null)
            {
                sb.AppendLine("\t\t\t\t<null>");
            }
            else
            {
                AppendMapEventPartyDetails(sb, party, "\t\t\t\t");
            }

            index++;
        }
    }
    private static void AppendMapEventPartyDetails(StringBuilder sb, MapEventParty party, string indent)
    {
        var partyName = party.Party?.Name?.ToString() ?? "<null>";
        sb.AppendLine($"{indent}Party: {partyName}");

        AppendObjectDetails(sb, party, indent, "MapEventParty Details");
    }

    private static void AppendObjectDetails(StringBuilder sb, object obj, string indent, string title)
    {
        if (obj == null)
        {
            sb.AppendLine($"{indent}{title}: <null>");
            return;
        }

        var type = obj.GetType();

        sb.AppendLine($"{indent}{title}: {GetFriendlyTypeName(type)}");

        AppendFields(sb, obj, type, indent + "\t");
        AppendProperties(sb, obj, type, indent + "\t");
    }

    private static void AppendFields(StringBuilder sb, object obj, Type type, string indent)
    {
        sb.AppendLine($"{indent}Fields:");

        var fields = type.GetFields(
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic);

        if (fields.Length == 0)
        {
            sb.AppendLine($"{indent}\t<none>");
            return;
        }

        foreach (var field in fields.OrderBy(f => f.Name))
        {
            object value;

            try
            {
                value = field.GetValue(obj);
            }
            catch (Exception ex)
            {
                sb.AppendLine($"{indent}\t{field.Name}: <failed: {ex.GetType().Name}>");
                continue;
            }

            sb.AppendLine($"{indent}\t{field.Name}: {FormatValue(value)}");
        }
    }

    private static void AppendProperties(StringBuilder sb, object obj, Type type, string indent)
    {
        sb.AppendLine($"{indent}Properties:");

        var properties = type.GetProperties(
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic);

        if (properties.Length == 0)
        {
            sb.AppendLine($"{indent}\t<none>");
            return;
        }

        foreach (var property in properties.OrderBy(p => p.Name))
        {
            if (property.GetIndexParameters().Length != 0)
            {
                sb.AppendLine($"{indent}\t{property.Name}: <indexed property>");
                continue;
            }

            object value;

            try
            {
                value = property.GetValue(obj, null);
            }
            catch (Exception ex)
            {
                sb.AppendLine($"{indent}\t{property.Name}: <failed: {ex.GetType().Name}>");
                continue;
            }

            sb.AppendLine($"{indent}\t{property.Name}: {FormatValue(value)}");
        }
    }

    private static string FormatValue(object value)
    {
        if (value == null)
            return "<null>";

        if (value is string str)
            return str;

        if (value is TextObject textObject)
            return textObject.ToString();

        if (value is CharacterObject character)
            return FormatCharacter(character);

        if (value is MobileParty mobileParty)
            return FormatMobileParty(mobileParty);

        if (value is PartyBase partyBase)
            return FormatPartyBase(partyBase);

        if (value is IFaction faction)
            return faction.Name?.ToString() ?? faction.StringId ?? "<unnamed faction>";

        if (value is UniqueTroopDescriptor descriptor)
            return descriptor.ToString();

        if (value is IEnumerable enumerable && !(value is string))
            return FormatEnumerable(enumerable);

        return value.ToString();
    }

    private static string FormatEnumerable(IEnumerable enumerable)
    {
        var values = new List<string>();
        var count = 0;

        foreach (var item in enumerable)
        {
            if (count >= 20)
            {
                values.Add("...");
                break;
            }

            values.Add(FormatValue(item));
            count++;
        }

        return "[" + string.Join(", ", values) + "]";
    }

    private static string FormatCharacter(CharacterObject character)
    {
        if (character == null)
            return "<null>";

        var id = character.StringId ?? "<no id>";
        var name = character.Name?.ToString() ?? "<no name>";

        return $"{name} ({id})";
    }

    private static string FormatMobileParty(MobileParty party)
    {
        if (party == null)
            return "<null>";

        var id = party.StringId ?? "<no id>";
        var name = party.Name?.ToString() ?? "<no name>";

        return $"{name} ({id})";
    }

    private static string FormatPartyBase(PartyBase party)
    {
        if (party == null)
            return "<null>";

        var name = party.Name?.ToString() ?? "<no name>";

        return name;
    }

    private static string FormatPartyBaseWithId(PartyBase party, IObjectManager objectManager)
    {
        if (party == null)
            return "<null>";

        var partyBaseId = objectManager != null && objectManager.TryGetId(party, out string resolvedPartyBaseId)
            ? resolvedPartyBaseId
            : "<unregistered>";

        return $"{FormatPartyBase(party)} (PartyBase id {partyBaseId})";
    }

    private static string GetFriendlyTypeName(Type type)
    {
        if (type == null)
            return "<null>";

        if (!type.IsGenericType)
            return type.FullName ?? type.Name;

        var genericTypeName = type.GetGenericTypeDefinition().FullName ?? type.Name;
        var tickIndex = genericTypeName.IndexOf('`');

        if (tickIndex >= 0)
            genericTypeName = genericTypeName.Substring(0, tickIndex);

        var genericArguments = type.GetGenericArguments()
            .Select(GetFriendlyTypeName)
            .ToArray();

        return genericTypeName + "<" + string.Join(", ", genericArguments) + ">";
    }
}
