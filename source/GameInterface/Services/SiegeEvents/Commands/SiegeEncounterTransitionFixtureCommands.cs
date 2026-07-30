using Autofac;
using Common;
using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.SiegeEvents.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.SiegeEvents.Commands;

internal class SiegeEncounterTransitionFixtureCommands
{
    private static SiegeEncounterTransitionFixture activeFixture;
    private static readonly Dictionary<string, FixtureRestoreReceipt> completedRestores = new();

    [CommandLineArgumentFunction("encounter_fixture_start_ai", "coop.debug.siege")]
    public static string StartAiFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 2)
            return "Usage: coop.debug.siege.encounter_fixture_start_ai <settlementId> <token>";

        if (!TryGetToken(args[1], out string token, out string tokenError))
            return tokenError;

        if (TryGetExistingFixture(token, out string existing))
            return existing;

        if (!TryResolveServices(
                out var objectManager,
                out _,
                out var behaviorSnapshot,
                out var siegeEventInterface))
        {
            return "Unable to resolve siege encounter fixture services.";
        }

        if (!objectManager.TryGetObject<Settlement>(args[0], out var settlement))
            return $"Settlement with id {args[0]} not found.";

        if (!IsOwnedFortification(settlement) || settlement.SiegeEvent != null)
            return $"{settlement.Name} must be an owned fortification without an active siege.";

        var besieger = MobileParty.AllLordParties
            .Where(party =>
                IsClean(party) &&
                !party.IsPlayerParty() &&
                party.LeaderHero != null &&
                party.MemberRoster.TotalHealthyCount > 0 &&
                party.MapFaction?.IsAtWarWith(settlement.MapFaction) == true)
            .OrderByDescending(party => party.Party.CalculateCurrentStrength())
            .FirstOrDefault();
        if (besieger == null)
            return $"No clean hostile AI lord party is available to besiege {settlement.Name}.";

        if (!behaviorSnapshot.TryCreate(besieger, out var behavior))
            return $"Unable to capture the movement state of {besieger.Name}.";

        var fixture = CreateFixture(token, besieger, behavior, settlement);
        activeFixture = fixture;

        try
        {
            besieger.Position = settlement.GatePosition;
            besieger.SetMoveBesiegeSettlement(settlement, MobileParty.NavigationType.Default);
            siegeEventInterface.StartSiegeEvent(besieger, settlement);
            if (settlement.SiegeEvent?.BesiegerCamp?.LeaderParty != besieger)
                throw new InvalidOperationException($"Failed to start the AI siege of {settlement.Name}.");

            return $"token={fixture.Token}|party={besieger.StringId}|settlement={settlement.StringId}";
        }
        catch (Exception exception)
        {
            bool restored = TryRestoreFixture(
                fixture,
                behaviorSnapshot,
                siegeEventInterface,
                out string restoreError);
            if (restored)
                activeFixture = null;

            return $"Fixture setup failed: {exception.Message}. " +
                $"Restore result: {(restored ? "restored" : restoreError)}. token={fixture.Token}";
        }
    }

    [CommandLineArgumentFunction("encounter_fixture_capture_player", "coop.debug.siege")]
    public static string CapturePlayerFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 3)
            return "Usage: coop.debug.siege.encounter_fixture_capture_player <controllerId> <settlementId> <token>";

        if (!TryGetToken(args[2], out string token, out string tokenError))
            return tokenError;

        if (TryGetExistingFixture(token, out string existing))
            return existing;

        if (!TryResolveServices(
                out var objectManager,
                out var playerManager,
                out var behaviorSnapshot,
                out _))
        {
            return "Unable to resolve siege encounter fixture services.";
        }

        if (!playerManager.TryGetPlayer(args[0], out var player) ||
            !playerManager.IsConnected(player) ||
            !objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var playerParty))
        {
            return $"Unable to resolve connected player {args[0]}.";
        }

        if (!objectManager.TryGetObject<Settlement>(args[1], out var settlement))
            return $"Settlement with id {args[1]} not found.";

        if (!IsOwnedFortification(settlement) || settlement.SiegeEvent != null)
            return $"{settlement.Name} must be an owned fortification without an active siege.";

        if (!IsClean(playerParty) || playerParty.LeaderHero == null)
            return $"{playerParty.Name} must be active, organized, unattached, and outside settlements, sieges, armies, and map events.";

        if (playerParty.MapFaction?.IsAtWarWith(settlement.MapFaction) != true)
            return $"{playerParty.Name} must already be hostile to {settlement.Name}.";

        if (!behaviorSnapshot.TryCreate(playerParty, out var behavior))
            return $"Unable to capture the movement state of {playerParty.Name}.";

        activeFixture = CreateFixture(token, playerParty, behavior, settlement);
        return $"token={activeFixture.Token}|party={playerParty.StringId}|settlement={settlement.StringId}";
    }

    [CommandLineArgumentFunction("encounter_fixture_restore", "coop.debug.siege")]
    public static string RestoreFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 1)
            return "Usage: coop.debug.siege.encounter_fixture_restore <token>";

        if (!TryGetToken(args[0], out string token, out string tokenError))
            return tokenError;

        if (activeFixture == null)
        {
            if (completedRestores.TryGetValue(token, out var receipt))
                return receipt.ToString();
            return "No siege encounter fixture is active.";
        }

        if (!string.Equals(activeFixture.Token, token, StringComparison.Ordinal))
            return $"Active siege encounter fixture token is {activeFixture.Token}, not {token}.";

        if (!TryResolveServices(
                out _,
                out _,
                out var behaviorSnapshot,
                out var siegeEventInterface))
        {
            return "Unable to resolve siege encounter fixture services.";
        }

        var fixture = activeFixture;
        if (!TryRestoreFixture(fixture, behaviorSnapshot, siegeEventInterface, out string error))
            return $"Unable to restore siege encounter fixture {fixture.Token}: {error}";

        var completedReceipt = new FixtureRestoreReceipt(fixture);
        completedRestores[fixture.Token] = completedReceipt;
        activeFixture = null;
        return completedReceipt.ToString();
    }

    [CommandLineArgumentFunction("encounter_fixture_status", "coop.debug.siege")]
    public static string FixtureStatus(List<string> args)
    {
        if (args.Count != 1)
            return "Usage: coop.debug.siege.encounter_fixture_status <token>";

        if (!TryGetToken(args[0], out string token, out string tokenError))
            return tokenError;

        if (activeFixture?.Token == token)
            return $"status=active|token={activeFixture.Token}|party={activeFixture.Party.StringId}|settlement={activeFixture.Settlement.StringId}";
        if (completedRestores.TryGetValue(token, out var receipt))
            return receipt.ToString();
        return $"status=missing|token={token}";
    }

    [CommandLineArgumentFunction("encounter_fixture_baseline", "coop.debug.siege")]
    public static string FixtureBaseline(List<string> args)
    {
        if (args.Count != 1)
            return "Usage: coop.debug.siege.encounter_fixture_baseline <token>";

        if (!TryGetToken(args[0], out string token, out string tokenError))
            return tokenError;

        if (activeFixture?.Token == token)
            return $"status=active|token={activeFixture.Token}|partyState={FormatPartyState(activeFixture, activeFixture.Behavior)}";
        if (completedRestores.TryGetValue(token, out var receipt))
            return $"status=restored|token={receipt.Token}|partyState={receipt.Baseline}";
        return $"status=missing|token={token}";
    }

    [CommandLineArgumentFunction("encounter_fixture_party_state", "coop.debug.siege")]
    public static string FixturePartyState(List<string> args)
    {
        if (args.Count != 2)
            return "Usage: coop.debug.siege.encounter_fixture_party_state <partyId> <settlementId>";

        if (!TryResolveServices(out var objectManager, out _, out var behaviorSnapshot, out _) ||
            !objectManager.TryGetObject<MobileParty>(args[0], out var party) ||
            !objectManager.TryGetObject<Settlement>(args[1], out var settlement) ||
            !behaviorSnapshot.TryCreate(party, out var behavior))
        {
            return "Unable to resolve the fixture party state.";
        }

        return $"partyState={FormatPartyState(party, behavior, settlement)}";
    }

    private static bool TryResolveServices(
        out IObjectManager objectManager,
        out IPlayerManager playerManager,
        out IMobilePartyBehaviorSnapshot behaviorSnapshot,
        out ISiegeEventInterface siegeEventInterface)
    {
        objectManager = null;
        playerManager = null;
        behaviorSnapshot = null;
        siegeEventInterface = null;
        return ContainerProvider.TryResolve(out objectManager) &&
            ContainerProvider.TryResolve(out playerManager) &&
            ContainerProvider.TryResolve(out behaviorSnapshot) &&
            ContainerProvider.TryResolve(out siegeEventInterface);
    }

    private static bool TryRestoreFixture(
        SiegeEncounterTransitionFixture fixture,
        IMobilePartyBehaviorSnapshot behaviorSnapshot,
        ISiegeEventInterface siegeEventInterface,
        out string error)
    {
        try
        {
            if (fixture.Party.BesiegerCamp != null ||
                fixture.Settlement.SiegeEvent?.BesiegerCamp?.LeaderParty == fixture.Party)
            {
                siegeEventInterface.BreakSiege(fixture.Party);
            }

            if (fixture.Party.BesiegerCamp != null)
                throw new InvalidOperationException($"{fixture.Party.StringId} is still attached to a siege camp.");

            if (fixture.Party.CurrentSettlement != null)
                LeaveSettlementAction.ApplyForParty(fixture.Party);

            if (fixture.Party.MapEvent != null)
                throw new InvalidOperationException($"{fixture.Party.StringId} is still attached to a map event.");

            fixture.Party.Position = fixture.Behavior.PartyPosition;
            fixture.Party.IsCurrentlyAtSea = fixture.Behavior.IsCurrentlyAtSea;
            if (!behaviorSnapshot.TryApply(fixture.Party, fixture.Behavior, out _))
                throw new InvalidOperationException($"Unable to restore movement state for {fixture.Party.StringId}.");

            fixture.Party.SetDisorganized(fixture.OriginalIsDisorganized);
            CharacterRelationManager.SetHeroRelation(
                fixture.OwnerLeader,
                fixture.PartyLeader,
                fixture.OriginalRelation);
            if (!behaviorSnapshot.TryCreate(fixture.Party, out var restoredBehavior) ||
                !BehaviorMatches(fixture.Behavior, restoredBehavior) ||
                fixture.Party.IsDisorganized != fixture.OriginalIsDisorganized ||
                CharacterRelationManager.GetHeroRelation(
                    fixture.OwnerLeader,
                    fixture.PartyLeader) != fixture.OriginalRelation)
            {
                throw new InvalidOperationException($"Restoration verification failed for {fixture.Party.StringId}.");
            }

            MessageBroker.Instance.Publish(
                typeof(SiegeEncounterTransitionFixtureCommands),
                new PartyBehaviorChangeAttempted(
                    fixture.Party,
                    forcePosition: true,
                    isCurrentlyAtSea: fixture.Behavior.IsCurrentlyAtSea,
                    resetMovementToHold: false));

            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    internal static bool BehaviorMatches(
        PartyBehaviorUpdateData expected,
        PartyBehaviorUpdateData actual) =>
        actual.MobilePartyId == expected.MobilePartyId &&
        actual.NewAiBehavior == expected.NewAiBehavior &&
        actual.InteractablePointId == expected.InteractablePointId &&
        actual.IsInteractableAnchor == expected.IsInteractableAnchor &&
        actual.BestTargetPoint.Equals(expected.BestTargetPoint) &&
        actual.PartyPosition.Equals(expected.PartyPosition) &&
        actual.DefaultBehavior == expected.DefaultBehavior &&
        actual.TargetPosition.Equals(expected.TargetPosition) &&
        actual.DesiredAiNavigationType == expected.DesiredAiNavigationType &&
        actual.TargetPartyId == expected.TargetPartyId &&
        actual.TargetSettlementId == expected.TargetSettlementId &&
        actual.MoveTargetPoint.Equals(expected.MoveTargetPoint) &&
        actual.IsTargetingPort == expected.IsTargetingPort &&
        actual.PartyMoveMode == expected.PartyMoveMode &&
        actual.MoveTargetPartyId == expected.MoveTargetPartyId &&
        actual.IsCurrentlyAtSea == expected.IsCurrentlyAtSea;

    private static SiegeEncounterTransitionFixture CreateFixture(
        string token,
        MobileParty party,
        PartyBehaviorUpdateData behavior,
        Settlement settlement)
    {
        return new SiegeEncounterTransitionFixture(
            token,
            party,
            behavior,
            settlement,
            settlement.OwnerClan.Leader,
            party.LeaderHero,
            CharacterRelationManager.GetHeroRelation(
                settlement.OwnerClan.Leader,
                party.LeaderHero));
    }

    private static bool TryGetToken(string value, out string token, out string error)
    {
        token = value;
        error = null;
        if (string.IsNullOrWhiteSpace(value) || !Guid.TryParseExact(value, "N", out _))
        {
            error = "Fixture token must be a caller-generated 32-character GUID in N format.";
            return false;
        }
        return true;
    }

    private static bool TryGetExistingFixture(string token, out string result)
    {
        if (activeFixture?.Token == token)
        {
            result = $"status=active|token={activeFixture.Token}|party={activeFixture.Party.StringId}|settlement={activeFixture.Settlement.StringId}";
            return true;
        }
        if (activeFixture != null)
        {
            result = $"Siege encounter fixture {activeFixture.Token} is already active.";
            return true;
        }
        if (completedRestores.ContainsKey(token))
        {
            result = $"Fixture token {token} already has a completed restore receipt.";
            return true;
        }
        result = null;
        return false;
    }

    private static string FormatPartyState(
        SiegeEncounterTransitionFixture fixture,
        PartyBehaviorUpdateData behavior) =>
        FormatPartyState(
            fixture.Party,
            behavior,
            fixture.Settlement,
            behavior.IsCurrentlyAtSea,
            fixture.OriginalIsDisorganized,
            fixture.OriginalRelation,
            "<null>",
            "<null>",
            "<null>",
            "<null>",
            "<null>");

    private static string FormatPartyState(
        MobileParty party,
        PartyBehaviorUpdateData behavior,
        Settlement settlement)
    {
        int relation = CharacterRelationManager.GetHeroRelation(settlement.OwnerClan.Leader, party.LeaderHero);
        return FormatPartyState(
            party,
            behavior,
            settlement,
            party.IsCurrentlyAtSea,
            party.IsDisorganized,
            relation,
            party.MapEvent == null ? "<null>" : "PRESENT",
            party.CurrentSettlement?.StringId ?? "<null>",
            party.BesiegerCamp == null ? "<null>" : "PRESENT",
            party.Army == null ? "<null>" : "PRESENT",
            ModInformation.IsClient && party == MobileParty.MainParty && PlayerEncounter.Current != null ? "PRESENT" : "<null>");
    }

    private static string FormatPartyState(
        MobileParty party,
        PartyBehaviorUpdateData behavior,
        Settlement settlement,
        bool isCurrentlyAtSea,
        bool isDisorganized,
        int relation,
        string mapEvent,
        string currentSettlement,
        string besiegerCamp,
        string army,
        string playerEncounter)
    {
        return $"mobilePartyId={behavior.MobilePartyId};newAiBehavior={behavior.NewAiBehavior};" +
            $"interactablePointId={behavior.InteractablePointId ?? "<null>"};" +
            $"isInteractableAnchor={behavior.IsInteractableAnchor};bestTargetPoint={behavior.BestTargetPoint};" +
            $"partyPosition={behavior.PartyPosition};defaultBehavior={behavior.DefaultBehavior};" +
            $"targetPosition={behavior.TargetPosition};desiredAiNavigationType={behavior.DesiredAiNavigationType};" +
            $"targetPartyId={behavior.TargetPartyId ?? "<null>"};targetSettlementId={behavior.TargetSettlementId ?? "<null>"};" +
            $"moveTargetPoint={behavior.MoveTargetPoint};isTargetingPort={behavior.IsTargetingPort};" +
            $"partyMoveMode={behavior.PartyMoveMode};moveTargetPartyId={behavior.MoveTargetPartyId ?? "<null>"};" +
            $"isCurrentlyAtSea={isCurrentlyAtSea};isDisorganized={isDisorganized};relation={relation};" +
            $"mapEvent={mapEvent};currentSettlement={currentSettlement};besiegerCamp={besiegerCamp};" +
            $"army={army};playerEncounter={playerEncounter}";
    }

    private static bool IsOwnedFortification(Settlement settlement) =>
        settlement?.IsFortification == true &&
        settlement.MapFaction != null &&
        settlement.OwnerClan?.Leader != null;

    private static bool IsClean(MobileParty party) =>
        party?.Ai != null &&
        party.IsActive &&
        !party.IsDisorganized &&
        party.CurrentSettlement == null &&
        party.MapEvent == null &&
        party.BesiegerCamp == null &&
        party.Army == null;

    private sealed class SiegeEncounterTransitionFixture
    {
        public string Token { get; }
        public MobileParty Party { get; }
        public PartyBehaviorUpdateData Behavior { get; }
        public Settlement Settlement { get; }
        public Hero OwnerLeader { get; }
        public Hero PartyLeader { get; }
        public int OriginalRelation { get; }
        public bool OriginalIsDisorganized { get; }

        public SiegeEncounterTransitionFixture(
            string token,
            MobileParty party,
            PartyBehaviorUpdateData behavior,
            Settlement settlement,
            Hero ownerLeader,
            Hero partyLeader,
            int originalRelation)
        {
            Token = token;
            Party = party;
            Behavior = behavior;
            Settlement = settlement;
            OwnerLeader = ownerLeader;
            PartyLeader = partyLeader;
            OriginalRelation = originalRelation;
            OriginalIsDisorganized = party.IsDisorganized;
        }
    }

    private sealed class FixtureRestoreReceipt
    {
        public string Token { get; }
        public string PartyId { get; }
        public string SettlementId { get; }
        public string Baseline { get; }

        public FixtureRestoreReceipt(SiegeEncounterTransitionFixture fixture)
        {
            Token = fixture.Token;
            PartyId = fixture.Party.StringId;
            SettlementId = fixture.Settlement.StringId;
            Baseline = FormatPartyState(fixture, fixture.Behavior);
        }

        public override string ToString() =>
            $"status=restored|restored=True|receipt=completed|token={Token}|party={PartyId}|settlement={SettlementId}";
    }
}
