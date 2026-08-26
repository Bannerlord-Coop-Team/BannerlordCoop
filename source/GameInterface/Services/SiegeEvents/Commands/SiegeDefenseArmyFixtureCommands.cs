#if DEBUG
using Common;
using Common.Messaging;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Settlements.Interfaces;
using GameInterface.Services.SiegeEvents.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.SiegeEvents.Commands;

internal static class SiegeDefenseArmyFixtureCommands
{
    private static FixtureState fixture;

    [CommandLineArgumentFunction("defense_army_fixture_state", "coop.debug.siege")]
    public static string FixtureStateCommand(List<string> args)
    {
        if (args.Count != 2)
            return "Usage: coop.debug.siege.defense_army_fixture_state <controllerId> <settlementId>";

        if (!TryResolveContext(args[0], args[1], out var playerParty, out var settlement, out var error))
            return error;

        return FormatState("Siege defense army fixture state", playerParty, settlement, new
        {
            success = true,
            fixtureActive = fixture != null,
            fixtureStaged = fixture?.Staged == true,
            fixtureRestored = fixture?.Restored == true,
            fixtureToken = fixture?.Token,
        });
    }

    [CommandLineArgumentFunction("capture_defense_army_fixture", "coop.debug.siege")]
    public static string CaptureFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "This command can only be used by the server";
        if (args.Count != 2)
            return "Usage: coop.debug.siege.capture_defense_army_fixture <controllerId> <settlementId>";
        if (fixture != null)
            return "The siege defense army fixture is already active";
        if (!TryResolveContext(args[0], args[1], out var playerParty, out var settlement, out var error))
            return error;
        if (!TryValidateCleanParty(playerParty, out error))
            return error;
        if (settlement.SiegeEvent != null || settlement.Party.MapEvent != null)
            return $"{settlement.Name} already has an active siege or map event";
        if (playerParty.MapFaction is not Kingdom kingdom || settlement.MapFaction != kingdom)
            return $"{playerParty.Name} and {settlement.Name} must belong to the same kingdom";
        if (playerParty.LeaderHero == null)
            return $"{playerParty.Name} has no leader hero";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)
            || !ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
            return "Unable to resolve the fixture snapshot services";

        var followers = MobileParty.AllLordParties
            .Where(party => party != playerParty
                && party.MapFaction == kingdom
                && party.LeaderHero != null
                && !party.IsPlayerParty()
                && IsCleanParty(party)
                && objectManager.TryGetId(party, out _))
            .OrderByDescending(party => party.Party.CalculateCurrentStrength())
            .Take(2)
            .ToArray();
        if (followers.Length != 2)
            return $"Only found {followers.Length} clean allied lord parties; need 2";

        var besieger = MobileParty.AllLordParties
            .Where(party => party != playerParty
                && party.LeaderHero != null
                && !party.IsPlayerParty()
                && IsCleanParty(party)
                && party.MapFaction?.IsAtWarWith(kingdom) == true
                && party.MapFaction?.IsAtWarWith(settlement.MapFaction) == true
                && objectManager.TryGetId(party, out _))
            .OrderByDescending(party => party.Party.CalculateCurrentStrength())
            .FirstOrDefault();
        if (besieger == null)
            return "No clean hostile AI lord party is available to besiege Danustica";

        var capturedParties = followers
            .Prepend(besieger)
            .Prepend(playerParty)
            .Select(party => CaptureParty(party, behaviorSnapshot))
            .ToArray();
        if (capturedParties.Any(snapshot => snapshot == null))
            return "Unable to capture the original movement state for every fixture party";

        fixture = new FixtureState(
            Guid.NewGuid().ToString("N"),
            args[0],
            settlement,
            capturedParties[0],
            capturedParties[1],
            capturedParties.Skip(2).ToArray());

        return FormatState("Captured siege defense army fixture", playerParty, settlement, new
        {
            success = true,
            fixtureToken = fixture.Token,
            controllerId = fixture.ControllerId,
            settlementId = settlement.StringId,
            playerPartyId = GetId(objectManager, playerParty),
            besiegerPartyId = GetId(objectManager, besieger),
            followerPartyIds = followers.Select(party => GetId(objectManager, party)).ToArray(),
            playerBehavior = GetBehaviorProof(fixture.Player.Behavior),
        });
    }

    [CommandLineArgumentFunction("stage_defense_army_fixture", "coop.debug.siege")]
    public static string StageFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "This command can only be used by the server";
        if (args.Count != 3 || !int.TryParse(args[2], out int armyPartyCount) || armyPartyCount != 3)
            return "Usage: coop.debug.siege.stage_defense_army_fixture <controllerId> <settlementId> 3";
        if (fixture == null)
            return "Capture the siege defense army fixture before staging it";
        if (fixture.Staged)
            return "The siege defense army fixture has already been staged";
        if (fixture.ControllerId != args[0] || fixture.Settlement.StringId != args[1])
            return "The requested controller or settlement does not match the captured fixture";

        var playerParty = fixture.Player.Party;
        var settlement = fixture.Settlement;
        if (!TryValidateCleanParty(playerParty, out var error))
            return error;
        if (settlement.SiegeEvent != null || settlement.Party.MapEvent != null)
            return $"{settlement.Name} is no longer clean for the fixture";
        if (playerParty.MapFaction is not Kingdom kingdom || settlement.MapFaction != kingdom)
            return $"{playerParty.Name} and {settlement.Name} no longer belong to the same kingdom";

        try
        {
            kingdom.CreateArmy(playerParty.LeaderHero, settlement, Army.ArmyTypes.Defender);
            fixture.Army = playerParty.Army;
            if (fixture.Army == null || fixture.Army.LeaderParty != playerParty)
                throw new InvalidOperationException("The player-led fixture army could not be created.");

            var playerPosition = new CampaignVec2(
                new Vec2(settlement.GatePosition.X, settlement.GatePosition.Y - 1.5f),
                true);
            StageAtHold(playerParty, playerPosition);
            foreach (var follower in fixture.Followers)
            {
                StageAtHold(follower.Party, playerPosition);
                follower.Party.Army = fixture.Army;
                fixture.Army.AddPartyToMergedParties(follower.Party);
            }

            var besieger = fixture.Besieger.Party;
            StageAtHold(besieger, settlement.GatePosition);
            besieger.SetMoveBesiegeSettlement(settlement, MobileParty.NavigationType.Default);
            Campaign.Current.SiegeEventManager.StartSiegeEvent(settlement, besieger);
            fixture.SiegeEvent = settlement.SiegeEvent;
            if (settlement.SiegeEvent?.BesiegerCamp?.LeaderParty != besieger)
                throw new InvalidOperationException("The hostile AI siege could not be created.");

            StartBattleAction.ApplyStartAssaultAgainstWalls(besieger, settlement);
            fixture.MapEvent = settlement.Party.MapEvent;
            if (settlement.Party.MapEvent?.IsSiegeAssault != true)
                throw new InvalidOperationException("The hostile AI wall assault could not be started.");

            fixture.Staged = true;
            return FormatState("Staged siege defense army fixture", playerParty, settlement, new
            {
                success = true,
                fixtureToken = fixture.Token,
                armyPartyCount = fixture.Army.Parties.Count,
                armyLeaderPartyId = playerParty.StringId,
                siegeAssault = true,
            });
        }
        catch (Exception exception)
        {
            try
            {
                RestoreFixtureState();
                fixture = null;
            }
            catch (Exception restoreException)
            {
                return $"Fixture staging failed: {exception.Message}. Rollback failed: {restoreException.Message}";
            }

            return $"Fixture staging failed: {exception.Message}. The captured state was restored";
        }
    }

    [CommandLineArgumentFunction("open_defender_encounter", "coop.debug.siege")]
    public static string OpenDefenderEncounter(List<string> args)
    {
        if (ModInformation.IsServer)
            return "This command can only be used by a client";
        if (args.Count != 1)
            return "Usage: coop.debug.siege.open_defender_encounter <settlementId>";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)
            || !ContainerProvider.TryResolve<ISettlementInterface>(out var settlementInterface)
            || !objectManager.TryGetObject<Settlement>(args[0], out var settlement))
            return $"Unable to resolve the settlement encounter for {args[0]}";

        var mapEvent = settlement.Party.MapEvent;
        if (mapEvent?.IsSiegeAssault != true)
            return $"{settlement.Name} does not have an active siege assault";
        if (!mapEvent.CanPartyJoinBattle(PartyBase.MainParty, BattleSideEnum.Defender))
            return $"The local player party cannot join the defenders at {settlement.Name}";

        settlementInterface.StartSettlementEncounter(MobileParty.MainParty, settlement);
        if (PlayerEncounter.Current == null)
            return $"Unable to start the local defender encounter at {settlement.Name}";

        return FormatClientState("Opened the defender encounter", settlement, new
        {
            success = true,
            requestedSide = BattleSideEnum.Defender.ToString(),
            menu = Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId,
        });
    }

    [CommandLineArgumentFunction("invoke_defender_join", "coop.debug.siege")]
    public static string InvokeDefenderJoin(List<string> args)
    {
        if (ModInformation.IsServer)
            return "This command can only be used by a client";
        if (args.Count != 1)
            return "Usage: coop.debug.siege.invoke_defender_join <settlementId>";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)
            || !objectManager.TryGetObject<Settlement>(args[0], out var settlement))
            return $"Settlement with id {args[0]} not found";
        if (PlayerEncounter.Current == null || PlayerEncounter.EncounteredBattle != settlement.Party.MapEvent)
            return $"The local player does not have the staged defender encounter at {settlement.Name}";

        new EncounterGameMenuBehavior()
            .game_menu_join_encounter_help_defenders_on_consequence(null);
        return FormatClientState("Invoked the defender join", settlement, new
        {
            success = true,
            requestedSide = BattleSideEnum.Defender.ToString(),
            joinedBattle = PlayerEncounter.Current?.IsJoinedBattle == true,
        });
    }

    [CommandLineArgumentFunction("defense_army_state", "coop.debug.siege")]
    public static string DefenseArmyState(List<string> args)
    {
        if (args.Count < 3 || args.Count > 4)
            return "Usage: coop.debug.siege.defense_army_state <controllerId> <settlementId> <baseline|joined|restored> [capturedState]";
        if (!TryResolveContext(args[0], args[1], out var playerParty, out var settlement, out var error))
            return error;

        bool success;
        bool playerBehaviorRestored = false;
        switch (args[2])
        {
            case "baseline":
                if (args.Count != 3)
                    return "The baseline state does not accept captured fixture state";
                success = settlement.Party.MapEvent?.IsSiegeAssault == true
                    && playerParty.MapEvent == null
                    && playerParty.Army?.LeaderParty == playerParty
                    && playerParty.Army.Parties.Count == 3;
                break;
            case "joined":
                if (args.Count != 3)
                    return "The joined state does not accept captured fixture state";
                success = playerParty.MapEvent == settlement.Party.MapEvent
                    && ReferenceEquals(playerParty.MapEvent?.DefenderSide, playerParty.Party.MapEventSide)
                    && playerParty.Army?.LeaderParty == playerParty
                    && playerParty.Army.Parties.Count == 3;
                break;
            case "restored":
                if (args.Count != 4)
                    return "The restored state requires the captured fixture state";
                if (!TryGetCapturedPlayerBehavior(args[3], out var expectedBehavior, out error))
                    return error;
                if (ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot)
                    && behaviorSnapshot.TryCreate(playerParty, out var actualBehavior))
                {
                    playerBehaviorRestored = JToken.DeepEquals(
                        expectedBehavior,
                        JToken.FromObject(GetBehaviorProof(actualBehavior)));
                }
                success = settlement.SiegeEvent == null
                    && settlement.Party.MapEvent == null
                    && playerParty.MapEvent == null
                    && playerParty.Army == null
                    && (!ModInformation.IsClient || PlayerEncounter.Current == null)
                    && playerBehaviorRestored;
                break;
            default:
                return $"Unknown defense army state '{args[2]}'";
        }

        return FormatState($"Siege defense army {args[2]} state", playerParty, settlement, new
        {
            expectedState = args[2],
            playerBehaviorRestored,
        }, success);
    }

    [CommandLineArgumentFunction("restore_defense_army_fixture", "coop.debug.siege")]
    public static string RestoreFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "This command can only be used by the server";
        if (!TryValidateToken(args, out var error))
            return error;

        try
        {
            RestoreFixtureState();
            fixture.Restored = true;
            return FormatState("Restored siege defense army fixture", fixture.Player.Party, fixture.Settlement, new
            {
                success = true,
                fixtureToken = fixture.Token,
                restored = true,
            });
        }
        catch (Exception exception)
        {
            return $"Failed to restore the siege defense army fixture: {exception.Message}";
        }
    }

    [CommandLineArgumentFunction("verify_defense_army_fixture", "coop.debug.siege")]
    public static string VerifyFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "This command can only be used by the server";
        if (!TryValidateToken(args, out var error))
            return error;
        if (!fixture.Restored)
            return "Restore the siege defense army fixture before verifying it";
        if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
            return "Unable to resolve the fixture snapshot service";

        bool partiesRestored = fixture.AllParties.All(snapshot =>
            snapshot.Party.Army == null
            && snapshot.Party.MapEvent == null
            && TryVerifyParty(behaviorSnapshot, snapshot));
        bool settlementRestored = fixture.Settlement.SiegeEvent == null
            && fixture.Settlement.Party.MapEvent == null;
        bool success = partiesRestored && settlementRestored;
        var result = FormatState(
            "Verified siege defense army fixture restoration",
            fixture.Player.Party,
            fixture.Settlement,
            new
            {
                fixtureToken = fixture.Token,
                partiesRestored,
                settlementRestored,
            },
            success);
        if (success)
            fixture = null;

        return result;
    }

    private static void RestoreFixtureState()
    {
        if (fixture == null)
            throw new InvalidOperationException("The siege defense army fixture is not active.");
        if (!ContainerProvider.TryResolve<ISiegeEventInterface>(out var siegeEventInterface)
            || !ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
            throw new InvalidOperationException("Unable to resolve the fixture restoration services.");

        if (fixture.MapEvent != null && !fixture.MapEvent.IsFinalized)
        {
            // Route fixture cleanup through the normal finalization path so joined players receive the encounter close.
            MessageBroker.Instance.Publish(fixture.MapEvent, new MapEventFinalizeAttempted(fixture.MapEvent));
            if (!fixture.MapEvent.IsFinalized)
                throw new InvalidOperationException("Unable to finalize the siege defense army fixture map event.");
        }

        var settlement = fixture.Settlement;
        if (ReferenceEquals(settlement.SiegeEvent, fixture.SiegeEvent))
        {
            var camp = fixture.SiegeEvent?.BesiegerCamp;
            if (camp != null)
            {
                foreach (var party in camp._besiegerParties.ToArray())
                    siegeEventInterface.BreakSiege(party);
            }
        }

        if (fixture.Army != null && fixture.Army.Kingdom != null)
            DisbandArmyAction.ApplyByObjectiveFinished(fixture.Army);

        foreach (var snapshot in fixture.AllParties)
        {
            if (!behaviorSnapshot.TryApply(snapshot.Party, snapshot.Behavior, out _))
                throw new InvalidOperationException($"Unable to restore movement state for {snapshot.Party.StringId}.");

            snapshot.Party.Position = snapshot.Behavior.PartyPosition;
            PublishForcedPosition(snapshot.Party);
        }
    }

    private static bool TryResolveContext(
        string controllerId,
        string settlementId,
        out MobileParty playerParty,
        out Settlement settlement,
        out string error)
    {
        playerParty = null;
        settlement = null;
        error = null;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)
            || !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
        {
            error = "Unable to resolve the siege defense fixture services";
            return false;
        }
        if (!playerManager.TryGetPlayer(controllerId, out var player)
            || !objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out playerParty))
        {
            error = $"Unable to resolve connected player {controllerId}";
            return false;
        }
        if (!objectManager.TryGetObject<Settlement>(settlementId, out settlement))
        {
            error = $"Settlement with id {settlementId} not found";
            return false;
        }

        return true;
    }

    private static bool TryValidateCleanParty(MobileParty party, out string error)
    {
        if (IsCleanParty(party))
        {
            error = null;
            return true;
        }

        error = $"{party.Name} must be active on the campaign map and outside an army, settlement, siege, or map event";
        return false;
    }

    private static bool IsCleanParty(MobileParty party) =>
        party?.IsActive == true
        && party.Army == null
        && party.CurrentSettlement == null
        && party.BesiegerCamp == null
        && party.MapEvent == null
        && !party.IsTransitionInProgress;

    private static PartySnapshot CaptureParty(
        MobileParty party,
        IMobilePartyBehaviorSnapshot behaviorSnapshot)
    {
        return behaviorSnapshot.TryCreate(party, out var behavior)
            ? new PartySnapshot(party, behavior)
            : null;
    }

    private static void StageAtHold(MobileParty party, CampaignVec2 position)
    {
        party.Position = position;
        party.SetMoveModeHold();
        party.SetNavigationModeHold();
        PublishForcedPosition(party);
    }

    private static void PublishForcedPosition(MobileParty party) =>
        MessageBroker.Instance.Publish(
            typeof(SiegeDefenseArmyFixtureCommands),
            new PartyBehaviorChangeAttempted(
                party,
                forcePosition: true,
                isCurrentlyAtSea: party.IsCurrentlyAtSea));

    private static bool TryVerifyParty(
        IMobilePartyBehaviorSnapshot behaviorSnapshot,
        PartySnapshot expected)
    {
        if (!behaviorSnapshot.TryCreate(expected.Party, out var actual))
            return false;

        var baseline = expected.Behavior;
        return actual.MobilePartyId == baseline.MobilePartyId
            && actual.NewAiBehavior == baseline.NewAiBehavior
            && actual.InteractablePointId == baseline.InteractablePointId
            && actual.BestTargetPoint == baseline.BestTargetPoint
            && actual.PartyPosition == baseline.PartyPosition
            && actual.DefaultBehavior == baseline.DefaultBehavior
            && actual.TargetPosition == baseline.TargetPosition
            && actual.DesiredAiNavigationType == baseline.DesiredAiNavigationType
            && actual.TargetPartyId == baseline.TargetPartyId
            && actual.TargetSettlementId == baseline.TargetSettlementId
            && actual.MoveTargetPoint == baseline.MoveTargetPoint
            && actual.IsTargetingPort == baseline.IsTargetingPort
            && actual.PartyMoveMode == baseline.PartyMoveMode
            && actual.MoveTargetPartyId == baseline.MoveTargetPartyId
            && actual.IsInteractableAnchor == baseline.IsInteractableAnchor
            && actual.IsCurrentlyAtSea == baseline.IsCurrentlyAtSea;
    }

    private static bool TryValidateToken(List<string> args, out string error)
    {
        error = null;
        if (fixture == null)
        {
            error = "The siege defense army fixture is not active";
            return false;
        }
        if (args.Count != 1)
        {
            error = "Expected the captured siege defense fixture state JSON";
            return false;
        }

        try
        {
            var capturedState = JObject.Parse(args[0]);
            var token = capturedState.Value<string>("fixtureToken")
                ?? capturedState["extra"]?.Value<string>("fixtureToken");
            if (token != fixture.Token)
            {
                error = "The captured fixture token does not match the active fixture";
                return false;
            }
        }
        catch (JsonException)
        {
            error = "The captured siege defense fixture state is not valid JSON";
            return false;
        }

        return true;
    }

    private static bool TryGetCapturedPlayerBehavior(
        string capturedStateJson,
        out JToken expectedBehavior,
        out string error)
    {
        expectedBehavior = null;
        error = null;
        try
        {
            var capturedState = JObject.Parse(capturedStateJson);
            expectedBehavior = capturedState["extra"]?["playerBehavior"];
            if (expectedBehavior == null)
            {
                error = "The captured fixture state has no player behavior snapshot";
                return false;
            }

            return true;
        }
        catch (JsonException)
        {
            error = "The captured siege defense fixture state is not valid JSON";
            return false;
        }
    }

    private static object GetBehaviorProof(PartyBehaviorUpdateData behavior) => new
    {
        behavior.MobilePartyId,
        newAiBehavior = behavior.NewAiBehavior.ToString(),
        behavior.InteractablePointId,
        bestTargetPoint = GetPositionProof(behavior.BestTargetPoint),
        partyPosition = GetPositionProof(behavior.PartyPosition),
        defaultBehavior = behavior.DefaultBehavior.ToString(),
        targetPosition = GetPositionProof(behavior.TargetPosition),
        desiredAiNavigationType = behavior.DesiredAiNavigationType.ToString(),
        behavior.TargetPartyId,
        behavior.TargetSettlementId,
        moveTargetPoint = GetPositionProof(behavior.MoveTargetPoint),
        behavior.IsTargetingPort,
        partyMoveMode = behavior.PartyMoveMode.ToString(),
        behavior.MoveTargetPartyId,
        behavior.IsInteractableAnchor,
        behavior.IsCurrentlyAtSea,
    };

    private static object GetPositionProof(CampaignVec2 position) => new
    {
        position.X,
        position.Y,
        position.IsOnLand,
    };

    private static string FormatClientState(string label, Settlement settlement, object extra) =>
        FormatState(label, MobileParty.MainParty, settlement, extra);

    private static string FormatState(
        string label,
        MobileParty playerParty,
        Settlement settlement,
        object extra,
        bool success = true)
    {
        var mapEvent = playerParty?.MapEvent;
        var army = playerParty?.Army;
        var structuredState = new
        {
            success,
            label,
            extra,
            settlementId = settlement?.StringId,
            siegeActive = settlement?.SiegeEvent != null,
            siegeAssault = settlement?.Party.MapEvent?.IsSiegeAssault == true,
            playerPartyId = playerParty?.StringId,
            playerMapEvent = mapEvent != null,
            playerCanonicalSide = GetCanonicalSide(mapEvent, playerParty?.Party.MapEventSide)?.ToString(),
            playerMissionSide = playerParty?.Party.MapEventSide?.MissionSide.ToString(),
            playerOnDefenderSide = mapEvent != null
                && ReferenceEquals(mapEvent.DefenderSide, playerParty.Party.MapEventSide),
            armyLeaderPartyId = army?.LeaderParty?.StringId,
            armyPartyCount = army?.Parties.Count ?? 0,
            encounterActive = ModInformation.IsClient && PlayerEncounter.Current != null,
            menu = ModInformation.IsClient ? Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId : null,
        };

        return label + Environment.NewLine + "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(structuredState);
    }

    private static BattleSideEnum? GetCanonicalSide(MapEvent mapEvent, MapEventSide mapEventSide)
    {
        if (mapEvent == null || mapEventSide == null)
            return null;
        if (ReferenceEquals(mapEvent.DefenderSide, mapEventSide))
            return BattleSideEnum.Defender;
        if (ReferenceEquals(mapEvent.AttackerSide, mapEventSide))
            return BattleSideEnum.Attacker;
        return null;
    }

    private static string GetId(IObjectManager objectManager, object value) =>
        objectManager.TryGetId(value, out string id) ? id : null;

    private sealed class FixtureState
    {
        public string Token { get; }
        public string ControllerId { get; }
        public Settlement Settlement { get; }
        public PartySnapshot Player { get; }
        public PartySnapshot Besieger { get; }
        public PartySnapshot[] Followers { get; }
        public Army Army { get; set; }
        public SiegeEvent SiegeEvent { get; set; }
        public MapEvent MapEvent { get; set; }
        public bool Staged { get; set; }
        public bool Restored { get; set; }
        public IEnumerable<PartySnapshot> AllParties =>
            new[] { Player, Besieger }.Concat(Followers);

        public FixtureState(
            string token,
            string controllerId,
            Settlement settlement,
            PartySnapshot player,
            PartySnapshot besieger,
            PartySnapshot[] followers)
        {
            Token = token;
            ControllerId = controllerId;
            Settlement = settlement;
            Player = player;
            Besieger = besieger;
            Followers = followers;
        }
    }

    private sealed class PartySnapshot
    {
        public MobileParty Party { get; }
        public PartyBehaviorUpdateData Behavior { get; }

        public PartySnapshot(MobileParty party, PartyBehaviorUpdateData behavior)
        {
            Party = party;
            Behavior = behavior;
        }
    }
}
#endif
