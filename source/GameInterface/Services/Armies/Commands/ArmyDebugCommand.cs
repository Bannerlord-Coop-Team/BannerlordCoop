using Common;
using Common.Extensions;
using Common.Messaging;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.CampaignSystem.Army;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Armies.Commands;

/// <summary>
/// Commands for <see cref="Army"/>
/// </summary>
public class ArmyDebugCommand
{
#if DEBUG
    private static ArmyOverlayFixture armyOverlayFixture;
#endif

    // coop.debug.army.list
    /// <summary>
    /// Lists all the current Army
    /// </summary>
    [CommandLineArgumentFunction("list", "coop.debug.army")]
    public static string ListArmy(List<string> args)
    {
        StringBuilder stringBuilder = new StringBuilder();



        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to resolve {nameof(ArmyRegistry)}";
        }

        foreach (var army in Kingdom.All.SelectMany(kingdom => kingdom.Armies))
        {
            if (!objectManager.TryGetId(army, out var armyId))
            {
                stringBuilder.AppendLine($"Unable to get id for Army Name: '{army.Name}'");
                continue;
            }

            stringBuilder.AppendLine($"Name: '{army.Name}'");
            stringBuilder.AppendLine($"StringId: '{armyId}'");
        }

        return stringBuilder.ToString();
    }

    // coop.debug.army.create empire town_EN2 lord_1_1 Raider
    // coop.debug.army.mobile_party_add Army_Created_1 lord_1_3_party_1
    // coop.debug.army.destroy Army_Created_1 NotEnoughParty
    // coop.debug.army.mobile_party_remove Army_Created_1 lord_1_3_party_1
    /// <summary>
    /// Creates a new army on the server and clients
    /// </summary>
    [CommandLineArgumentFunction("create", "coop.debug.army")]
    public static string CreateArmy(List<string> args)
    {
        var sb = new StringBuilder();
        if (ModInformation.IsClient)
        {
            return "Command is only available to run on the server";
        }

        if (args.Count != 4)
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.AppendLine("Usage: coop.debug.kingdom.create <kingdomId> <targetSettlmentId> <heroLeaderId> <armyType>");
            stringBuilder.Append(GetArmyTypesUsage(stringBuilder));

            return stringBuilder.ToString();
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return "Unable to get ObjectManager";
        }

        var kingdomId = args[0];
        if (objectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom) == false)
        {
            return $"Unable to get Kingdom with {kingdomId}";
        }

        var targetSettlmentId = args[1];
        if (objectManager.TryGetObject<Settlement>(targetSettlmentId, out var targetSettlment) == false)
        {
            return $"Unable to get Settlement with {targetSettlmentId}";
        }

        var heroLeaderId = args[2];
        if (objectManager.TryGetObject<Hero>(heroLeaderId, out var armyLeader) == false)
        {
            return $"Unable to get Hero with {heroLeaderId}";
        }

        var armyTypeInt = args[3];
        if (Enum.TryParse(armyTypeInt, true, out ArmyTypes armyType) == false)
        {
            return $"Unable to cast {armyTypeInt} to {nameof(ArmyTypes)}\n" +
                GetArmyTypesUsage();
        }

        kingdom.CreateArmy(armyLeader, targetSettlment, armyType);
        var army = armyLeader.PartyBelongedTo?.Army;
        sb.AppendLine($"Created army {army.Name.ToString()}");
        return sb.ToString();
    }

    private static string GetArmyTypesUsage(StringBuilder stringBuilder = null)
    {
        stringBuilder = stringBuilder ?? new StringBuilder();

        stringBuilder.Append($"\tArmy.ArmyTypes = [");

        foreach (var armyTypeEnum in Enum.GetNames(typeof(ArmyTypes)).Zip(Enum.GetValues(typeof(ArmyTypes)).Cast<int>()))
        {
            stringBuilder.AppendLine($"\t\t{armyTypeEnum.Item1} = {armyTypeEnum.Item2}");
        }

        stringBuilder.Append("\t]");

        return stringBuilder.ToString();
    }

    // coop.debug.army.destroy Army_Created_1 NotEnoughParty
    /// <summary>
    /// Deletes an army on the server and clients
    /// </summary>
    [CommandLineArgumentFunction("destroy", "coop.debug.army")]
    public static string DestroyArmy(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Command is only available to run on the server";
        }

        if (args.Count != 2)
        {
            return "Usage: coop.debug.kingdom.destroy <armyId> <disbandArmyReason>";
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get {nameof(IObjectManager)}";
        }

        var armyId = args[0];
        if (objectManager.TryGetObject<Army>(armyId, out var army) == false)
        {
            return $"Unable to get {nameof(Army)} with {armyId}";
        }

        var disbandArmyReason = args[1];
        if (Enum.TryParse(disbandArmyReason, true, out ArmyDispersionReason reason) == false)
        {
            return $"Unable to cast {disbandArmyReason} to {nameof(ArmyDispersionReason)}\n" +
                GetArmyDispersionReasonUsage();
        }
        var armyName = army.Name.ToString();
        DisbandArmyAction.ApplyInternal(army, reason);

        return $"Destroyed army {armyName} with id {armyId}";
    }

    private static string GetArmyDispersionReasonUsage(StringBuilder stringBuilder = null)
    {
        stringBuilder = stringBuilder ?? new StringBuilder();

        stringBuilder.Append($"\t{nameof(ArmyDispersionReason)} = [");

        foreach (var armyTypeEnum in Enum.GetNames(typeof(ArmyDispersionReason)).Zip(Enum.GetValues(typeof(ArmyDispersionReason)).Cast<int>()))
        {
            stringBuilder.AppendLine($"\t\t{armyTypeEnum.Item1} = {armyTypeEnum.Item2}");
        }

        stringBuilder.Append("\t]");

        return stringBuilder.ToString();
    }

    // coop.debug.army.mobile_party_list Army_Created_1
    /// <summary>
    /// Lists all the current Mobile Parties for an Army
    /// </summary>
    /// 
    [CommandLineArgumentFunction("mobile_party_list", "coop.debug.army")]
    public static string GetMobilePartyList(List<string> args)
    {

        var stringBuilder = new StringBuilder();


        if (args.Count != 1)
        {

            return "Usage: coop.debug.army.mobile_party_list <ArmyId>";
        }

        string armyId = args[0];


        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get {nameof(IObjectManager)}";
        }

        if (objectManager.TryGetObject<Army>(armyId, out var army) == false)
        {
            return $"Unable to get {nameof(Army)} with {armyId}";
        }

        foreach (var mobileParty in army.Parties)
        {
            stringBuilder.AppendLine($"Name: {mobileParty.Name}\nStringId: {mobileParty.StringId}");
        }

        return stringBuilder.ToString();
    }

    // coop.debug.army.mobile_party_add Army_Created_1 lord_1_34_party_1
    /// <summary>
    /// Add a Mobile Party to an Army
    /// </summary>
    /// 
    [CommandLineArgumentFunction("mobile_party_add", "coop.debug.army")]
    public static string AddMobileParty(List<string> args)
    {

        var stringBuilder = new StringBuilder();


        if (args.Count != 2)
        {

            return "Usage: coop.debug.army.mobile_party_add <ArmyId> <MobilePartyId>";
        }

        string armyId = args[0];
        string mobilePartyId = args[1];


        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get {nameof(IObjectManager)}";
        }

        if (objectManager.TryGetObject(mobilePartyId, out MobileParty mobileParty) == false)
        {
            return $"Unable to get {nameof(MobileParty)} with {mobilePartyId}";
        }


        if (objectManager.TryGetObject<Army>(armyId, out var army) == false)
        {
            return $"Unable to get {nameof(Army)} with {armyId}";
        }

        mobileParty.Army = army;

        stringBuilder.AppendLine($"Added {mobileParty.Name} to {armyId}");

        return stringBuilder.ToString();
    }

    // coop.debug.army.mobile_party_remove Army_Created_1 lord_1_3_party_1
    /// <summary>
    /// Add a Mobile Party to an Army
    /// </summary>
    /// 
    [CommandLineArgumentFunction("mobile_party_remove", "coop.debug.army")]
    public static string RemoveMobileParty(List<string> args)
    {

        var stringBuilder = new StringBuilder();


        if (args.Count != 2)
        {

            return "Usage: coop.debug.army.mobile_party_remove <ArmyId> <MobilePartyId>";
        }

        string armyId = args[0];
        string mobilePartyId = args[1];


        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get {nameof(IObjectManager)}";
        }

        if (objectManager.TryGetObject(mobilePartyId, out MobileParty mobileParty) == false)
        {
            return $"Unable to get {nameof(MobileParty)} with {mobilePartyId}";
        }


        if (objectManager.TryGetObject<Army>(armyId, out var army) == false)
        {
            return $"Unable to get {nameof(Army)} with {armyId}";
        }

        mobileParty.Army = null;

        stringBuilder.AppendLine($"Removed {mobileParty.Name} from {armyId}");

        return stringBuilder.ToString();
    }
    // coop.debug.army.info Army_Created_1 
    /// <summary>
    /// Info about army
    /// </summary>
    /// 
    [CommandLineArgumentFunction("info", "coop.debug.army")]
    public static string Info(List<string> args)
    {
        var sb = new StringBuilder();
        if (args.Count != 1)
        {

            return "Usage: coop.debug.army.info <ArmyId>";
        }
        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get {nameof(IObjectManager)}";
        }
        if (objectManager.TryGetObject<Army>(args[0], out var army) == false)
        {
            return $"Unable to get {nameof(Army)} with {args[0]}";
        }
        sb.AppendLine($"AttachedParties count: {army?.LeaderParty.AttachedParties?.Count}");
        sb.AppendLine($"{army._parties.Count}");
        sb.AppendLine($"LeaderHero: {army?.LeaderParty?.LeaderHero?.Name}");
        sb.AppendLine($"Army.name {army.Name}");
        sb.AppendLine($"Armyowner {army.ArmyOwner.Name}");
        sb.AppendLine($"leaderparty owner {army?.LeaderParty.Owner.Name}");
        sb.AppendLine($"armycohesion: {army?.Cohesion}");
        return sb.ToString();
    }

#if DEBUG
    [CommandLineArgumentFunction("overlay_fixture_capture", "coop.debug.army")]
    public static string CaptureOverlayFixture(List<string> args)
    {
        const string usage = "Usage: coop.debug.army.overlay_fixture_capture <controllerId> <kingdomId> <settlementId> <leaderId> <proposerClanId>";
        if (!ModInformation.IsServer) return "Command can only be run on the server.";
        if (args.Count != 5) return usage;
        if (armyOverlayFixture != null) return "An army overlay fixture lifecycle is already active.";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !ContainerProvider.TryResolve<IKingdomMembershipState>(out var kingdomMembershipState) ||
            !ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
            return "Unable to resolve army overlay fixture services.";
        if (!playerManager.TryGetPlayer(args[0], out var player))
            return $"Player '{args[0]}' was not found.";
        if (!objectManager.TryGetObject(player.ClanId, out Clan playerClan))
            return $"Player clan '{player.ClanId}' was not found.";
        if (!objectManager.TryGetObject(args[1], out Kingdom kingdom))
            return $"Kingdom '{args[1]}' was not found.";
        if (!objectManager.TryGetObject(args[2], out Settlement settlement))
            return $"Settlement '{args[2]}' was not found.";
        if (!objectManager.TryGetObject(args[3], out Hero leader))
            return $"Leader hero '{args[3]}' was not found.";
        if (!objectManager.TryGetObject(args[4], out Clan proposerClan))
            return $"Proposer clan '{args[4]}' was not found.";
        if (leader.PartyBelongedTo == null || leader.PartyBelongedTo.Army != null)
            return $"Leader '{args[3]}' must have an active party outside an army.";
        if (!behaviorSnapshot.TryCreate(leader.PartyBelongedTo, out var leaderBehavior))
            return $"Unable to capture the movement state for leader '{args[3]}'.";
        if (leader.Clan?.Kingdom != kingdom)
            return $"Leader '{args[3]}' does not belong to kingdom '{args[1]}'.";
        if (proposerClan.Kingdom != kingdom)
            return $"Proposer clan '{args[4]}' does not belong to kingdom '{args[1]}'.";
        if (kingdom._unresolvedDecisions.OfType<SettlementClaimantPreliminaryDecision>()
            .Any(decision => decision.Settlement == settlement))
            return $"Kingdom '{args[1]}' already has a claimant decision for '{args[2]}'.";

        string previousKingdomId = null;
        if (playerClan.Kingdom != null && !objectManager.TryGetIdWithLogging(playerClan.Kingdom, out previousKingdomId))
            return "Unable to capture the player's current kingdom id.";

        var snapshot = new ArmyOverlaySnapshot
        {
            FixtureId = "army-overlay-join",
            ControllerId = args[0],
            KingdomId = args[1],
            SettlementId = args[2],
            LeaderId = args[3],
            ProposerClanId = args[4],
            PlayerClanId = player.ClanId,
            PreviousKingdomId = previousKingdomId
        };
        armyOverlayFixture = new ArmyOverlayFixture(
            snapshot,
            kingdom,
            settlement,
            leader,
            proposerClan,
            playerClan,
            kingdomMembershipState,
            behaviorSnapshot,
            leaderBehavior);
        return JsonResult(snapshot);
    }

    [CommandLineArgumentFunction("overlay_fixture_stage", "coop.debug.army")]
    public static string StageOverlayFixture(List<string> args)
    {
        const string usage = "Usage: coop.debug.army.overlay_fixture_stage <capturedJson>";
        if (!ModInformation.IsServer) return "Command can only be run on the server.";
        if (args.Count != 1) return usage;
        if (!TryMatchOverlaySnapshot(args[0], out var fixture, out var error)) return error;
        if (fixture.Staged) return "The army overlay fixture mutation has already run.";
        if (!ContainerProvider.TryResolve<IKingdomDecisionVoteManager>(out var decisionVoteManager))
            return "Unable to resolve the kingdom decision vote manager.";

        try
        {
            fixture.KingdomMembershipState.MoveClanToKingdom(
                fixture.PlayerClan.Kingdom,
                fixture.Kingdom,
                fixture.PlayerClan,
                publishCollectionChanges: true);

            fixture.Kingdom.CreateArmy(fixture.Leader, fixture.Settlement, ArmyTypes.Defender);
            fixture.Army = fixture.Leader.PartyBelongedTo?.Army;
            if (fixture.Army == null)
                throw new InvalidOperationException("The fixture army was not created.");

            fixture.Army.LeaderParty.SetMoveGoToSettlement(
                fixture.Settlement,
                fixture.Army.LeaderParty.NavigationCapability,
                isTargetingThePort: false);
            fixture.Army._aiBehaviorObject = null;
            fixture.Decision = new SettlementClaimantPreliminaryDecision(fixture.ProposerClan, fixture.Settlement);
            fixture.Kingdom.AddDecision(fixture.Decision, true);
            if (!decisionVoteManager.TryExtendDecisionDeadlineForDebug(fixture.Decision, TimeSpan.FromHours(1)))
                throw new InvalidOperationException("The fixture decision deadline could not be extended.");
            fixture.Staged = true;

            return JsonResult(new
            {
                fixtureId = fixture.Snapshot.FixtureId,
                fixtureArmyCreated = true,
                leaderId = fixture.Snapshot.LeaderId,
                defaultBehavior = fixture.Army.LeaderParty.DefaultBehavior.ToString(),
                aiBehaviorObjectMissing = fixture.Army.AiBehaviorObject == null,
                decisionStaged = fixture.Kingdom._unresolvedDecisions.Contains(fixture.Decision),
                playerKingdomId = fixture.Kingdom.StringId
            });
        }
        catch (Exception exception)
        {
            RestoreOverlayFixtureState(fixture);
            return "Failed to stage army overlay fixture: " + exception.Message;
        }
    }

    [CommandLineArgumentFunction("overlay_fixture_state", "coop.debug.army")]
    public static string OverlayFixtureState(List<string> args)
    {
        const string usage = "Usage: coop.debug.army.overlay_fixture_state <leaderId>";
        if (args.Count != 1) return usage;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObject(args[0], out Hero leader))
            return $"Leader hero '{args[0]}' was not found.";

        Army army = leader.PartyBelongedTo?.Army;
        string behaviorText = ModInformation.IsClient
            ? army?.GetLongTermBehaviorText()?.ToString()
            : string.Empty;
        return JsonResult(new
        {
            leaderId = args[0],
            armyPresent = army != null,
            defaultBehavior = army?.LeaderParty.DefaultBehavior.ToString(),
            aiBehaviorObjectMissing = army != null && army.AiBehaviorObject == null,
            behaviorText = behaviorText ?? string.Empty
        });
    }

    [CommandLineArgumentFunction("overlay_fixture_restore", "coop.debug.army")]
    public static string RestoreOverlayFixture(List<string> args)
    {
        const string usage = "Usage: coop.debug.army.overlay_fixture_restore <capturedJson>";
        if (!ModInformation.IsServer) return "Command can only be run on the server.";
        if (args.Count != 1) return usage;
        if (!TryMatchOverlaySnapshot(args[0], out var fixture, out var error)) return error;
        if (fixture.RestoreCompleted) return "The army overlay fixture restoration has already run.";

        RestoreOverlayFixtureState(fixture);
        bool restored = IsOverlayFixtureRestored(fixture);
        fixture.RestoreCompleted = restored;
        return JsonResult(new
        {
            fixtureId = fixture.Snapshot.FixtureId,
            restored,
            decisionRemoved = fixture.Decision == null || !fixture.Kingdom._unresolvedDecisions.Contains(fixture.Decision),
            armyRemoved = fixture.Leader.PartyBelongedTo?.Army != fixture.Army,
            playerKingdomId = fixture.PlayerClan.Kingdom?.StringId
        });
    }

    [CommandLineArgumentFunction("overlay_fixture_verify", "coop.debug.army")]
    public static string VerifyOverlayFixture(List<string> args)
    {
        const string usage = "Usage: coop.debug.army.overlay_fixture_verify <capturedJson>";
        if (!ModInformation.IsServer) return "Command can only be run on the server.";
        if (args.Count != 1) return usage;
        if (!TryMatchOverlaySnapshot(args[0], out var fixture, out var error)) return error;

        TryGetFixtureKingdomId(fixture.PlayerClan.Kingdom, out string currentKingdomId);
        bool restored = fixture.RestoreCompleted && IsOverlayFixtureRestored(fixture);
        var result = JsonResult(new
        {
            fixtureId = fixture.Snapshot.FixtureId,
            restored,
            decisionRemoved = fixture.Decision == null || !fixture.Kingdom._unresolvedDecisions.Contains(fixture.Decision),
            armyRemoved = fixture.Leader.PartyBelongedTo?.Army != fixture.Army,
            playerKingdomId = currentKingdomId,
            expectedKingdomId = fixture.Snapshot.PreviousKingdomId
        });
        if (restored) armyOverlayFixture = null;
        return result;
    }

    private static void RestoreOverlayFixtureState(ArmyOverlayFixture fixture)
    {
        if (fixture.Decision != null && fixture.Kingdom._unresolvedDecisions.Contains(fixture.Decision))
            fixture.Kingdom.RemoveDecision(fixture.Decision);
        if (fixture.Army != null && fixture.Leader.PartyBelongedTo?.Army == fixture.Army)
            DisbandArmyAction.ApplyInternal(fixture.Army, ArmyDispersionReason.ObjectiveFinished);
        if (!fixture.BehaviorSnapshot.TryApply(
            fixture.Leader.PartyBelongedTo,
            fixture.LeaderBehavior,
            out _))
            throw new InvalidOperationException("Unable to restore the fixture leader's movement state.");
        fixture.Leader.PartyBelongedTo.Position = fixture.LeaderBehavior.PartyPosition;
        MessageBroker.Instance.Publish(
            typeof(ArmyDebugCommand),
            new PartyBehaviorChangeAttempted(
                fixture.Leader.PartyBelongedTo,
                forcePosition: true,
                isCurrentlyAtSea: fixture.Leader.PartyBelongedTo.IsCurrentlyAtSea));

        Kingdom previousKingdom = null;
        if (!string.IsNullOrEmpty(fixture.Snapshot.PreviousKingdomId))
        {
            if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
                !objectManager.TryGetObject(fixture.Snapshot.PreviousKingdomId, out previousKingdom))
                throw new InvalidOperationException("Unable to restore the player's previous kingdom.");
        }
        fixture.KingdomMembershipState.MoveClanToKingdom(
            fixture.PlayerClan.Kingdom,
            previousKingdom,
            fixture.PlayerClan,
            publishCollectionChanges: true);
    }

    private static bool IsOverlayFixtureRestored(ArmyOverlayFixture fixture)
    {
        return TryGetFixtureKingdomId(fixture.PlayerClan.Kingdom, out string currentKingdomId) &&
            (fixture.Decision == null || !fixture.Kingdom._unresolvedDecisions.Contains(fixture.Decision)) &&
            fixture.Leader.PartyBelongedTo?.Army != fixture.Army &&
            IsLeaderBehaviorRestored(fixture) &&
            currentKingdomId == fixture.Snapshot.PreviousKingdomId;
    }

    private static bool TryGetFixtureKingdomId(Kingdom kingdom, out string kingdomId)
    {
        kingdomId = null;
        if (kingdom == null) return true;

        return ContainerProvider.TryResolve<IObjectManager>(out var objectManager) &&
            objectManager.TryGetId(kingdom, out kingdomId);
    }

    private static bool IsLeaderBehaviorRestored(ArmyOverlayFixture fixture)
    {
        if (!fixture.BehaviorSnapshot.TryCreate(
            fixture.Leader.PartyBelongedTo,
            out var actual))
            return false;

        var expected = fixture.LeaderBehavior;
        return actual.MobilePartyId == expected.MobilePartyId &&
            actual.NewAiBehavior == expected.NewAiBehavior &&
            actual.InteractablePointId == expected.InteractablePointId &&
            actual.BestTargetPoint == expected.BestTargetPoint &&
            actual.PartyPosition == expected.PartyPosition &&
            actual.DefaultBehavior == expected.DefaultBehavior &&
            actual.TargetPosition == expected.TargetPosition &&
            actual.DesiredAiNavigationType == expected.DesiredAiNavigationType &&
            actual.TargetPartyId == expected.TargetPartyId &&
            actual.TargetSettlementId == expected.TargetSettlementId &&
            actual.MoveTargetPoint == expected.MoveTargetPoint &&
            actual.IsTargetingPort == expected.IsTargetingPort &&
            actual.PartyMoveMode == expected.PartyMoveMode &&
            actual.MoveTargetPartyId == expected.MoveTargetPartyId &&
            actual.IsInteractableAnchor == expected.IsInteractableAnchor &&
            actual.IsCurrentlyAtSea == expected.IsCurrentlyAtSea;
    }

    private static bool TryMatchOverlaySnapshot(
        string json,
        out ArmyOverlayFixture fixture,
        out string error)
    {
        fixture = armyOverlayFixture;
        error = null;
        if (fixture == null)
        {
            error = "No army overlay fixture lifecycle is active.";
            return false;
        }

        ArmyOverlaySnapshot snapshot;
        try
        {
            snapshot = JsonConvert.DeserializeObject<ArmyOverlaySnapshot>(json);
        }
        catch (JsonException)
        {
            error = "The captured army overlay fixture JSON is invalid.";
            return false;
        }

        if (snapshot == null || snapshot.FixtureId != fixture.Snapshot.FixtureId ||
            snapshot.ControllerId != fixture.Snapshot.ControllerId ||
            snapshot.KingdomId != fixture.Snapshot.KingdomId ||
            snapshot.SettlementId != fixture.Snapshot.SettlementId ||
            snapshot.LeaderId != fixture.Snapshot.LeaderId ||
            snapshot.ProposerClanId != fixture.Snapshot.ProposerClanId ||
            snapshot.PlayerClanId != fixture.Snapshot.PlayerClanId ||
            snapshot.PreviousKingdomId != fixture.Snapshot.PreviousKingdomId)
        {
            error = "The captured army overlay fixture state does not match the active lifecycle.";
            return false;
        }
        return true;
    }

    private static string JsonResult(object value) =>
        "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(value);

    private sealed class ArmyOverlaySnapshot
    {
        public string FixtureId { get; set; }
        public string ControllerId { get; set; }
        public string KingdomId { get; set; }
        public string SettlementId { get; set; }
        public string LeaderId { get; set; }
        public string ProposerClanId { get; set; }
        public string PlayerClanId { get; set; }
        public string PreviousKingdomId { get; set; }
    }

    private sealed class ArmyOverlayFixture
    {
        public ArmyOverlaySnapshot Snapshot { get; }
        public Kingdom Kingdom { get; }
        public Settlement Settlement { get; }
        public Hero Leader { get; }
        public Clan ProposerClan { get; }
        public Clan PlayerClan { get; }
        public IKingdomMembershipState KingdomMembershipState { get; }
        public IMobilePartyBehaviorSnapshot BehaviorSnapshot { get; }
        public PartyBehaviorUpdateData LeaderBehavior { get; }
        public Army Army { get; set; }
        public SettlementClaimantPreliminaryDecision Decision { get; set; }
        public bool Staged { get; set; }
        public bool RestoreCompleted { get; set; }

        public ArmyOverlayFixture(
            ArmyOverlaySnapshot snapshot,
            Kingdom kingdom,
            Settlement settlement,
            Hero leader,
            Clan proposerClan,
            Clan playerClan,
            IKingdomMembershipState kingdomMembershipState,
            IMobilePartyBehaviorSnapshot behaviorSnapshot,
            PartyBehaviorUpdateData leaderBehavior)
        {
            Snapshot = snapshot;
            Kingdom = kingdom;
            Settlement = settlement;
            Leader = leader;
            ProposerClan = proposerClan;
            PlayerClan = playerClan;
            KingdomMembershipState = kingdomMembershipState;
            BehaviorSnapshot = behaviorSnapshot;
            LeaderBehavior = leaderBehavior;
        }
    }
#endif
}
