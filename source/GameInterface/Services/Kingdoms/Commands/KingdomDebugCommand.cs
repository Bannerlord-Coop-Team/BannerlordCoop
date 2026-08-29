using Autofac;
using Common;
using Common.Extensions;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.Kingdoms.Extentions;
using GameInterface.Services.Kingdoms.Handlers;
using GameInterface.Services.Kingdoms.Data;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Newtonsoft.Json;
using SandBox.GauntletUI;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions.ItemTypes;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using TaleWorlds.ScreenSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Kingdoms.Commands;

/// <summary>
/// Commands for <see cref="Kingdom"/>
/// </summary>
public class KingdomDebugCommand
{
    private static readonly ILogger Logger = LogManager.GetLogger<KingdomDebugCommand>();
    private static PolicyTimeoutFixture pendingPolicyTimeoutFixture;
    private static AllianceTimeoutFixture pendingAllianceTimeoutFixture;
    private enum CollectionTarget
    {
        Armies,
        Clans,
        FiefsCache,
        HeroesCache,
        AliveLordsCache,
        DeadLordsCache,
        SettlementsCache,
        TownsCache,
        VillagesCache,
        WarPartyComponentsCache,
    }

    private enum CollectionOperation
    {
        Add,
        Remove,
    }

    private static readonly string CreateUsage = "Usage: coop.debug.kingdom.create <leaderHeroName> <kingdomName> (run on the server; use '_' for spaces in the hero name)";
    private static readonly string CollectionAddUsage = "Usage: coop.debug.kingdom.collection_add <collection> <kingdomId> <valueId> | unresolvedDecisions <kingdomId> <proposerClanId> <ignoreInfluenceCost> <decisionType> <decisionTypeArgs>";
    private static readonly string CollectionRemoveUsage = "Usage: coop.debug.kingdom.collection_remove <collection> <kingdomId> <valueId> | unresolvedDecisions <kingdomId> <index>";
    private static readonly string RemoveUsage = "Usage: coop.debug.kingdom.remove_decision <kingdomId> <Index>";
    private static readonly string AddBasicUsage = "Usage: coop.debug.kingdom.add_decision <kingdomId> <proposerClanId> <ignoreInfluenceCost> <decisionType> <decisionTypeArgs>";
    private static readonly string AddDeclareWarDecisionUsage = "Usage: coop.debug.kingdom.add_decision <kingdomId> <proposerClanId> <ignoreInfluenceCost> DeclareWarDecision <factionId>";
    private static readonly string AddExpelClanFromKingdomDecisionUsage = "Usage: coop.debug.kingdom.add_decision <kingdomId> <proposerClanId> <ignoreInfluenceCost> ExpelClanFromKingdomDecision <clanToExpelId>";
    private static readonly string AddKingSelectionKingdomDecisionUsage = "Usage: coop.debug.kingdom.add_decision <kingdomId> <proposerClanId> <ignoreInfluenceCost> KingSelectionKingdomDecision <clanToExcludeId>";
    private static readonly string AddKingdomPolicyDecisionUsage = "Usage: coop.debug.kingdom.add_decision <kingdomId> <proposerClanId> <ignoreInfluenceCost> KingdomPolicyDecision <policyId> <isInvertedDecision>";
    private static readonly string AddSettlementClaimantDecisionUsage = "Usage: coop.debug.kingdom.add_decision <kingdomId> <proposerClanId> <ignoreInfluenceCost> SettlementClaimantDecision <settlementId> <capturerHeroId> <clanToExcludeId>";
    private static readonly string AddSettlementClaimantPreliminaryDecisionUsage = "Usage: coop.debug.kingdom.add_decision <kingdomId> <proposerClanId> <ignoreInfluenceCost> SettlementClaimantPreliminaryDecision <SettlementId>";
    private static readonly string AddMakePeaceKingdomDecisionUsage = "Usage: coop.debug.kingdom.add_decision <kingdomId> <proposerClanId> <ignoreInfluenceCost> MakePeaceKingdomDecision <factionId> <dailyTribute> <applyResults>";
    private static readonly string AddAcceptCallToWarAgreementDecisionUsage = "Usage: coop.debug.kingdom.add_decision <kingdomId> <proposerClanId> <ignoreInfluenceCost> AcceptCallToWarAgreementDecision <callingKingdomId> <kingdomToCallToWarAgainstId>";
    private static readonly string AddProposeCallToWarAgreementDecisionUsage = "Usage: coop.debug.kingdom.add_decision <kingdomId> <proposerClanId> <ignoreInfluenceCost> ProposeCallToWarAgreementDecision <calledKingdomId> <kingdomToCallToWarAgainstId>";
    private static readonly string AddStartAllianceDecisionUsage = "Usage: coop.debug.kingdom.add_decision <kingdomId> <proposerClanId> <ignoreInfluenceCost> StartAllianceDecision <kingdomToStartAllianceWithId>";
    private static readonly string AddTradeAgreementDecisionUsage = "Usage: coop.debug.kingdom.add_decision <kingdomId> <proposerClanId> <ignoreInfluenceCost> TradeAgreementDecision <targetKingdomId>";
    private delegate bool KingdomDecisionDelegate(IObjectManager objectManager, List<string> args, Clan proposerClan, out KingdomDecision kingdomDecision, out string message);
    private static readonly Dictionary<string, KingdomDecisionDelegate> TryGetKingdomDecisionFunc = new Dictionary<string, KingdomDecisionDelegate>()
        {
            { nameof(DeclareWarDecision), TryGetDeclareWarDecision },
            { nameof(ExpelClanFromKingdomDecision), TryGetExpelClanFromKingdomDecision },
            { nameof(KingSelectionKingdomDecision), TryGetKingSelectionKingdomDecision },
            { nameof(KingdomPolicyDecision), TryGetKingdomPolicyDecision },
            { nameof(SettlementClaimantDecision), TryGetSettlementClaimantDecision },
            { nameof(SettlementClaimantPreliminaryDecision), TryGetSettlementClaimantPreliminaryDecision },
            { nameof(AcceptCallToWarAgreementDecision), TryGetAcceptCallToWarAgreementDecision },
            { nameof(ProposeCallToWarAgreementDecision), TryGetProposeCallToWarAgreementDecision },
            { nameof(StartAllianceDecision), TryGetStartAllianceDecision },
            { nameof(TradeAgreementDecision), TryGetTradeAgreementDecision },
            //{ nameof(MakePeaceKingdomDecision), TryGetMakePeaceKingdomDecision },
        };


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

    private static bool TryGetPlayerManager(out IPlayerManager playerManager)
    {
        playerManager = null;
        if (ContainerProvider.TryGetContainer(out var container) == false) return false;

        return container.TryResolve(out playerManager);
    }

    private static bool TryGetKingdomMembershipState(out IKingdomMembershipState kingdomMembershipState)
    {
        kingdomMembershipState = null;
        if (ContainerProvider.TryGetContainer(out var container) == false) return false;

        return container.TryResolve(out kingdomMembershipState);
    }

    private static bool TryGetKingdomDecisionVoteManager(out IKingdomDecisionVoteManager voteManager)
    {
        voteManager = null;
        if (ContainerProvider.TryGetContainer(out var container) == false) return false;

        return container.TryResolve(out voteManager);
    }

    [CommandLineArgumentFunction("open", "coop.debug.kingdom")]
    public static string OpenKingdomScreen(List<string> args)
    {
        if (!ModInformation.IsClient) return "Command can only be run on a client.";
        if (args.Count != 0) return "Usage: coop.debug.kingdom.open";
        if (Clan.PlayerClan?.Kingdom == null) return "The player clan is not in a kingdom.";
        if (Game.Current?.GameStateManager == null) return "The game-state manager is unavailable.";
        if (Game.Current.GameStateManager.ActiveState is KingdomState) return "KINGDOM_SCREEN_ALREADY_OPEN";

        KingdomState kingdomState = Game.Current.GameStateManager.CreateState<KingdomState>(
            (IFaction)Clan.PlayerClan);
        Game.Current.GameStateManager.PushState(kingdomState, 0);
        return "KINGDOM_SCREEN_OPENED";
    }

    [CommandLineArgumentFunction("open_decision", "coop.debug.kingdom")]
    public static string OpenKingdomDecisionScreen(List<string> args)
    {
        if (!ModInformation.IsClient) return "Command can only be run on a client.";
        if (args.Count < 2) return "Usage: <kingdomId> <decisionIndex>";

        KingdomDecision decision;
        Kingdom playerKingdom = Clan.PlayerClan?.Kingdom;
        bool isPlayerKingdom = playerKingdom != null &&
            (string.Equals(playerKingdom.StringId, args[0], StringComparison.Ordinal) ||
             string.Equals($"{nameof(Kingdom)}_{playerKingdom.StringId}", args[0], StringComparison.Ordinal));
        if (isPlayerKingdom)
        {
            if (!int.TryParse(args[1], out int index)) return $"Decision index is not a number: {args[1]}";

            int zeroBasedIndex = index - 1;
            if (zeroBasedIndex < 0 || zeroBasedIndex >= playerKingdom._unresolvedDecisions.Count)
                return "Decision index is out of bounds.";

            decision = playerKingdom._unresolvedDecisions[zeroBasedIndex];
        }
        else if (!TryGetKingdomDecisionByIndex(args, out Kingdom _, out decision, out int _, out string message))
        {
            return message;
        }
        if (Game.Current?.GameStateManager == null) return "The game-state manager is unavailable.";
        if (Game.Current.GameStateManager.ActiveState is KingdomState) return "KINGDOM_SCREEN_ALREADY_OPEN";

        KingdomState kingdomState = Game.Current.GameStateManager.CreateState<KingdomState>(decision);
        InquiryData inquiry = null;
        Action<InquiryData, bool, bool> captureInquiry = (data, _, _) => inquiry = data;
        InformationManager.OnShowInquiry += captureInquiry;
        try
        {
            Game.Current.GameStateManager.PushState(kingdomState, 0);
        }
        finally
        {
            InformationManager.OnShowInquiry -= captureInquiry;
        }

        if (inquiry == null || inquiry.AffirmativeAction == null ||
            !string.Equals(inquiry.TitleText, GameTexts.FindText("str_decision").ToString(), StringComparison.Ordinal))
        {
            return "The decision confirmation did not open.";
        }

        InformationManager.HideInquiry();
        inquiry.AffirmativeAction();
        return "KINGDOM_DECISION_SCREEN_OPENED";
    }

    [CommandLineArgumentFunction("close", "coop.debug.kingdom")]
    public static string CloseKingdomScreen(List<string> args)
    {
        if (!ModInformation.IsClient) return "Command can only be run on a client.";
        if (args.Count != 0) return "Usage: coop.debug.kingdom.close";
        if (!(Game.Current?.GameStateManager?.ActiveState is KingdomState))
            return "No active Kingdom screen.";

        Game.Current.GameStateManager.PopState(0);
        return "KINGDOM_SCREEN_CLOSED";
    }

    [CommandLineArgumentFunction("screen_state", "coop.debug.kingdom")]
    public static string KingdomScreenState(List<string> args)
    {
        if (!ModInformation.IsClient) return "Command can only be run on a client.";
        if (args.Count != 0) return "Usage: coop.debug.kingdom.screen_state";

        var kingdomScreen = ScreenManager.TopScreen as GauntletKingdomScreen;
        return $"KINGDOM_SCREEN_STATE active={Game.Current?.GameStateManager?.ActiveState is KingdomState} " +
            $"topScreen={kingdomScreen != null} dataSource={kingdomScreen?.DataSource != null} " +
            $"decisionActive={kingdomScreen?.DataSource?.Decision?.IsActive ?? false} " +
            $"clanShown={kingdomScreen?.DataSource?.Clan?.Show ?? false} " +
            $"kingdom={kingdomScreen?.DataSource?.Kingdom?.Name} " +
            $"clans={kingdomScreen?.DataSource?.Clan?.Clans?.Count ?? -1}";
    }

    [CommandLineArgumentFunction("policy_timeout_capture", "coop.debug.kingdom")]
    public static string CapturePolicyTimeoutFixture(List<string> args)
    {
        if (ModInformation.IsClient) return "Command can only be run on the server.";
        if (args.Count != 0) return "Usage: coop.debug.kingdom.policy_timeout_capture";
        if (pendingPolicyTimeoutFixture != null) return "A policy-timeout fixture lifecycle is already active.";
        if (!TryGetObjectManager(out var objectManager) || !TryGetPlayerManager(out var playerManager))
            return "Unable to resolve policy-timeout fixture services.";
        if (!playerManager.TryGetPlayer("testclient", out var player))
            return "No registered player has controller id 'testclient'.";
        if (!objectManager.TryGetObject(player.ClanId, out Clan proposerClan) || proposerClan.Kingdom == null)
            return "The testclient clan is not in a kingdom.";

        Kingdom kingdom = proposerClan.Kingdom;
        if (kingdom.UnresolvedDecisions.Count > 0)
            return $"Kingdom {kingdom.StringId} already has an unresolved decision.";

        PolicyObject policy = PolicyObject.All
            .Where(candidate => !kingdom.ActivePolicies.Contains(candidate))
            .OrderBy(candidate => candidate.StringId)
            .FirstOrDefault();
        if (policy == null) return $"Kingdom {kingdom.StringId} has every policy active.";
        if (!objectManager.TryGetIdWithLogging(kingdom, out string kingdomId) ||
            !objectManager.TryGetIdWithLogging(proposerClan, out string proposerClanId) ||
            !objectManager.TryGetIdWithLogging(policy, out string policyId))
            return "Unable to resolve policy-timeout fixture ids.";

        return PolicyTimeoutJsonResult(new
        {
            success = true,
            controllerId = player.ControllerId,
            kingdomId,
            kingdomName = kingdom.Name.ToString(),
            proposerClanId,
            policyId,
            policyName = policy.Name.ToString(),
            policyWasActive = false,
            unresolvedDecisionCount = kingdom.UnresolvedDecisions.Count
        });
    }

    [CommandLineArgumentFunction("policy_timeout_stage", "coop.debug.kingdom")]
    public static string StagePolicyTimeoutFixture(List<string> args)
    {
        const string usage = "Usage: coop.debug.kingdom.policy_timeout_stage <kingdomId> <proposerClanId> <policyId> <policyWasActive>";
        if (ModInformation.IsClient) return "Command can only be run on the server.";
        if (args.Count != 4 || !bool.TryParse(args[3], out bool policyWasActive)) return usage;
        if (pendingPolicyTimeoutFixture != null) return "A policy-timeout fixture lifecycle is already active.";
        if (!TryGetObjectManager(out var objectManager)) return "Unable to resolve ObjectManager";
        if (!objectManager.TryGetObject(args[0], out Kingdom kingdom)) return $"Kingdom with ID: '{args[0]}' not found";
        if (!objectManager.TryGetObject(args[1], out Clan proposerClan)) return $"Clan with ID: '{args[1]}' not found";
        if (!objectManager.TryGetObject(args[2], out PolicyObject policy)) return $"PolicyObject with ID: '{args[2]}' not found";
        if (proposerClan.Kingdom != kingdom) return $"Clan {args[1]} is not in kingdom {args[0]}.";
        if (kingdom.UnresolvedDecisions.Count > 0)
            return $"Kingdom {args[0]} no longer has a clean decision fixture.";
        if (kingdom.ActivePolicies.Contains(policy) != policyWasActive)
            return $"Policy {args[2]} changed after fixture capture.";

        var fixture = new PolicyTimeoutFixture(
            kingdom,
            proposerClan,
            policy,
            args[0],
            args[1],
            args[2],
            policyWasActive);
        pendingPolicyTimeoutFixture = fixture;

        var decision = new KingdomPolicyDecision(fixture.ProposerClan, fixture.Policy, fixture.PolicyWasActive);
        fixture.Kingdom.AddDecision(decision, true);
        fixture.DecisionStaged = true;
        int decisionIndex = fixture.Kingdom._unresolvedDecisions.IndexOf(decision) + 1;
        if (decisionIndex <= 0) return "The policy-timeout decision was not added to the kingdom.";

        return PolicyTimeoutJsonResult(new
        {
            success = true,
            fixture.KingdomId,
            fixture.PolicyId,
            decisionIndex,
            votingDurationSeconds = (int)KingdomDecisionVoteManager.VotingRoundDuration.TotalSeconds
        });
    }

    [CommandLineArgumentFunction("policy_timeout_state", "coop.debug.kingdom")]
    public static string GetPolicyTimeoutState(List<string> args)
    {
        if (ModInformation.IsServer) return "Command can only be run on a client.";
        if (args.Count != 0) return "Usage: coop.debug.kingdom.policy_timeout_state";

        var kingdomScreen = ScreenManager.TopScreen as GauntletKingdomScreen;
        DecisionItemBaseVM decision = kingdomScreen?.DataSource?.Decision?.CurrentDecision;
        string decisionTitle = decision?.TitleText ?? string.Empty;
        return PolicyTimeoutJsonResult(new
        {
            success = true,
            kingdomScreenActive = Game.Current?.GameStateManager?.ActiveState is KingdomState,
            topScreenIsKingdom = kingdomScreen != null,
            decisionPresent = decision != null,
            decisionActive = decision?.IsActive ?? false,
            decisionTitle,
            hasVotingCountdown = decisionTitle.IndexOf("Voting ends in", StringComparison.OrdinalIgnoreCase) >= 0,
            inquiryActive = InformationManager.IsAnyInquiryActive()
        });
    }

    [CommandLineArgumentFunction("policy_timeout_restore", "coop.debug.kingdom")]
    public static string RestorePolicyTimeoutFixture(List<string> args)
    {
        const string usage = "Usage: coop.debug.kingdom.policy_timeout_restore <kingdomId> <proposerClanId> <policyId> <policyWasActive>";
        if (ModInformation.IsClient) return "Command can only be run on the server.";
        if (args.Count != 4 || !bool.TryParse(args[3], out bool policyWasActive)) return usage;
        if (!TryMatchPendingPolicyTimeoutFixture(args[0], args[1], args[2], policyWasActive, out var fixture, out string error))
            return error;

        KingdomPolicyDecision stagedDecision = fixture.Kingdom.UnresolvedDecisions
            .OfType<KingdomPolicyDecision>()
            .FirstOrDefault(decision => decision.Policy == fixture.Policy);
        if (stagedDecision != null) fixture.Kingdom.RemoveDecision(stagedDecision);

        bool policyIsActive = fixture.Kingdom.ActivePolicies.Contains(fixture.Policy);
        if (fixture.PolicyWasActive && !policyIsActive)
        {
            fixture.Kingdom.AddPolicy(fixture.Policy);
        }
        else if (!fixture.PolicyWasActive && policyIsActive)
        {
            fixture.Kingdom.RemovePolicy(fixture.Policy);
        }

        pendingPolicyTimeoutFixture = null;
        return PolicyTimeoutJsonResult(new
        {
            success = true,
            fixture.KingdomId,
            fixture.PolicyId,
            restoredPolicyActive = fixture.Kingdom.ActivePolicies.Contains(fixture.Policy),
            unresolvedDecisionCount = fixture.Kingdom.UnresolvedDecisions.Count
        });
    }

    [CommandLineArgumentFunction("policy_timeout_verify", "coop.debug.kingdom")]
    public static string VerifyPolicyTimeoutFixture(List<string> args)
    {
        const string usage = "Usage: coop.debug.kingdom.policy_timeout_verify <kingdomId> <policyId> <policyWasActive>";
        if (ModInformation.IsClient) return "Command can only be run on the server.";
        if (args.Count != 3 || !bool.TryParse(args[2], out bool policyWasActive)) return usage;
        if (pendingPolicyTimeoutFixture != null) return "The policy-timeout fixture lifecycle is still active.";
        if (!TryGetObjectManager(out var objectManager)) return "Unable to resolve ObjectManager";
        if (!objectManager.TryGetObject(args[0], out Kingdom kingdom)) return $"Kingdom with ID: '{args[0]}' not found";
        if (!objectManager.TryGetObject(args[1], out PolicyObject policy)) return $"PolicyObject with ID: '{args[1]}' not found";

        bool policyIsActive = kingdom.ActivePolicies.Contains(policy);
        bool decisionPresent = kingdom.UnresolvedDecisions
            .OfType<KingdomPolicyDecision>()
            .Any(decision => decision.Policy == policy);
        bool success = policyIsActive == policyWasActive && !decisionPresent && kingdom.UnresolvedDecisions.Count == 0;
        return PolicyTimeoutJsonResult(new
        {
            success,
            kingdomId = args[0],
            policyId = args[1],
            expectedPolicyActive = policyWasActive,
            policyIsActive,
            decisionPresent,
            unresolvedDecisionCount = kingdom.UnresolvedDecisions.Count
        });
    }

    [CommandLineArgumentFunction("alliance_timeout_target", "coop.debug.kingdom")]
    public static string GetAllianceTimeoutTarget(List<string> args)
    {
        if (ModInformation.IsClient) return "Command can only be run on the server.";
        if (args.Count != 0) return "Usage: coop.debug.kingdom.alliance_timeout_target";
        if (pendingAllianceTimeoutFixture != null) return "An alliance-timeout fixture lifecycle is already active.";
        if (!TryGetObjectManager(out var objectManager) || !TryGetPlayerManager(out var playerManager))
            return "Unable to resolve alliance-timeout fixture services.";
        if (!playerManager.TryGetPlayer("testclient", out var player))
            return "No registered player has controller id 'testclient'.";
        if (!objectManager.TryGetObject(player.ClanId, out Clan playerClan))
            return "Unable to resolve the testclient clan.";
        if (playerClan.Kingdom != null)
            return "The testclient clan must be kingdomless before selecting an alliance-timeout target.";

        Kingdom sturgia = Kingdom.All.SingleOrDefault(candidate =>
            string.Equals(candidate.StringId, "sturgia", StringComparison.Ordinal));
        if (sturgia == null) return "The Sturgia kingdom is unavailable.";
        if (sturgia.Leader == null) return "Sturgia has no ruling leader.";

        AllianceCampaignBehavior allianceBehavior = Campaign.Current?.GetCampaignBehavior<AllianceCampaignBehavior>();
        if (allianceBehavior == null) return "The alliance campaign behavior is unavailable.";

        Kingdom kingdom = null;
        foreach (Kingdom candidate in Kingdom.All
            .Where(candidate =>
                IsEligibleAllianceTimeoutCandidate(candidate, sturgia, allianceBehavior))
            .OrderBy(candidate => candidate.StringId, StringComparer.Ordinal))
        {
            if (!TryValidateAllianceTimeoutTarget(candidate, sturgia, out string error))
            {
                if (!string.IsNullOrEmpty(error)) return error;
                continue;
            }

            kingdom = candidate;
            break;
        }
        if (kingdom == null)
            return "No NPC-ruled kingdom can receive a valid alliance offer from Sturgia after reversible fixture normalization.";
        if (!objectManager.TryGetIdWithLogging(kingdom, out string kingdomId) ||
            !objectManager.TryGetIdWithLogging(sturgia, out string sturgiaId))
            return "Unable to resolve alliance-timeout target ids.";

        return LiveTestJsonResult(new
        {
            success = true,
            kingdomId,
            kingdomStringId = kingdom.StringId,
            kingdomName = kingdom.Name.ToString(),
            targetKingdomId = sturgiaId,
            targetKingdomName = sturgia.Name.ToString()
        });
    }

    [CommandLineArgumentFunction("alliance_timeout_capture", "coop.debug.kingdom")]
    public static string CaptureAllianceTimeoutFixture(List<string> args)
    {
        if (ModInformation.IsClient) return "Command can only be run on the server.";
        if (args.Count != 0) return "Usage: coop.debug.kingdom.alliance_timeout_capture";
        if (pendingAllianceTimeoutFixture != null) return "An alliance-timeout fixture lifecycle is already active.";
        if (!TryGetObjectManager(out var objectManager) || !TryGetPlayerManager(out var playerManager))
            return "Unable to resolve alliance-timeout fixture services.";
        if (!playerManager.TryGetPlayer("testclient", out var player))
            return "No registered player has controller id 'testclient'.";
        if (!objectManager.TryGetObject(player.ClanId, out Clan proposerClan) || proposerClan.Kingdom == null)
            return "The testclient clan is not in a kingdom.";

        Kingdom kingdom = proposerClan.Kingdom;
        if (kingdom.RulingClan == null) return $"Kingdom {kingdom.StringId} has no ruling clan.";
        if (kingdom.Leader == null) return $"Kingdom {kingdom.StringId} has no ruling leader.";
        if (kingdom.RulingClan == proposerClan)
            return "The testclient clan must be a voting supporter under an NPC ruler.";
        Kingdom targetKingdom = Kingdom.All.SingleOrDefault(candidate =>
            string.Equals(candidate.StringId, "sturgia", StringComparison.Ordinal));
        if (targetKingdom == null) return "The Sturgia kingdom is unavailable.";
        if (targetKingdom.Leader == null) return "Sturgia has no ruling leader.";
        if (targetKingdom == kingdom) return "The testclient clan is already in Sturgia.";
        if (kingdom.UnresolvedDecisions.Count > 0)
            return $"Kingdom {kingdom.StringId} already has an unresolved decision.";
        if (FactionManager.IsAtWarAgainstFaction(kingdom, targetKingdom))
            return $"Kingdom {kingdom.StringId} is at war with Sturgia.";

        AllianceCampaignBehavior allianceBehavior = Campaign.Current?.GetCampaignBehavior<AllianceCampaignBehavior>();
        if (allianceBehavior == null) return "The alliance campaign behavior is unavailable.";
        if (allianceBehavior.IsAllyWithKingdom(kingdom, targetKingdom))
            return $"Kingdom {kingdom.StringId} is already allied with Sturgia.";
        if (!objectManager.TryGetIdWithLogging(kingdom, out string kingdomId) ||
            !objectManager.TryGetIdWithLogging(proposerClan, out string proposerClanId) ||
            !objectManager.TryGetIdWithLogging(targetKingdom, out string targetKingdomId))
            return "Unable to resolve alliance-timeout fixture ids.";

        if (!TryCreateAllianceTimeoutFixture(
                kingdom,
                proposerClan,
                targetKingdom,
                kingdomId,
                targetKingdomId,
                out var fixture,
                out string fixtureError))
        {
            return fixtureError;
        }
        pendingAllianceTimeoutFixture = fixture;
        if (!TryPrepareAllianceTimeoutFixture(fixture, out _, out string error))
        {
            if (fixture.EligibilityRestored) pendingAllianceTimeoutFixture = null;
            return error;
        }

        return LiveTestJsonResult(new
        {
            success = true,
            controllerId = player.ControllerId,
            kingdomId,
            kingdomName = kingdom.Name.ToString(),
            proposerClanId,
            targetKingdomId,
            targetKingdomName = targetKingdom.Name.ToString(),
            allianceWasActive = false,
            leaderRelationBefore = fixture.LeaderRelationBefore,
            leaderRelationAfter = fixture.CurrentLeaderRelation,
            leaderRelationChanged = fixture.LeaderRelationChanged,
            eligibilityInputsChanged = fixture.EligibilityInputsChanged,
            normalizedThreatClanCount = fixture.NormalizedThreatClanCount,
            normalizedSupporterRelationCount = fixture.NormalizedSupporterRelationCount,
            unresolvedDecisionCount = kingdom.UnresolvedDecisions.Count
        });
    }

    [CommandLineArgumentFunction("alliance_timeout_stage", "coop.debug.kingdom")]
    public static string StageAllianceTimeoutFixture(List<string> args)
    {
        const string usage = "Usage: coop.debug.kingdom.alliance_timeout_stage <kingdomId> <proposerClanId> <targetKingdomId>";
        if (ModInformation.IsClient) return "Command can only be run on the server.";
        if (args.Count != 3) return usage;
        if (!TryMatchPendingAllianceTimeoutFixture(args[0], args[2], false, out var fixture, out string error))
            return error;
        if (!TryGetObjectManager(out var objectManager))
            return FailAllianceTimeoutFixture(fixture, "Unable to resolve ObjectManager");
        if (!objectManager.TryGetObject(args[0], out Kingdom kingdom))
            return FailAllianceTimeoutFixture(fixture, $"Kingdom with ID: '{args[0]}' not found");
        if (!objectManager.TryGetObject(args[1], out Clan proposerClan))
            return FailAllianceTimeoutFixture(fixture, $"Clan with ID: '{args[1]}' not found");
        if (!objectManager.TryGetObject(args[2], out Kingdom targetKingdom))
            return FailAllianceTimeoutFixture(fixture, $"Kingdom with ID: '{args[2]}' not found");
        if (proposerClan.Kingdom != kingdom)
            return FailAllianceTimeoutFixture(fixture, $"Clan {args[1]} is not in kingdom {args[0]}.");
        if (kingdom.RulingClan == null || kingdom.RulingClan == proposerClan)
            return FailAllianceTimeoutFixture(fixture, "The testclient clan is no longer a voting supporter under an NPC ruler.");
        if (kingdom.UnresolvedDecisions.Count > 0)
            return FailAllianceTimeoutFixture(fixture, $"Kingdom {args[0]} no longer has a clean decision fixture.");

        if (!TryVerifyUnchangedAllianceState(fixture, out string allianceStateError))
            return FailAllianceTimeoutFixture(fixture, allianceStateError);
        if (FactionManager.IsAtWarAgainstFaction(kingdom, targetKingdom))
        {
            return FailAllianceTimeoutFixture(fixture, "The alliance-timeout fixture relationship changed after capture.");
        }
        if (!TryCanMakeAlliance(
                kingdom,
                targetKingdom,
                kingdom.RulingClan,
                includeReason: true,
                out bool canMakeAlliance,
                out TextObject reason,
                out string gateError))
        {
            return FailAllianceTimeoutFixture(fixture, gateError);
        }
        if (!canMakeAlliance)
        {
            return FailAllianceTimeoutFixture(
                fixture,
                $"Kingdom {kingdom.StringId} cannot currently receive a valid alliance offer from Sturgia: {reason}");
        }

        if (!fixture.TryNormalizeNpcElectionSupporters(out string electionSupporterError))
            return FailAllianceTimeoutFixture(fixture, electionSupporterError);

        string stageFailure = string.Empty;
        int decisionIndex = 0;
        StartAllianceDecision decision = null;
        try
        {
            if (!TryCanMakeAlliance(
                    kingdom,
                    targetKingdom,
                    kingdom.RulingClan,
                    includeReason: true,
                    out canMakeAlliance,
                    out reason,
                    out gateError))
            {
                stageFailure = gateError;
            }
            else if (!canMakeAlliance)
            {
                stageFailure = $"Kingdom {kingdom.StringId} cannot currently receive a valid alliance offer from Sturgia: {reason}";
            }
            else
            {
                // A redirected inbound offer is authored by the player kingdom's ruling clan.
                decision = new StartAllianceDecision(fixture.Kingdom.RulingClan, fixture.TargetKingdom);
                CoopKingdomElection._opponentProposedAllianceDecisions.Add(decision);
                fixture.Kingdom.AddDecision(decision, true);
                if (!fixture.TryRestoreNpcElectionSupporters(out string restoreError))
                {
                    stageFailure = $"The alliance-timeout election supporters could not be restored: {restoreError}";
                }
                else
                {
                    decision.TriggerTime = CampaignTime.Zero;
                    CoopKingdomDecisionProposalBehaviorPatch.HourlyTickPrefix();

                    if (!TryVerifyUnchangedAllianceState(fixture, out allianceStateError))
                    {
                        stageFailure = allianceStateError;
                    }
                    else
                    {
                        decisionIndex = fixture.Kingdom._unresolvedDecisions.IndexOf(decision) + 1;
                        if (decisionIndex <= 0)
                        {
                            stageFailure = "The alliance-timeout decision was not retained for co-op voting.";
                        }
                    }
                }
            }
        }
        catch (Exception exception)
        {
            stageFailure = $"The alliance-timeout decision could not be staged: {exception.Message}";
        }
        finally
        {
            if (!fixture.TryRestoreNpcElectionSupporters(out string restoreError))
            {
                stageFailure = string.IsNullOrEmpty(stageFailure)
                    ? $"The alliance-timeout election supporters could not be restored: {restoreError}"
                    : $"{stageFailure} Fixture election supporter restoration failed: {restoreError}";
            }
        }

        if (!string.IsNullOrEmpty(stageFailure)) return FailAllianceTimeoutFixture(fixture, stageFailure);

        return LiveTestJsonResult(new
        {
            success = true,
            fixture.KingdomId,
            fixture.TargetKingdomId,
            decisionIndex,
            normalizedNpcElectionSupporterCount = fixture.NormalizedNpcElectionSupporterCount,
            npcElectionSupportersRestored = fixture.NpcElectionSupportersRestored,
            triggerPast = decision.TriggerTime.IsPast,
            votingDurationSeconds = (int)KingdomDecisionVoteManager.VotingRoundDuration.TotalSeconds
        });
    }

    [CommandLineArgumentFunction("alliance_timeout_state", "coop.debug.kingdom")]
    public static string GetAllianceTimeoutState(List<string> args)
    {
        const string usage = "Usage: coop.debug.kingdom.alliance_timeout_state <expectedActive>";
        if (ModInformation.IsServer) return "Command can only be run on a client.";
        if (args.Count != 1 || !bool.TryParse(args[0], out bool expectedActive)) return usage;

        var kingdomScreen = ScreenManager.TopScreen as GauntletKingdomScreen;
        DecisionItemBaseVM decisionItem = kingdomScreen?.DataSource?.Decision?.CurrentDecision;
        var allianceItem = decisionItem as StartAllianceDecisionItemVM;
        bool decisionActive = allianceItem?.IsActive ?? false;
        var allianceDecision = allianceItem?.KingdomDecisionMaker?._decision as StartAllianceDecision;
        string targetKingdomName = allianceDecision?.KingdomToStartAllianceWith?.Name?.ToString() ?? string.Empty;
        string decisionTitle = allianceItem?.TitleText ?? string.Empty;
        bool namesSturgia = string.Equals(targetKingdomName, "Sturgia", StringComparison.OrdinalIgnoreCase) ||
            decisionTitle.IndexOf("Sturgia", StringComparison.OrdinalIgnoreCase) >= 0;
        bool hasVotingCountdown = decisionTitle.IndexOf("Voting ends in", StringComparison.OrdinalIgnoreCase) >= 0;
        bool observedExpectedState = expectedActive
            ? decisionActive && namesSturgia && hasVotingCountdown
            : decisionItem == null;
        if (!observedExpectedState) return LiveTestJsonResult(null);

        return LiveTestJsonResult(new
        {
            success = true,
            expectedActive,
            kingdomScreenActive = Game.Current?.GameStateManager?.ActiveState is KingdomState,
            topScreenIsKingdom = kingdomScreen != null,
            decisionPresent = decisionItem != null,
            allianceDecisionActive = decisionActive,
            decisionTitle,
            targetKingdomName,
            namesSturgia,
            hasVotingCountdown,
            inquiryActive = InformationManager.IsAnyInquiryActive()
        });
    }

    [CommandLineArgumentFunction("alliance_timeout_submit_no", "coop.debug.kingdom")]
    public static string SubmitAllianceTimeoutNoVote(List<string> args)
    {
        if (ModInformation.IsServer) return "Command can only be run on a client.";
        if (args.Count != 0) return "Usage: coop.debug.kingdom.alliance_timeout_submit_no";

        var kingdomScreen = ScreenManager.TopScreen as GauntletKingdomScreen;
        var decisionItem = kingdomScreen?.DataSource?.Decision?.CurrentDecision as StartAllianceDecisionItemVM;
        if (decisionItem == null || !decisionItem.IsActive) return "No active alliance decision is displayed.";

        DecisionOptionVM noOption = decisionItem.DecisionOptionsList.SingleOrDefault(candidate =>
            candidate.Option is StartAllianceDecision.StartAllianceDecisionOutcome outcome &&
            !outcome.ShouldAllianceBeStarted);
        if (noOption == null) return "The alliance decision has no No option.";

        noOption.CurrentSupportWeight = Supporter.SupportWeights.FullyPush;
        decisionItem._currentSelectedOption = noOption;
        decisionItem.ExecuteFinalSelection();

        return LiveTestJsonResult(new
        {
            success = true,
            selectedOutcome = "No",
            finalVoteSubmitted = true
        });
    }

    [CommandLineArgumentFunction("alliance_timeout_server_state", "coop.debug.kingdom")]
    public static string GetAllianceTimeoutServerState(List<string> args)
    {
        const string usage = "Usage: coop.debug.kingdom.alliance_timeout_server_state <kingdomId> <targetKingdomId>";
        if (ModInformation.IsClient) return "Command can only be run on the server.";
        if (args.Count != 2) return usage;
        if (!TryMatchPendingAllianceTimeoutFixture(args[0], args[1], false, out var fixture, out string error))
            return error;

        AllianceCampaignBehavior allianceBehavior = Campaign.Current?.GetCampaignBehavior<AllianceCampaignBehavior>();
        if (allianceBehavior == null) return "The alliance campaign behavior is unavailable.";
        bool decisionPresent = HasMatchingAllianceDecision(fixture);
        bool allianceIsActive = allianceBehavior.IsAllyWithKingdom(fixture.Kingdom, fixture.TargetKingdom);
        return LiveTestJsonResult(new
        {
            success = !decisionPresent && allianceIsActive == fixture.AllianceWasActive,
            fixture.KingdomId,
            fixture.TargetKingdomId,
            decisionPresent,
            expectedAllianceActive = fixture.AllianceWasActive,
            allianceIsActive,
            unresolvedDecisionCount = fixture.Kingdom.UnresolvedDecisions.Count
        });
    }

    [CommandLineArgumentFunction("alliance_timeout_restore", "coop.debug.kingdom")]
    public static string RestoreAllianceTimeoutFixture(List<string> args)
    {
        const string usage = "Usage: coop.debug.kingdom.alliance_timeout_restore <kingdomId> <targetKingdomId> <allianceWasActive>";
        if (ModInformation.IsClient) return "Command can only be run on the server.";
        if (args.Count != 3 || !bool.TryParse(args[2], out bool allianceWasActive)) return usage;
        if (!TryMatchPendingAllianceTimeoutFixture(args[0], args[1], allianceWasActive, out var fixture, out string error))
            return error;

        if (!TryRestoreAllianceTimeoutFixture(fixture, out string restoreError)) return restoreError;

        pendingAllianceTimeoutFixture = null;
        AllianceCampaignBehavior allianceBehavior = Campaign.Current.GetCampaignBehavior<AllianceCampaignBehavior>();
        return LiveTestJsonResult(new
        {
            success = true,
            fixture.KingdomId,
            fixture.TargetKingdomId,
            restoredAllianceActive = allianceBehavior.IsAllyWithKingdom(fixture.Kingdom, fixture.TargetKingdom),
            leaderRelationBefore = fixture.LeaderRelationBefore,
            leaderRelationRestored = fixture.LeaderRelationRestored,
            eligibilityInputsRestored = fixture.EligibilityRestored,
            unresolvedDecisionCount = fixture.Kingdom.UnresolvedDecisions.Count
        });
    }

    [CommandLineArgumentFunction("alliance_timeout_verify", "coop.debug.kingdom")]
    public static string VerifyAllianceTimeoutFixture(List<string> args)
    {
        const string usage = "Usage: coop.debug.kingdom.alliance_timeout_verify <kingdomId> <targetKingdomId> <allianceWasActive>";
        if (ModInformation.IsClient) return "Command can only be run on the server.";
        if (args.Count != 3 || !bool.TryParse(args[2], out bool allianceWasActive)) return usage;
        if (pendingAllianceTimeoutFixture != null) return "The alliance-timeout fixture lifecycle is still active.";
        if (!TryGetObjectManager(out var objectManager)) return "Unable to resolve ObjectManager";
        if (!objectManager.TryGetObject(args[0], out Kingdom kingdom)) return $"Kingdom with ID: '{args[0]}' not found";
        if (!objectManager.TryGetObject(args[1], out Kingdom targetKingdom)) return $"Kingdom with ID: '{args[1]}' not found";

        AllianceCampaignBehavior allianceBehavior = Campaign.Current?.GetCampaignBehavior<AllianceCampaignBehavior>();
        if (allianceBehavior == null) return "The alliance campaign behavior is unavailable.";
        bool allianceIsActive = allianceBehavior.IsAllyWithKingdom(kingdom, targetKingdom);
        bool decisionPresent = kingdom.UnresolvedDecisions
            .OfType<StartAllianceDecision>()
            .Any(decision => decision.KingdomToStartAllianceWith == targetKingdom);
        bool success = allianceIsActive == allianceWasActive && !decisionPresent &&
            kingdom.UnresolvedDecisions.Count == 0;
        return LiveTestJsonResult(new
        {
            success,
            kingdomId = args[0],
            targetKingdomId = args[1],
            expectedAllianceActive = allianceWasActive,
            allianceIsActive,
            decisionPresent,
            unresolvedDecisionCount = kingdom.UnresolvedDecisions.Count
        });
    }

    private static bool TryMatchPendingAllianceTimeoutFixture(
        string kingdomId,
        string targetKingdomId,
        bool allianceWasActive,
        out AllianceTimeoutFixture fixture,
        out string error)
    {
        fixture = pendingAllianceTimeoutFixture;
        error = string.Empty;
        if (fixture == null)
        {
            error = "No alliance-timeout fixture has been captured.";
            return false;
        }
        if (fixture.KingdomId != kingdomId || fixture.TargetKingdomId != targetKingdomId ||
            fixture.AllianceWasActive != allianceWasActive)
        {
            error = "The alliance-timeout fixture arguments do not match the captured state.";
            return false;
        }

        return true;
    }

    private static bool HasMatchingAllianceDecision(AllianceTimeoutFixture fixture) =>
        fixture.Kingdom.UnresolvedDecisions
            .OfType<StartAllianceDecision>()
            .Any(decision => decision.KingdomToStartAllianceWith == fixture.TargetKingdom);

    private static bool IsEligibleAllianceTimeoutCandidate(
        Kingdom kingdom,
        Kingdom targetKingdom,
        AllianceCampaignBehavior allianceBehavior)
    {
        if (kingdom == null || targetKingdom == null || kingdom == targetKingdom ||
            kingdom.IsEliminated || targetKingdom.IsEliminated ||
            kingdom.RulingClan == null || kingdom.Leader == null || targetKingdom.RulingClan == null ||
            targetKingdom.Leader == null || kingdom.UnresolvedDecisions.Count != 0 ||
            FactionManager.IsAtWarAgainstFaction(kingdom, targetKingdom) ||
            allianceBehavior.IsAllyWithKingdom(kingdom, targetKingdom)) return false;

        if (Campaign.Current?.Models?.AllianceModel == null) return false;

        int maximumAlliances = Campaign.Current.Models.AllianceModel.MaxNumberOfAlliances;
        if (kingdom.AlliedKingdoms.Count >= maximumAlliances ||
            targetKingdom.AlliedKingdoms.Count >= maximumAlliances) return false;

        // The vanilla gate owns the topology and directional score checks after preparation.
        return true;
    }

    private static List<AllianceTimeoutNeighbor> GetAllianceTimeoutNeighbors(Kingdom kingdom)
    {
        var seenFortifications = new HashSet<Settlement>();
        var neighborCounts = new Dictionary<Kingdom, float>();
        float totalNeighborCount = 0f;
        foreach (Town fief in kingdom.Fiefs)
        {
            foreach (Settlement fortification in fief.GetNeighborFortifications(MobileParty.NavigationType.All))
            {
                IFaction faction = fortification.MapFaction;
                if (faction == kingdom || !faction.IsKingdomFaction || !seenFortifications.Add(fortification)) continue;

                var neighboringKingdom = (Kingdom)faction;
                if (neighborCounts.TryGetValue(neighboringKingdom, out float count))
                {
                    neighborCounts[neighboringKingdom] = count + 1f;
                }
                else
                {
                    neighborCounts.Add(neighboringKingdom, 1f);
                }

                totalNeighborCount++;
            }
        }

        return neighborCounts
            .Select(pair => new AllianceTimeoutNeighbor(pair.Key, pair.Value, totalNeighborCount))
            .ToList();
    }

    private static bool TryCreateAllianceTimeoutFixture(
        Kingdom kingdom,
        Clan proposerClan,
        Kingdom targetKingdom,
        string kingdomId,
        string targetKingdomId,
        out AllianceTimeoutFixture fixture,
        out string error)
    {
        fixture = null;
        error = string.Empty;
        if (kingdom?.Leader == null || targetKingdom?.Leader == null)
        {
            error = "The alliance-timeout fixture requires both kingdom leaders.";
            return false;
        }

        var campaign = Campaign.Current;
        if (campaign?.Models?.DiplomacyModel == null)
        {
            error = "The campaign diplomacy model is unavailable.";
            return false;
        }

        try
        {
            campaign.Models.DiplomacyModel.GetHeroesForEffectiveRelation(
                kingdom.Leader,
                targetKingdom.Leader,
                out Hero effectiveLeader,
                out Hero effectiveTargetLeader);
            if (effectiveLeader == null || effectiveTargetLeader == null || effectiveLeader == effectiveTargetLeader)
            {
                error = "The alliance-timeout fixture could not resolve distinct effective relation heroes.";
                return false;
            }

            fixture = new AllianceTimeoutFixture(
                kingdom,
                proposerClan,
                targetKingdom,
                kingdomId,
                targetKingdomId,
                false,
                effectiveLeader,
                effectiveTargetLeader,
                CharacterRelationManager.GetHeroRelation(effectiveLeader, effectiveTargetLeader));
            return true;
        }
        catch (Exception exception)
        {
            error = $"The alliance-timeout fixture could not resolve the effective relation: {exception.Message}";
            return false;
        }
    }

    private static bool TryValidateAllianceTimeoutTarget(Kingdom kingdom, Kingdom targetKingdom, out string error)
    {
        if (!TryCreateAllianceTimeoutFixture(
                kingdom,
                kingdom.RulingClan,
                targetKingdom,
                kingdom.StringId,
                targetKingdom.StringId,
                out var fixture,
                out error))
        {
            return false;
        }

        bool targetIsEligible;
        bool gateRejected;
        bool eligibilityRestored = false;
        try
        {
            targetIsEligible = TryPrepareAllianceTimeoutFixture(fixture, out gateRejected, out error);
        }
        finally
        {
            eligibilityRestored = fixture.TryRestoreEligibility(out string restoreError);
            if (!eligibilityRestored)
            {
                error = $"Unable to restore alliance-timeout target normalization: {restoreError}";
            }
        }

        if (!targetIsEligible && gateRejected && eligibilityRestored)
        {
            error = string.Empty;
            return false;
        }
        return string.IsNullOrEmpty(error) && targetIsEligible;
    }

    private static bool TryPrepareAllianceTimeoutFixture(
        AllianceTimeoutFixture fixture,
        out bool gateRejected,
        out string error)
    {
        gateRejected = false;
        bool requiresPlayerSupportNormalization = fixture.RequiresPlayerSupportNormalization;
        if (!TryCanMakeAlliance(
                fixture.Kingdom,
                fixture.TargetKingdom,
                fixture.Kingdom.RulingClan,
                includeReason: false,
                out bool canMakeAlliance,
                out _,
                out error)) return false;
        if (canMakeAlliance && !requiresPlayerSupportNormalization) return true;

        if (!fixture.TryNormalizeLeaderRelation(out string leaderRelationError))
        {
            return TryFailAllianceTimeoutFixturePreparation(fixture, leaderRelationError, out error);
        }
        if (TryCanMakeAlliance(
                fixture.Kingdom,
                fixture.TargetKingdom,
                fixture.Kingdom.RulingClan,
                includeReason: false,
                out canMakeAlliance,
                out _,
                out error) && canMakeAlliance && !requiresPlayerSupportNormalization) return true;
        if (!string.IsNullOrEmpty(error))
        {
            string gateFailure = error;
            return TryFailAllianceTimeoutFixturePreparation(fixture, gateFailure, out error);
        }

        if (!fixture.TryNormalizeThreatStrengths(out string threatStrengthError))
        {
            return TryFailAllianceTimeoutFixturePreparation(fixture, threatStrengthError, out error);
        }
        if (TryCanMakeAlliance(
                fixture.Kingdom,
                fixture.TargetKingdom,
                fixture.Kingdom.RulingClan,
                includeReason: false,
                out canMakeAlliance,
                out _,
                out error) && canMakeAlliance && !requiresPlayerSupportNormalization) return true;
        if (!string.IsNullOrEmpty(error))
        {
            string gateFailure = error;
            return TryFailAllianceTimeoutFixturePreparation(fixture, gateFailure, out error);
        }

        if (!fixture.TryNormalizeKingdomCultures(out string cultureError))
        {
            return TryFailAllianceTimeoutFixturePreparation(fixture, cultureError, out error);
        }
        if (TryCanMakeAlliance(
                fixture.Kingdom,
                fixture.TargetKingdom,
                fixture.Kingdom.RulingClan,
                includeReason: false,
                out canMakeAlliance,
                out _,
                out error) && canMakeAlliance && !requiresPlayerSupportNormalization) return true;
        if (!string.IsNullOrEmpty(error))
        {
            string gateFailure = error;
            return TryFailAllianceTimeoutFixturePreparation(fixture, gateFailure, out error);
        }

        if (!fixture.TryNormalizeLeaderHonor(out string leaderHonorError))
        {
            return TryFailAllianceTimeoutFixturePreparation(fixture, leaderHonorError, out error);
        }
        if (TryCanMakeAlliance(
                fixture.Kingdom,
                fixture.TargetKingdom,
                fixture.Kingdom.RulingClan,
                includeReason: false,
                out canMakeAlliance,
                out _,
                out error) && canMakeAlliance && !requiresPlayerSupportNormalization) return true;
        if (!string.IsNullOrEmpty(error))
        {
            string gateFailure = error;
            return TryFailAllianceTimeoutFixturePreparation(fixture, gateFailure, out error);
        }

        if (!fixture.TryNormalizePlayerSupport(out string playerSupportError))
        {
            return TryFailAllianceTimeoutFixturePreparation(fixture, playerSupportError, out error);
        }
        if (!TryCanMakeAlliance(
                fixture.Kingdom,
                fixture.TargetKingdom,
                fixture.Kingdom.RulingClan,
                includeReason: true,
                out canMakeAlliance,
                out TextObject reason,
                out string gateError))
        {
            return TryFailAllianceTimeoutFixturePreparation(fixture, gateError, out error);
        }
        if (canMakeAlliance) return true;

        gateRejected = true;
        return TryFailAllianceTimeoutFixturePreparation(
            fixture,
            $"Kingdom {fixture.Kingdom.StringId} cannot currently receive a valid alliance offer from Sturgia: {reason}",
            out error);
    }

    private static bool TryFailAllianceTimeoutFixturePreparation(
        AllianceTimeoutFixture fixture,
        string failure,
        out string error)
    {
        if (!fixture.TryRestoreEligibility(out string restoreError))
        {
            error = $"{failure} Fixture restoration failed: {restoreError}";
            return false;
        }

        error = failure;
        return false;
    }

    private static bool TryCanMakeAlliance(
        Kingdom kingdom,
        Kingdom targetKingdom,
        IFaction evaluatingFaction,
        bool includeReason,
        out bool canMakeAlliance,
        out TextObject reason,
        out string error)
    {
        canMakeAlliance = false;
        reason = null;
        error = string.Empty;
        try
        {
            canMakeAlliance = Campaign.Current.Models.AllianceModel.CanMakeAlliance(
                kingdom,
                targetKingdom,
                evaluatingFaction,
                out reason,
                includeReason);
            return true;
        }
        catch (Exception exception)
        {
            error = $"Alliance eligibility evaluation failed: {exception.Message}";
            return false;
        }
    }

    private static bool TryVerifyUnchangedAllianceState(AllianceTimeoutFixture fixture, out string error)
    {
        error = string.Empty;
        AllianceCampaignBehavior allianceBehavior = Campaign.Current?.GetCampaignBehavior<AllianceCampaignBehavior>();
        if (allianceBehavior == null)
        {
            error = "The alliance campaign behavior is unavailable.";
            return false;
        }

        try
        {
            if (allianceBehavior.IsAllyWithKingdom(fixture.Kingdom, fixture.TargetKingdom) == fixture.AllianceWasActive)
                return true;

            error = "The alliance state changed; the alliance-timeout fixture will not alter it.";
            return false;
        }
        catch (Exception exception)
        {
            error = $"The alliance state could not be verified: {exception.Message}";
            return false;
        }
    }

    private static string FailAllianceTimeoutFixture(AllianceTimeoutFixture fixture, string failure)
    {
        if (TryRestoreAllianceTimeoutFixture(fixture, out string restoreError))
        {
            pendingAllianceTimeoutFixture = null;
            return failure;
        }

        return $"{failure} Fixture restoration failed: {restoreError}";
    }

    private static bool TryRestoreAllianceTimeoutFixture(AllianceTimeoutFixture fixture, out string error)
    {
        var errors = new List<string>();
        try
        {
            foreach (StartAllianceDecision decision in fixture.Kingdom.UnresolvedDecisions
                .OfType<StartAllianceDecision>()
                .Where(candidate => candidate.KingdomToStartAllianceWith == fixture.TargetKingdom)
                .ToList())
            {
                fixture.Kingdom.RemoveDecision(decision);
                CoopKingdomElection.RemoveTrackedPlayerAllianceOffer(decision);
            }
        }
        catch (Exception exception)
        {
            errors.Add($"The alliance decision could not be removed: {exception.Message}");
        }

        if (!TryVerifyUnchangedAllianceState(fixture, out string allianceStateError))
        {
            errors.Add(allianceStateError);
        }

        if (!fixture.TryRestoreEligibility(out string relationError))
        {
            errors.Add($"The alliance eligibility inputs were not restored: {relationError}");
        }
        if (HasMatchingAllianceDecision(fixture) ||
            fixture.Kingdom.UnresolvedDecisions.Count != fixture.UnresolvedDecisionCount)
        {
            errors.Add("The unresolved decision state was not restored.");
        }

        error = string.Join(" ", errors);
        return errors.Count == 0;
    }

    private static bool TryMatchPendingPolicyTimeoutFixture(
        string kingdomId,
        string proposerClanId,
        string policyId,
        bool policyWasActive,
        out PolicyTimeoutFixture fixture,
        out string error)
    {
        fixture = pendingPolicyTimeoutFixture;
        error = string.Empty;
        if (fixture == null)
        {
            error = "No policy-timeout fixture has been captured.";
            return false;
        }
        if (fixture.KingdomId != kingdomId || fixture.ProposerClanId != proposerClanId ||
            fixture.PolicyId != policyId || fixture.PolicyWasActive != policyWasActive)
        {
            error = "The policy-timeout fixture arguments do not match the captured state.";
            return false;
        }

        return true;
    }

    private static string LiveTestJsonResult(object value) =>
        "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(value);

    private static string PolicyTimeoutJsonResult(object value) => LiveTestJsonResult(value);

    private sealed class AllianceTimeoutFixture
    {
        private const float MinimumQuerierKingdomStrength = 1f;
        private const float ThreatScoreThreshold = 430f;
        private const float ThreatSelectionMargin = 1f;
        private const float ThreatScoreCoefficient = 130f;
        private const float ThreatScoreBaseline = 0.4f;
        private const float MaximumThreatExposure = 1.7f;
        private const float MaximumThreatPowerRatio = 3f;
        private readonly List<AllianceTimeoutClanStrength> normalizedThreatClanStrengths = new List<AllianceTimeoutClanStrength>();
        private readonly List<AllianceTimeoutRelation> normalizedSupporterRelations = new List<AllianceTimeoutRelation>();
        private readonly List<AllianceTimeoutMercenaryStatus> normalizedNpcElectionSupporters = new List<AllianceTimeoutMercenaryStatus>();

        public Kingdom Kingdom { get; }
        public Clan ProposerClan { get; }
        public Kingdom TargetKingdom { get; }
        public string KingdomId { get; }
        public string TargetKingdomId { get; }
        public bool AllianceWasActive { get; }
        public Hero EffectiveLeader { get; }
        public Hero EffectiveTargetLeader { get; }
        public int LeaderRelationBefore { get; }
        public int UnresolvedDecisionCount { get; }
        public CultureObject KingdomCultureBefore { get; }
        public CultureObject TargetKingdomCultureBefore { get; }
        public int KingdomLeaderHonorBefore { get; }
        public int TargetKingdomLeaderHonorBefore { get; }
        public bool LeaderRelationChanged { get; private set; }
        public bool LeaderRelationRestored { get; private set; }
        public bool KingdomCultureChanged { get; private set; }
        public bool TargetKingdomCultureChanged { get; private set; }
        public bool KingdomLeaderHonorChanged { get; private set; }
        public bool TargetKingdomLeaderHonorChanged { get; private set; }
        public bool EligibilityRestored { get; private set; }
        public bool NpcElectionSupportersRestored { get; private set; }
        public bool EligibilityInputsChanged =>
            LeaderRelationChanged || KingdomCultureChanged || TargetKingdomCultureChanged ||
            KingdomLeaderHonorChanged || TargetKingdomLeaderHonorChanged ||
            normalizedThreatClanStrengths.Count != 0 || normalizedSupporterRelations.Count != 0 ||
            normalizedNpcElectionSupporters.Count != 0;
        public int NormalizedThreatClanCount => normalizedThreatClanStrengths.Count;
        public int NormalizedSupporterRelationCount => normalizedSupporterRelations.Count;
        public int NormalizedNpcElectionSupporterCount => normalizedNpcElectionSupporters.Count;
        public int CurrentLeaderRelation => CharacterRelationManager.GetHeroRelation(EffectiveLeader, EffectiveTargetLeader);
        public bool RequiresPlayerSupportNormalization =>
            ProposerClan != null && ProposerClan.Kingdom == Kingdom && ProposerClan != Kingdom.RulingClan;

        public AllianceTimeoutFixture(
            Kingdom kingdom,
            Clan proposerClan,
            Kingdom targetKingdom,
            string kingdomId,
            string targetKingdomId,
            bool allianceWasActive,
            Hero effectiveLeader,
            Hero effectiveTargetLeader,
            int leaderRelationBefore)
        {
            Kingdom = kingdom;
            ProposerClan = proposerClan;
            TargetKingdom = targetKingdom;
            KingdomId = kingdomId;
            TargetKingdomId = targetKingdomId;
            AllianceWasActive = allianceWasActive;
            EffectiveLeader = effectiveLeader;
            EffectiveTargetLeader = effectiveTargetLeader;
            LeaderRelationBefore = leaderRelationBefore;
            UnresolvedDecisionCount = kingdom.UnresolvedDecisions.Count;
            KingdomCultureBefore = kingdom.Culture;
            TargetKingdomCultureBefore = targetKingdom.Culture;
            KingdomLeaderHonorBefore = kingdom.Leader.GetTraitLevel(DefaultTraits.Honor);
            TargetKingdomLeaderHonorBefore = targetKingdom.Leader.GetTraitLevel(DefaultTraits.Honor);
            LeaderRelationRestored = true;
            NpcElectionSupportersRestored = true;
            EligibilityRestored = true;
        }

        public bool TryNormalizeLeaderRelation(out string error)
        {
            error = string.Empty;
            int maximumRelation = Campaign.Current.Models.DiplomacyModel.MaxRelationLimit;
            int currentRelation = CurrentLeaderRelation;
            if (currentRelation == maximumRelation) return true;

            if (!TrySetLeaderRelation(maximumRelation, out string normalizationError))
            {
                if (!TryRestoreLeaderRelation(out string normalizationExceptionRestoreError))
                {
                    error = $"The normalized leader relation could not be restored: {normalizationExceptionRestoreError}";
                    return false;
                }

                error = $"The alliance-timeout fixture could not normalize the leader relation: {normalizationError}";
                return false;
            }
            if (CurrentLeaderRelation == maximumRelation)
            {
                LeaderRelationChanged = true;
                LeaderRelationRestored = false;
                EligibilityRestored = false;
                return true;
            }

            if (!TryRestoreLeaderRelation(out string normalizationRestoreError))
            {
                error = $"The normalized leader relation could not be restored: {normalizationRestoreError}";
                return false;
            }

            error = "The alliance-timeout fixture could not normalize the leader relation.";
            return false;
        }

        public bool TryRestoreLeaderRelation(out string error)
        {
            error = string.Empty;
            int currentRelation = CurrentLeaderRelation;
            if (currentRelation != LeaderRelationBefore)
            {
                if (!TrySetLeaderRelation(LeaderRelationBefore, out string restoreError))
                {
                    error = restoreError;
                }
            }

            LeaderRelationRestored = CurrentLeaderRelation == LeaderRelationBefore;
            if (!LeaderRelationRestored)
            {
                error = string.IsNullOrEmpty(error)
                    ? $"Expected {LeaderRelationBefore}, found {CurrentLeaderRelation}."
                    : $"{error} Expected {LeaderRelationBefore}, found {CurrentLeaderRelation}.";
            }

            return LeaderRelationRestored;
        }

        public bool TryNormalizeThreatStrengths(out string error)
        {
            error = string.Empty;
            if (!TryEnsureKingdomStrength(Kingdom, MinimumQuerierKingdomStrength, out error)) return false;
            if (!TryEnsureKingdomStrength(TargetKingdom, MinimumQuerierKingdomStrength, out error)) return false;
            if (!TryNormalizeThreateningNeighbor(Kingdom, TargetKingdom, out error)) return false;
            if (!TryEnsureKingdomStrength(TargetKingdom, MinimumQuerierKingdomStrength, out error)) return false;
            if (!TryNormalizeThreateningNeighbor(TargetKingdom, Kingdom, out error)) return false;
            if (!TryEnsureKingdomStrength(Kingdom, MinimumQuerierKingdomStrength, out error)) return false;
            return TryEnsureKingdomStrength(TargetKingdom, MinimumQuerierKingdomStrength, out error);
        }

        public bool TryNormalizeKingdomCultures(out string error)
        {
            if (!TryNormalizeKingdomCulture(Kingdom, TargetKingdom, true, out error)) return false;
            return TryNormalizeKingdomCulture(TargetKingdom, Kingdom, false, out error);
        }

        public bool TryNormalizeLeaderHonor(out string error)
        {
            if (!TrySetKingdomLeaderHonor(1, out error)) return false;
            return TrySetTargetKingdomLeaderHonor(1, out error);
        }

        public bool TryNormalizePlayerSupport(out string error)
        {
            error = string.Empty;
            if (!RequiresPlayerSupportNormalization) return true;

            int maximumRelation = Campaign.Current.Models.DiplomacyModel.MaxRelationLimit;
            if (!TrySetLeaderRelation(maximumRelation, out error)) return false;
            if (!TrySetKingdomLeaderHonor(1, out error)) return false;
            if (!TrySetTargetKingdomLeaderHonor(1, out error)) return false;

            foreach (Clan supporterClan in Kingdom.Clans
                     .Where(candidate =>
                         !candidate.IsUnderMercenaryService && candidate != ProposerClan &&
                         candidate != Kingdom.RulingClan && candidate.Leader != null))
            {
                try
                {
                    Campaign.Current.Models.DiplomacyModel.GetHeroesForEffectiveRelation(
                        supporterClan.Leader,
                        TargetKingdom.Leader,
                        out Hero effectiveSupporter,
                        out Hero effectiveTarget);
                    if (effectiveSupporter == null || effectiveTarget == null || effectiveSupporter == effectiveTarget)
                    {
                        error = "The alliance-timeout fixture could not resolve a supporter relation.";
                        return false;
                    }
                    if (IsLeaderRelation(effectiveSupporter, effectiveTarget) ||
                        normalizedSupporterRelations.Any(snapshot => snapshot.IsFor(effectiveSupporter, effectiveTarget)))
                    {
                        continue;
                    }

                    int currentRelation = CharacterRelationManager.GetHeroRelation(effectiveSupporter, effectiveTarget);
                    if (currentRelation == maximumRelation) continue;

                    var snapshot = new AllianceTimeoutRelation(
                        effectiveSupporter,
                        effectiveTarget,
                        currentRelation);
                    normalizedSupporterRelations.Add(snapshot);
                    CharacterRelationManager.SetHeroRelation(effectiveSupporter, effectiveTarget, maximumRelation);
                    if (CharacterRelationManager.GetHeroRelation(effectiveSupporter, effectiveTarget) != maximumRelation)
                    {
                        error = $"Expected supporter relation {maximumRelation}, found " +
                            $"{CharacterRelationManager.GetHeroRelation(effectiveSupporter, effectiveTarget)}.";
                        return false;
                    }

                    EligibilityRestored = false;
                }
                catch (Exception exception)
                {
                    error = $"The alliance-timeout fixture could not normalize supporter relation: {exception.Message}";
                    return false;
                }
            }

            return true;
        }

        public bool TryNormalizeNpcElectionSupporters(out string error)
        {
            error = string.Empty;
            if (ProposerClan == null || ProposerClan.Kingdom != Kingdom)
            {
                error = "The alliance-timeout fixture no longer has a testclient voting clan.";
                return false;
            }
            if (ProposerClan.IsUnderMercenaryService)
            {
                error = "The testclient voting clan cannot be under mercenary service.";
                return false;
            }
            if (normalizedNpcElectionSupporters.Count != 0)
            {
                error = "The alliance-timeout fixture has already initialized its election supporters.";
                return false;
            }

            foreach (Clan clan in Kingdom.Clans
                     .Where(candidate => candidate != null && candidate != ProposerClan &&
                         !candidate.IsUnderMercenaryService)
                     .OrderBy(candidate => candidate.StringId, StringComparer.Ordinal))
            {
                if (clan.Leader?.IsHumanPlayerCharacter == true)
                {
                    return TryFailNpcElectionSupporterNormalization(
                        "The alliance-timeout fixture requires testclient to be the only non-mercenary human supporter.",
                        out error);
                }

                var snapshot = new AllianceTimeoutMercenaryStatus(clan, clan.IsUnderMercenaryService);
                normalizedNpcElectionSupporters.Add(snapshot);
                try
                {
                    clan.IsUnderMercenaryService = true;
                }
                catch (Exception exception)
                {
                    return TryFailNpcElectionSupporterNormalization(
                        $"The alliance-timeout fixture could not initialize election supporters: {exception.Message}",
                        out error);
                }
                if (!clan.IsUnderMercenaryService)
                {
                    return TryFailNpcElectionSupporterNormalization(
                        $"Expected {clan.StringId} to be under mercenary service during election setup.",
                        out error);
                }
            }

            if (normalizedNpcElectionSupporters.Count == 0)
            {
                error = "The alliance-timeout fixture could not find an NPC election supporter to initialize.";
                return false;
            }

            NpcElectionSupportersRestored = false;
            EligibilityRestored = false;
            return true;
        }

        private bool TryFailNpcElectionSupporterNormalization(string failure, out string error)
        {
            if (TryRestoreNpcElectionSupporters(out string restoreError))
            {
                error = failure;
                return false;
            }

            error = $"{failure} Fixture election supporter restoration failed: {restoreError}";
            return false;
        }

        public bool TryRestoreNpcElectionSupporters(out string error)
        {
            var errors = new List<string>();
            foreach (AllianceTimeoutMercenaryStatus snapshot in normalizedNpcElectionSupporters)
            {
                try
                {
                    if (snapshot.CurrentValue != snapshot.Value)
                    {
                        snapshot.Clan.IsUnderMercenaryService = snapshot.Value;
                    }
                    if (snapshot.CurrentValue != snapshot.Value)
                    {
                        errors.Add($"Expected {snapshot.Clan.StringId} mercenary status {snapshot.Value}, found {snapshot.CurrentValue}.");
                    }
                }
                catch (Exception exception)
                {
                    errors.Add($"The election supporter {snapshot.Clan.StringId} could not be restored: {exception.Message}");
                }
            }

            NpcElectionSupportersRestored = errors.Count == 0 &&
                normalizedNpcElectionSupporters.All(snapshot => snapshot.CurrentValue == snapshot.Value);
            if (!NpcElectionSupportersRestored && errors.Count == 0)
            {
                errors.Add("The alliance-timeout election supporters did not restore to their captured values.");
            }

            error = string.Join(" ", errors);
            return NpcElectionSupportersRestored;
        }

        public bool TryRestoreEligibility(out string error)
        {
            var errors = new List<string>();
            if (!TryRestoreNpcElectionSupporters(out string npcElectionSupporterError)) errors.Add(npcElectionSupporterError);
            TryRestoreSupporterRelations(errors);
            if (!TryRestoreLeaderRelation(out string leaderRelationError)) errors.Add(leaderRelationError);
            TryRestoreKingdomLeaderHonor(errors);
            TryRestoreTargetKingdomLeaderHonor(errors);
            TryRestoreKingdomCultures(errors);
            TryRestoreThreatStrengths(errors);

            EligibilityRestored = errors.Count == 0 &&
                LeaderRelationRestored &&
                Kingdom.Culture == KingdomCultureBefore &&
                TargetKingdom.Culture == TargetKingdomCultureBefore &&
                Kingdom.Leader.GetTraitLevel(DefaultTraits.Honor) == KingdomLeaderHonorBefore &&
                TargetKingdom.Leader.GetTraitLevel(DefaultTraits.Honor) == TargetKingdomLeaderHonorBefore &&
                normalizedThreatClanStrengths.All(snapshot => snapshot.Clan.CurrentTotalStrength == snapshot.Value) &&
                normalizedSupporterRelations.All(snapshot => snapshot.CurrentValue == snapshot.Value) &&
                normalizedNpcElectionSupporters.All(snapshot => snapshot.CurrentValue == snapshot.Value) &&
                NpcElectionSupportersRestored;
            if (!EligibilityRestored && errors.Count == 0)
            {
                errors.Add("The alliance-timeout eligibility inputs did not restore to their captured values.");
            }

            error = string.Join(" ", errors);
            return EligibilityRestored;
        }

        private bool TryNormalizeKingdomCulture(
            Kingdom kingdom,
            Kingdom otherKingdom,
            bool isFixtureKingdom,
            out string error)
        {
            error = string.Empty;
            if (kingdom.Culture == null || !otherKingdom.Fiefs.Any(fief => fief.Culture == kingdom.Culture)) return true;

            CultureObject culture = MBObjectManager.Instance.GetObjectTypeList<CultureObject>()
                .Where(candidate => candidate != null && !otherKingdom.Fiefs.Any(fief => fief.Culture == candidate))
                .OrderBy(candidate => candidate.StringId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (culture == null)
            {
                error = $"The alliance-timeout fixture could not find a reversible culture for {kingdom.StringId}.";
                return false;
            }

            try
            {
                kingdom.Culture = culture;
            }
            catch (Exception exception)
            {
                error = $"The alliance-timeout fixture could not normalize kingdom culture: {exception.Message}";
                return false;
            }
            if (kingdom.Culture != culture)
            {
                error = "The alliance-timeout fixture could not normalize kingdom culture.";
                return false;
            }

            if (isFixtureKingdom) KingdomCultureChanged = true;
            else TargetKingdomCultureChanged = true;
            EligibilityRestored = false;
            return true;
        }

        private bool TrySetKingdomLeaderHonor(int honor, out string error)
        {
            return TrySetLeaderHonor(Kingdom.Leader, honor, true, out error);
        }

        private bool TrySetTargetKingdomLeaderHonor(int honor, out string error)
        {
            return TrySetLeaderHonor(TargetKingdom.Leader, honor, false, out error);
        }

        private bool TrySetLeaderHonor(Hero leader, int honor, bool isFixtureKingdom, out string error)
        {
            error = string.Empty;
            if (leader.GetTraitLevel(DefaultTraits.Honor) == honor) return true;

            try
            {
                leader.SetTraitLevel(DefaultTraits.Honor, honor);
            }
            catch (Exception exception)
            {
                error = $"The alliance-timeout fixture could not normalize ruler honor: {exception.Message}";
                return false;
            }
            if (leader.GetTraitLevel(DefaultTraits.Honor) != honor)
            {
                error = $"Expected ruler honor {honor}, found {leader.GetTraitLevel(DefaultTraits.Honor)}.";
                return false;
            }

            if (isFixtureKingdom) KingdomLeaderHonorChanged = true;
            else TargetKingdomLeaderHonorChanged = true;
            EligibilityRestored = false;
            return true;
        }

        private void TryRestoreSupporterRelations(List<string> errors)
        {
            foreach (AllianceTimeoutRelation snapshot in normalizedSupporterRelations)
            {
                try
                {
                    if (snapshot.CurrentValue != snapshot.Value)
                    {
                        CharacterRelationManager.SetHeroRelation(snapshot.First, snapshot.Second, snapshot.Value);
                    }
                    if (snapshot.CurrentValue != snapshot.Value)
                    {
                        errors.Add($"Expected supporter relation {snapshot.Value}, found {snapshot.CurrentValue}.");
                    }
                }
                catch (Exception exception)
                {
                    errors.Add($"The supporter relation could not be restored: {exception.Message}");
                }
            }
        }

        private void TryRestoreKingdomLeaderHonor(List<string> errors)
        {
            TryRestoreLeaderHonor(Kingdom.Leader, KingdomLeaderHonorBefore, errors);
        }

        private void TryRestoreTargetKingdomLeaderHonor(List<string> errors)
        {
            TryRestoreLeaderHonor(TargetKingdom.Leader, TargetKingdomLeaderHonorBefore, errors);
        }

        private static void TryRestoreLeaderHonor(Hero leader, int honor, List<string> errors)
        {
            try
            {
                if (leader.GetTraitLevel(DefaultTraits.Honor) != honor)
                {
                    leader.SetTraitLevel(DefaultTraits.Honor, honor);
                }
                if (leader.GetTraitLevel(DefaultTraits.Honor) != honor)
                {
                    errors.Add($"Expected ruler honor {honor}, found {leader.GetTraitLevel(DefaultTraits.Honor)}.");
                }
            }
            catch (Exception exception)
            {
                errors.Add($"The ruler honor could not be restored: {exception.Message}");
            }
        }

        private void TryRestoreKingdomCultures(List<string> errors)
        {
            TryRestoreKingdomCulture(Kingdom, KingdomCultureBefore, errors);
            TryRestoreKingdomCulture(TargetKingdom, TargetKingdomCultureBefore, errors);
        }

        private static void TryRestoreKingdomCulture(Kingdom kingdom, CultureObject culture, List<string> errors)
        {
            try
            {
                if (kingdom.Culture != culture) kingdom.Culture = culture;
                if (kingdom.Culture != culture)
                {
                    errors.Add($"The culture for {kingdom.StringId} was not restored.");
                }
            }
            catch (Exception exception)
            {
                errors.Add($"The culture for {kingdom.StringId} could not be restored: {exception.Message}");
            }
        }

        private void TryRestoreThreatStrengths(List<string> errors)
        {
            foreach (AllianceTimeoutClanStrength snapshot in normalizedThreatClanStrengths)
            {
                try
                {
                    if (snapshot.Clan.CurrentTotalStrength != snapshot.Value)
                    {
                        snapshot.Clan.CurrentTotalStrength = snapshot.Value;
                    }
                    if (snapshot.Clan.CurrentTotalStrength != snapshot.Value)
                    {
                        errors.Add($"Expected threat strength {snapshot.Value}, found {snapshot.Clan.CurrentTotalStrength}.");
                    }
                }
                catch (Exception exception)
                {
                    errors.Add($"Threat strength could not be restored: {exception.Message}");
                }
            }
        }

        private bool TryNormalizeThreateningNeighbor(
            Kingdom querierKingdom,
            Kingdom otherKingdom,
            out string error)
        {
            error = string.Empty;
            List<AllianceTimeoutNeighbor> neighbors = GetAllianceTimeoutNeighbors(querierKingdom);
            float querierStrength = GetKingdomStrength(querierKingdom);
            AllianceTimeoutNeighbor threatNeighbor = neighbors
                .Where(candidate => candidate.Kingdom != otherKingdom && !candidate.Kingdom.IsEliminated)
                .OrderByDescending(candidate => GetMaximumThreatScore(candidate, querierStrength))
                .ThenBy(candidate => candidate.Kingdom.StringId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (threatNeighbor == null ||
                GetMaximumThreatScore(threatNeighbor, querierStrength) <= ThreatScoreThreshold) return true;

            float requiredStrength = GetRequiredThreatStrength(
                threatNeighbor,
                querierStrength,
                ThreatScoreThreshold + ThreatSelectionMargin);
            if (!TryEnsureKingdomStrength(threatNeighbor.Kingdom, requiredStrength, out error)) return false;

            float selectedThreatScore = GetThreatScore(threatNeighbor, querierStrength);
            foreach (AllianceTimeoutNeighbor competingNeighbor in neighbors
                     .Where(candidate => candidate.Kingdom != threatNeighbor.Kingdom))
            {
                if (GetThreatScore(competingNeighbor, querierStrength) < selectedThreatScore) continue;

                float maximumStrength = GetMaximumThreatStrength(
                    competingNeighbor,
                    querierStrength,
                    selectedThreatScore - ThreatSelectionMargin);
                if (!TryLimitKingdomStrength(competingNeighbor.Kingdom, maximumStrength, out error)) return false;
            }

            AllianceTimeoutNeighbor selectedThreat = GetVanillaThreateningNeighbor(querierKingdom, out float threatScore);
            if (selectedThreat?.Kingdom == threatNeighbor.Kingdom && threatScore > ThreatScoreThreshold) return true;

            error = $"The alliance-timeout fixture could not select {threatNeighbor.Kingdom.StringId} " +
                "as the vanilla threatening neighbor.";
            return false;
        }

        private bool TryEnsureKingdomStrength(Kingdom kingdom, float requiredStrength, out string error)
        {
            error = string.Empty;
            float currentStrength = GetKingdomStrength(kingdom);
            if (currentStrength >= requiredStrength) return true;

            Clan clan = kingdom.Clans
                .Where(candidate => !candidate.IsUnderMercenaryService)
                .OrderBy(candidate => candidate.StringId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (clan == null)
            {
                error = $"The alliance-timeout fixture could not find a non-mercenary clan for {kingdom.StringId}.";
                return false;
            }

            float normalizedStrength = clan.CurrentTotalStrength + requiredStrength - currentStrength;
            if (!TrySetClanStrength(clan, normalizedStrength, out error)) return false;
            if (GetKingdomStrength(kingdom) < requiredStrength)
            {
                error = $"Expected kingdom strength {requiredStrength}, found {GetKingdomStrength(kingdom)}.";
                return false;
            }

            EligibilityRestored = false;
            return true;
        }

        private bool TryLimitKingdomStrength(Kingdom kingdom, float maximumStrength, out string error)
        {
            error = string.Empty;
            float currentStrength = GetKingdomStrength(kingdom);
            if (currentStrength < maximumStrength) return true;

            float remainingReduction = currentStrength - maximumStrength + ThreatSelectionMargin;
            foreach (Clan clan in kingdom.Clans
                     .Where(candidate => !candidate.IsUnderMercenaryService)
                     .OrderBy(candidate => candidate.StringId, StringComparer.Ordinal))
            {
                if (remainingReduction <= 0f) break;

                float reduction = MathF.Min(clan.CurrentTotalStrength, remainingReduction);
                if (reduction <= 0f) continue;
                if (!TrySetClanStrength(clan, clan.CurrentTotalStrength - reduction, out error)) return false;
                remainingReduction -= reduction;
            }

            if (GetKingdomStrength(kingdom) >= maximumStrength)
            {
                error = $"Expected kingdom strength below {maximumStrength}, found {GetKingdomStrength(kingdom)}.";
                return false;
            }

            EligibilityRestored = false;
            return true;
        }

        private bool TrySetClanStrength(Clan clan, float strength, out string error)
        {
            error = string.Empty;
            if (!normalizedThreatClanStrengths.Any(snapshot => snapshot.Clan == clan))
            {
                normalizedThreatClanStrengths.Add(new AllianceTimeoutClanStrength(clan, clan.CurrentTotalStrength));
            }

            try
            {
                clan.CurrentTotalStrength = strength;
            }
            catch (Exception exception)
            {
                error = $"The alliance-timeout fixture could not normalize threat strength: {exception.Message}";
                return false;
            }

            return true;
        }

        private AllianceTimeoutNeighbor GetVanillaThreateningNeighbor(
            Kingdom querierKingdom,
            out float threatScore)
        {
            AllianceTimeoutNeighbor selectedNeighbor = null;
            threatScore = 0f;
            float querierStrength = GetKingdomStrength(querierKingdom);
            foreach (AllianceTimeoutNeighbor neighbor in GetAllianceTimeoutNeighbors(querierKingdom))
            {
                float candidateThreatScore = GetThreatScore(neighbor, querierStrength);
                if (threatScore < candidateThreatScore)
                {
                    selectedNeighbor = neighbor;
                    threatScore = candidateThreatScore;
                }
            }

            return selectedNeighbor;
        }

        private static float GetMaximumThreatScore(AllianceTimeoutNeighbor neighbor, float querierStrength)
        {
            if (querierStrength <= 0f || neighbor.TotalNeighborScore <= 0f) return 0f;

            float exposureScore = MBMath.Map(
                neighbor.NeighborScore / neighbor.TotalNeighborScore,
                0f,
                1f,
                1f,
                2f);
            return (MathF.Min(exposureScore, MaximumThreatExposure) + ThreatScoreBaseline +
                MaximumThreatPowerRatio) * ThreatScoreCoefficient;
        }

        private float GetThreatScore(AllianceTimeoutNeighbor neighbor, float querierStrength)
        {
            if (querierStrength <= 0f || neighbor.TotalNeighborScore <= 0f) return 0f;

            float exposureScore = MBMath.Map(
                neighbor.NeighborScore / neighbor.TotalNeighborScore,
                0f,
                1f,
                1f,
                2f);
            float powerRatio = MathF.Clamp(
                GetKingdomStrength(neighbor.Kingdom) / querierStrength,
                0f,
                MaximumThreatPowerRatio);
            return (MathF.Min(exposureScore, MaximumThreatExposure) + ThreatScoreBaseline +
                powerRatio) * ThreatScoreCoefficient;
        }

        private static float GetRequiredThreatStrength(
            AllianceTimeoutNeighbor neighbor,
            float querierStrength,
            float requiredThreatScore)
        {
            float exposureScore = MBMath.Map(
                neighbor.NeighborScore / neighbor.TotalNeighborScore,
                0f,
                1f,
                1f,
                2f);
            float requiredPowerRatio = (requiredThreatScore / ThreatScoreCoefficient) -
                MathF.Min(exposureScore, MaximumThreatExposure) - ThreatScoreBaseline;
            return querierStrength * MathF.Clamp(requiredPowerRatio, 0f, MaximumThreatPowerRatio);
        }

        private static float GetMaximumThreatStrength(
            AllianceTimeoutNeighbor neighbor,
            float querierStrength,
            float maximumThreatScore)
        {
            float exposureScore = MBMath.Map(
                neighbor.NeighborScore / neighbor.TotalNeighborScore,
                0f,
                1f,
                1f,
                2f);
            float maximumPowerRatio = (maximumThreatScore / ThreatScoreCoefficient) -
                MathF.Min(exposureScore, MaximumThreatExposure) - ThreatScoreBaseline;
            return querierStrength * MathF.Clamp(maximumPowerRatio, 0f, MaximumThreatPowerRatio);
        }

        private bool IsLeaderRelation(Hero first, Hero second) =>
            (first == EffectiveLeader && second == EffectiveTargetLeader) ||
            (first == EffectiveTargetLeader && second == EffectiveLeader);

        private static float GetKingdomStrength(Kingdom kingdom) => kingdom.Clans
            .Where(clan => !clan.IsUnderMercenaryService)
            .Sum(clan => clan.CurrentTotalStrength);

        private bool TrySetLeaderRelation(int relation, out string error)
        {
            error = string.Empty;
            try
            {
                CharacterRelationManager.SetHeroRelation(EffectiveLeader, EffectiveTargetLeader, relation);
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }

            if (CurrentLeaderRelation == relation) return true;

            error = $"Expected {relation}, found {CurrentLeaderRelation}.";
            return false;
        }
    }

    private sealed class AllianceTimeoutClanStrength
    {
        public Clan Clan { get; }
        public float Value { get; }

        public AllianceTimeoutClanStrength(Clan clan, float value)
        {
            Clan = clan;
            Value = value;
        }
    }

    private sealed class AllianceTimeoutRelation
    {
        public Hero First { get; }
        public Hero Second { get; }
        public int Value { get; }
        public int CurrentValue => CharacterRelationManager.GetHeroRelation(First, Second);

        public AllianceTimeoutRelation(Hero first, Hero second, int value)
        {
            First = first;
            Second = second;
            Value = value;
        }

        public bool IsFor(Hero first, Hero second) =>
            (first == First && second == Second) || (first == Second && second == First);
    }

    private sealed class AllianceTimeoutMercenaryStatus
    {
        public Clan Clan { get; }
        public bool Value { get; }
        public bool CurrentValue => Clan.IsUnderMercenaryService;

        public AllianceTimeoutMercenaryStatus(Clan clan, bool value)
        {
            Clan = clan;
            Value = value;
        }
    }

    private sealed class AllianceTimeoutNeighbor
    {
        public Kingdom Kingdom { get; }
        public float NeighborScore { get; }
        public float TotalNeighborScore { get; }

        public AllianceTimeoutNeighbor(Kingdom kingdom, float neighborScore, float totalNeighborScore)
        {
            Kingdom = kingdom;
            NeighborScore = neighborScore;
            TotalNeighborScore = totalNeighborScore;
        }
    }

    private sealed class PolicyTimeoutFixture
    {
        public Kingdom Kingdom { get; }
        public Clan ProposerClan { get; }
        public PolicyObject Policy { get; }
        public string KingdomId { get; }
        public string ProposerClanId { get; }
        public string PolicyId { get; }
        public bool PolicyWasActive { get; }
        public bool DecisionStaged { get; set; }

        public PolicyTimeoutFixture(
            Kingdom kingdom,
            Clan proposerClan,
            PolicyObject policy,
            string kingdomId,
            string proposerClanId,
            string policyId,
            bool policyWasActive)
        {
            Kingdom = kingdom;
            ProposerClan = proposerClan;
            Policy = policy;
            KingdomId = kingdomId;
            ProposerClanId = proposerClanId;
            PolicyId = policyId;
            PolicyWasActive = policyWasActive;
        }
    }

    // coop.debug.kingdom.create Derthert Vlandia_Reborn
    /// <summary>
    /// Creates a kingdom ruled by the named hero's clan and replicates it to every client through the
    /// same notification the governor "create kingdom" dialog uses. Server only.
    /// </summary>
    /// <param name="args">leader hero (coop id, game StringId, or name with '_' for spaces), then the kingdom name</param>
    /// <returns>result message</returns>
    [CommandLineArgumentFunction("create", "coop.debug.kingdom")]
    public static string CreateKingdomCommand(List<string> args)
    {
        if (!ModInformation.IsServer) return "This command can only be run on the server.";
        if (args.Count < 2) return CreateUsage;
        if (Campaign.Current == null) return "No campaign is loaded.";
        if (!TryGetObjectManager(out var objectManager)) return "Unable to resolve ObjectManager";
        if (!ContainerProvider.TryResolve<IKingdomCreator>(out var kingdomCreator)) return "Unable to resolve KingdomCreator";

        if (!TryGetLeaderHero(objectManager, args[0], out Hero leader, out string heroError)) return heroError;
        if (leader.IsDead) return $"{leader.Name} ({leader.StringId}) is dead and cannot rule a kingdom.";

        Clan clan = leader.Clan;
        if (clan == null) return $"{leader.Name} ({leader.StringId}) does not belong to a clan.";

        // A kingdom is ruled by its ruling clan's leader, and clan leadership changes have no sync yet,
        // so reject rather than silently create a kingdom ruled by a different hero.
        if (clan.Leader != leader) return $"{leader.Name} does not lead clan {clan.StringId}. Pass the clan leader instead.";

        // The console splits arguments on spaces, so everything after the hero is the kingdom name.
        string kingdomName = string.Join(" ", args.Skip(1)).Trim();
        if (!KingdomHandler.CanCreateKingdomForClan(clan, kingdomName, out string reason))
        {
            return $"Unable to create kingdom {kingdomName}: {reason}.";
        }

        CultureObject culture = clan.Culture ?? leader.Culture;
        if (culture == null) return $"Clan {clan.StringId} has no culture for the new kingdom to inherit.";

        // A debug-created kingdom usually has no owning player; an empty controller id makes the
        // notification's settlement-restore steps a no-op on the server and every client.
        TryGetPlayerManager(out var playerManager);
        objectManager.TryGetId(clan, out string clanId);
        string controllerId = playerManager?.Players.FirstOrDefault(player => player.ClanId == clanId)?.ControllerId ?? string.Empty;

        if (!kingdomCreator.TryCreateKingdom(clan, kingdomName, culture, controllerId, out string kingdomId, out string createError))
        {
            return $"Unable to create kingdom {kingdomName}: {createError}.";
        }

        return $"Created kingdom '{kingdomName}' ({kingdomId}) ruled by {leader.Name} of clan {clan.StringId}.";
    }

    /// <summary>
    /// Resolves the leader from a coop object manager id, a game StringId, or a hero name ('_' stands in
    /// for a space because the console splits arguments on spaces).
    /// </summary>
    private static bool TryGetLeaderHero(IObjectManager objectManager, string nameOrId, out Hero hero, out string error)
    {
        error = null;
        if (objectManager.TryGetObject(nameOrId, out hero)) return true;

        string heroName = nameOrId.Replace('_', ' ').Trim();
        hero = Hero.AllAliveHeroes.FirstOrDefault(candidate => candidate.StringId == nameOrId)
            ?? Hero.AllAliveHeroes.FirstOrDefault(candidate => string.Equals(candidate.Name?.ToString(), heroName, StringComparison.OrdinalIgnoreCase));
        if (hero != null) return true;

        error = $"No hero '{nameOrId}' found by coop id, game StringId, or name. Run coop.debug.hero.id <hero name> to look one up.";
        return false;
    }

    // coop.debug.kingdom.list
    /// <summary>
    /// Lists all the current Kingdoms
    /// </summary>
    /// <param name="args">actually none are being used..</param>
    /// <returns>strings of all the kingdoms</returns>
    [CommandLineArgumentFunction("list", "coop.debug.kingdom")]
    public static string ListKingdoms(List<string> args)
    {
        StringBuilder stringBuilder = new StringBuilder();

        List<Kingdom> kingdoms = Campaign.Current.CampaignObjectManager.Kingdoms.ToList();
        kingdoms.ForEach((kingdom) =>
        {
            stringBuilder.Append(string.Format("Name: '{0}'\n Id : '{1}'\n", kingdom.Name, kingdom.StringId));
        });
        return stringBuilder.ToString();
    }

    // coop.debug.kingdom.info <kingdomId>
    /// <summary>
    /// Reflection-dumps every field of a Kingdom so a server screenshot and a client screenshot can be
    /// compared field-for-field to confirm Kingdom field syncs still replicate.
    /// </summary>
    [CommandLineArgumentFunction("info", "coop.debug.kingdom")]
    public static string Info(List<string> args)
    {
        if (args.Count != 1) return "Usage: coop.debug.kingdom.info <kingdomId>";
        if (!TryGetObjectManager(out IObjectManager objectManager)) return "Unable to resolve ObjectManager";
        if (objectManager.TryGetObject(args[0], out Kingdom kingdom) == false) return $"Unable to find kingdom with id: {args[0]}";

        StringBuilder stringBuilder = new StringBuilder();
        foreach (var field in typeof(Kingdom).GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
        {
            stringBuilder.AppendLine($"{field.Name} = {field.GetValue(kingdom)}");
        }
        return stringBuilder.ToString();
    }

    // coop.debug.kingdom.force_player_join_kingdom
    /// <summary>
    /// Forces a registered co-op player's clan to join a kingdom. Server only.
    /// </summary>
    /// <param name="args">controller id, kingdom id</param>
    /// <returns>result message</returns>
    [CommandLineArgumentFunction("force_player_join_kingdom", "coop.debug.kingdom")]
    public static string ForcePlayerJoinKingdom(List<string> args)
    {
        if (!ModInformation.IsServer)
        {
            return "This command can only be run on the server.";
        }

        if (args.Count != 2)
        {
            return "Usage: coop.debug.kingdom.force_player_join_kingdom <controllerId> <kingdomId>";
        }

        if (TryGetPlayerManager(out var playerManager) == false)
        {
            return "Unable to resolve PlayerManager";
        }

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (TryGetKingdomMembershipState(out var kingdomMembershipState) == false)
        {
            return "Unable to resolve KingdomMembershipState";
        }

        string controllerId = args[0];
        string kingdomId = args[1];

        if (!playerManager.TryGetPlayer(controllerId, out var player))
        {
            return $"Player not found with controller id: {controllerId}";
        }

        if (string.IsNullOrEmpty(player.ClanId))
        {
            return $"Player {controllerId} does not have a clan id.";
        }

        if (!objectManager.TryGetObject(player.ClanId, out Clan clan))
        {
            return $"Clan not found for player {controllerId} with clan id: {player.ClanId}";
        }

        if (!objectManager.TryGetObject(kingdomId, out Kingdom kingdom))
        {
            return $"Kingdom not found with id: {kingdomId}";
        }

        Kingdom previousKingdom = clan.Kingdom;
        if (previousKingdom == kingdom)
        {
            return $"Player {controllerId}'s clan {clan.StringId} is already in kingdom {kingdom.StringId}.";
        }

        // Server-authoritative apply: run with patches live (no AllowedThread) so membership
        // and fief collection changes replicate to clients.
        kingdomMembershipState.MoveClanToKingdom(
            previousKingdom,
            kingdom,
            clan,
            publishCollectionChanges: true);

        if (clan.Kingdom != kingdom)
        {
            string currentKingdomId = clan.Kingdom?.StringId ?? "<none>";
            return $"Tried to force player {controllerId}'s clan {clan.StringId} to join {kingdom.StringId}, but current kingdom is {currentKingdomId}.";
        }

        string previousKingdomId = previousKingdom?.StringId ?? "<none>";
        return $"Forced player {controllerId}'s clan {clan.StringId} to join kingdom {kingdom.StringId}. Previous kingdom: {previousKingdomId}.";
    }

    // coop.debug.kingdom.force_player_vassalage Player khuzait true
    [CommandLineArgumentFunction("force_player_vassalage", "coop.debug.kingdom")]
    public static string ForcePlayerVassalage(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "This command can only be run on the server.";
        }

        if (args.Count < 2 || args.Count > 3)
        {
            return "Usage: coop.debug.kingdom.force_player_vassalage <controllerId> <kingdomId> [grantRewards]";
        }

        if (!TryGetPlayerManager(out var playerManager))
        {
            return "Unable to resolve PlayerManager";
        }

        if (!playerManager.TryGetPlayer(args[0], out var player))
        {
            return $"Player not found with controller id: {args[0]}";
        }

        if (!playerManager.TryGetPeer(args[0], out var peer))
        {
            return $"Player {args[0]} does not have a connected peer.";
        }

        if (!TryGetObjectManager(out var objectManager) ||
            !objectManager.TryGetObject<Kingdom>(args[1], out var kingdom))
        {
            return $"Kingdom not found with id: {args[1]}";
        }

        bool grantRewards = true;
        if (args.Count == 3 && !bool.TryParse(args[2], out grantRewards))
        {
            return $"Unable to parse {args[2]} as a boolean.";
        }

        MessageBroker.Instance.Publish(peer, new RequestVassalService(kingdom.StringId, grantRewards));
        return $"Queued vassalage for player {player.ControllerId} in kingdom {kingdom.StringId}. GrantRewards={grantRewards}.";
    }

    // coop.debug.kingdom.add_decision_usage
    /// <summary>
    /// Lists all the usages of add_decision command.
    /// </summary>
    /// <param name="args">actually none are being used..</param>
    /// <returns>strings of all the usages</returns>
    [CommandLineArgumentFunction("add_decision_usage", "coop.debug.kingdom")]
    public static string AddDecisionUsage(List<string> args)
    {
        StringBuilder stringBuilder = new StringBuilder();

        stringBuilder.Append($"Basic usage: {AddBasicUsage}\n");
        stringBuilder.Append($"{AddDeclareWarDecisionUsage}\n");
        stringBuilder.Append($"{AddExpelClanFromKingdomDecisionUsage}\n");
        stringBuilder.Append($"{AddKingSelectionKingdomDecisionUsage}\n");
        stringBuilder.Append($"{AddKingdomPolicyDecisionUsage}\n");
        stringBuilder.Append($"{AddSettlementClaimantDecisionUsage}\n");
        stringBuilder.Append($"{AddSettlementClaimantPreliminaryDecisionUsage}\n");
        stringBuilder.Append($"{AddMakePeaceKingdomDecisionUsage}\n");
        stringBuilder.Append($"{AddAcceptCallToWarAgreementDecisionUsage}\n");
        stringBuilder.Append($"{AddProposeCallToWarAgreementDecisionUsage}\n");
        stringBuilder.Append($"{AddStartAllianceDecisionUsage}\n");
        stringBuilder.Append($"{AddTradeAgreementDecisionUsage}\n");

        return stringBuilder.ToString();
    }

    // coop.debug.kingdom.remove_decision_usage
    /// <summary>
    /// Returns the usage of remove_decision command's usage.
    /// </summary>
    /// <param name="args">actually none are being used..</param>
    /// <returns>strings of usage.</returns>
    [CommandLineArgumentFunction("remove_decision_usage", "coop.debug.kingdom")]
    public static string RemoveDecisionUsage(List<string> args)
    {
        StringBuilder stringBuilder = new StringBuilder();

        stringBuilder.Append(RemoveUsage);

        return stringBuilder.ToString();
    }

    // coop.debug.kingdom.list_decisions
    /// <summary>
    /// Lists all the decisions of a specific kingdom.
    /// </summary>
    /// <param name="args">actually none are being used..</param>
    /// <returns>strings of all the decisions of a specific kingdom</returns>
    [CommandLineArgumentFunction("list_kingdom_decisions", "coop.debug.kingdom")]
    public static string ListKingdomDecisions(List<string> args)
    {
        if (args.Count < 1)
        {
            return "Usage: coop.debug.kingdom.list_kingdom_decisions <kingdomId>";
        }

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (objectManager.TryGetObject(args[0], out Kingdom kingdom) == false)
        {
            return $"ID: '{args[0]}' not found";
        }

        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.Append($"Kingdom decisions of Kingdom: {kingdom.Name}\n");

        int i = 1;
        foreach (KingdomDecision kingdomDecision in kingdom.UnresolvedDecisions)
        {
            stringBuilder.Append($"{i}. {kingdomDecision.GetType().Name}\n");
            i++;
        }

        return stringBuilder.ToString();
    }

    // coop.debug.kingdom.decisions
    /// <summary>
    /// Lists active kingdom decisions and registered client vote state.
    /// </summary>
    /// <param name="args">first arg : kingdomId</param>
    /// <returns>strings of all active kingdom decisions with client votes</returns>
    [CommandLineArgumentFunction("decisions", "coop.debug.kingdom")]
    public static string ListKingdomDecisionVotes(List<string> args)
    {
        if (args.Count < 1)
        {
            return "Usage: coop.debug.kingdom.decisions <kingdomId>";
        }

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (TryGetPlayerManager(out var playerManager) == false)
        {
            return "Unable to resolve PlayerManager";
        }

        if (TryGetKingdomDecisionVoteManager(out var voteManager) == false)
        {
            return "Unable to resolve KingdomDecisionVoteManager";
        }

        if (objectManager.TryGetObject(args[0], out Kingdom kingdom) == false)
        {
            return $"ID: '{args[0]}' not found";
        }

        IReadOnlyList<KingdomDecisionVoteManager.KingdomDecisionDebugInfo> decisionInfos =
            voteManager.GetDecisionDebugInfo(kingdom);

        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.AppendLine($"Active decisions of Kingdom: {kingdom.Name} ({kingdom.StringId})");
        stringBuilder.AppendLine($"Registered clients: {playerManager.Players.Count}");

        if (decisionInfos.Count == 0)
        {
            stringBuilder.AppendLine("(none)");
            return stringBuilder.ToString();
        }

        foreach (KingdomDecisionVoteManager.KingdomDecisionDebugInfo decisionInfo in decisionInfos)
        {
            stringBuilder.AppendLine($"{decisionInfo.DecisionIndex + 1}. {decisionInfo.DecisionType}");
            if (decisionInfo.ClientVotes.Count == 0)
            {
                stringBuilder.AppendLine("  Clients: (none registered)");
                continue;
            }

            foreach (KingdomDecisionVoteManager.KingdomDecisionClientVoteDebugInfo clientVote in decisionInfo.ClientVotes)
            {
                string clanId = string.IsNullOrWhiteSpace(clientVote.ClanId) ? "<none>" : clientVote.ClanId;
                stringBuilder.Append($"  - {clientVote.ControllerId} | Clan: {clientVote.ClanName} ({clanId}) | {clientVote.Status}");

                if (!string.IsNullOrWhiteSpace(clientVote.SupportWeight))
                {
                    stringBuilder.Append($" | Support: {clientVote.SupportWeight}");
                }

                if (clientVote.HasVote && !clientVote.IsFinal)
                {
                    stringBuilder.Append(" | Not Final");
                }

                stringBuilder.AppendLine();
            }
        }

        return stringBuilder.ToString();
    }

    // coop.debug.kingdom.list_decision_outcomes
    /// <summary>
    /// Lists the outcomes for a queued kingdom decision.
    /// </summary>
    /// <param name="args">first arg : kingdomId ; second arg : 1-based decision index</param>
    /// <returns>strings of all outcomes of a decision</returns>
    [CommandLineArgumentFunction("list_decision_outcomes", "coop.debug.kingdom")]
    public static string ListKingdomDecisionOutcomes(List<string> args)
    {
        if (!TryGetKingdomDecisionByIndex(args, out Kingdom kingdom, out KingdomDecision decision, out int _, out string message))
        {
            return message;
        }

        KingdomElection election = new KingdomElection(decision);
        election.Setup();
        election.DetermineSupport(election._possibleOutcomes, false);
        decision.DetermineSponsors(election._possibleOutcomes);
        election.UpdateSupport(election._possibleOutcomes);

        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.Append($"Decision outcomes for {decision.GetType().Name} in {kingdom.Name}:\n");
        for (int i = 0; i < election._possibleOutcomes.Count; i++)
        {
            DecisionOutcome outcome = election._possibleOutcomes[i];
            string sponsor = outcome.SponsorClan == null ? "<none>" : outcome.SponsorClan.StringId;
            stringBuilder.Append($"{i + 1}. {outcome.GetDecisionTitle()} Sponsor: {sponsor} Support: {outcome.TotalSupportPoints}\n");
        }
        stringBuilder.Append("Use support weights: Choose, StayNeutral, SlightlyFavor, StronglyFavor, FullyPush.\n");
        return stringBuilder.ToString();
    }

    // coop.debug.kingdom.vote_decision
    /// <summary>
    /// Requests a vote for a queued kingdom decision from the local client.
    /// </summary>
    /// <param name="args">kingdomId, 1-based decision index, 1-based outcome index or abstain, support weight</param>
    /// <returns>result message</returns>
    [CommandLineArgumentFunction("vote_decision", "coop.debug.kingdom")]
    public static string VoteKingdomDecision(List<string> args)
    {
        if (!TryGetKingdomDecisionByIndex(args, out Kingdom kingdom, out KingdomDecision decision, out int decisionIndex, out string message))
        {
            return message;
        }

        if (args.Count < 4 || args.Count > 5)
        {
            return "Usage: coop.debug.kingdom.vote_decision <kingdomId> <decisionIndex> <outcomeIndex|abstain> <supportWeight> [isFinal]";
        }

        bool isAbstain = args[2].Equals("abstain", StringComparison.OrdinalIgnoreCase);
        int outcomeIndex = -1;
        if (!isAbstain)
        {
            if (!int.TryParse(args[2], out int parsedOutcomeIndex))
            {
                return $"Outcome index is not a number: {args[2]}";
            }
            outcomeIndex = parsedOutcomeIndex - 1;
        }

        if (!TryParseSupportWeight(args[3], out Supporter.SupportWeights supportWeight))
        {
            return $"Support weight is invalid: {args[3]}. Use Choose, StayNeutral, SlightlyFavor, StronglyFavor, or FullyPush.";
        }

        bool finalVote = false;
        if (args.Count == 5 && !bool.TryParse(args[4], out finalVote))
        {
            return $"Unable to parse {args[4]} as a boolean.";
        }

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }
        if (!objectManager.TryGetIdWithLogging(kingdom, out string kingdomId))
        {
            return "Unable to resolve kingdom id.";
        }

        MessageBroker.Instance.Publish(decision, new KingdomDecisionVoteRequested(
            new KingdomDecisionVoteData(kingdomId, decisionIndex, outcomeIndex, (int)supportWeight, isAbstain, finalVote)));

        return $"Requested vote for {decision.GetType().Name}: outcome={args[2]}, support={supportWeight}, final={finalVote}.";
    }

    // coop.debug.kingdom.resolve_decision
    /// <summary>
    /// Resolves a queued player kingdom decision after every vote or the shared deadline.
    /// </summary>
    /// <param name="args">kingdomId, 1-based decision index</param>
    /// <returns>result message</returns>
    [CommandLineArgumentFunction("resolve_decision", "coop.debug.kingdom")]
    public static string ResolveKingdomDecision(List<string> args)
    {
        if (!TryGetKingdomDecisionByIndex(args, out Kingdom _, out KingdomDecision decision, out int _, out string message))
        {
            return message;
        }

        if (TryGetKingdomDecisionVoteManager(out var voteManager) == false)
        {
            return "Unable to resolve KingdomDecisionVoteManager";
        }

        return voteManager.TryResolveDecision(decision)
            ? $"Resolved {decision.GetType().Name} through player vote manager."
            : $"Could not resolve {decision.GetType().Name} through player vote manager.";
    }

    // coop.debug.kingdom.list_policies
    /// <summary>
    /// Lists the active policies of a specific kingdom. Useful for verifying that a policy change
    /// resolved on the server has replicated to clients.
    /// </summary>
    /// <param name="args">first arg : kingdomId</param>
    /// <returns>strings of all the active policies of a specific kingdom</returns>
    [CommandLineArgumentFunction("list_policies", "coop.debug.kingdom")]
    public static string ListKingdomPolicies(List<string> args)
    {
        if (args.Count < 1)
        {
            return "Usage: coop.debug.kingdom.list_policies <kingdomId>";
        }

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (objectManager.TryGetObject(args[0], out Kingdom kingdom) == false)
        {
            return $"ID: '{args[0]}' not found";
        }

        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.Append($"Active policies of Kingdom: {kingdom.Name}\n");

        int i = 1;
        foreach (PolicyObject policy in kingdom.ActivePolicies)
        {
            stringBuilder.Append($"{i}. {policy.Name} ({policy.StringId})\n");
            i++;
        }

        if (kingdom.ActivePolicies.Count == 0)
        {
            stringBuilder.Append("(none)\n");
        }

        return stringBuilder.ToString();
    }

    // coop.debug.kingdom.collection_list clans kingdom_V1
    /// <summary>
    /// Lists one of the synced Kingdom collection caches for server/client verification.
    /// </summary>
    /// <param name="args">collection name and kingdom id</param>
    /// <returns>IDs currently present in the selected collection</returns>
    [CommandLineArgumentFunction("collection_list", "coop.debug.kingdom")]
    public static string ListKingdomCollection(List<string> args)
    {
        if (args.Count != 2)
        {
            return "Usage: coop.debug.kingdom.collection_list <collection> <kingdomId>";
        }

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (objectManager.TryGetObject(args[1], out Kingdom kingdom) == false)
        {
            return $"Kingdom with ID: '{args[1]}' not found";
        }

        var collectionName = NormalizeCollectionName(args[0]);
        if (collectionName == "activepolicies")
        {
            return FormatCollection(objectManager, args[0], kingdom.ActivePolicies.Cast<object>());
        }

        if (collectionName == "unresolveddecisions")
        {
            return ListKingdomDecisions(new List<string> { args[1] });
        }

        if (!TryParseCollectionTarget(args[0], out var collectionType, out var parseMessage))
        {
            return parseMessage;
        }

        return FormatCollection(objectManager, args[0], GetCollectionValues(kingdom, collectionType));
    }

    // coop.debug.kingdom.collection_add clans kingdom_V1 clan_1
    /// <summary>
    /// Adds an item to a synced Kingdom collection on the server and broadcasts the change to clients.
    /// </summary>
    /// <param name="args">collection name, kingdom id, and value id</param>
    /// <returns>Result of the collection add</returns>
    [CommandLineArgumentFunction("collection_add", "coop.debug.kingdom")]
    public static string AddKingdomCollectionItem(List<string> args)
    {
        return ChangeKingdomCollection(args, CollectionOperation.Add);
    }

    // coop.debug.kingdom.collection_remove clans kingdom_V1 clan_1
    /// <summary>
    /// Removes an item from a synced Kingdom collection on the server and broadcasts the change to clients.
    /// </summary>
    /// <param name="args">collection name, kingdom id, and value id</param>
    /// <returns>Result of the collection remove</returns>
    [CommandLineArgumentFunction("collection_remove", "coop.debug.kingdom")]
    public static string RemoveKingdomCollectionItem(List<string> args)
    {
        return ChangeKingdomCollection(args, CollectionOperation.Remove);
    }

    // coop.debug.kingdom.declare_war
    /// <summary>
    /// Directly declares war between two factions (run on the server). Deterministic alternative
    /// to a DeclareWarDecision, which the kingdom AI may vote against.
    /// </summary>
    /// <param name="args">first arg : faction1Id ; second arg : faction2Id</param>
    /// <returns>result message</returns>
    [CommandLineArgumentFunction("declare_war", "coop.debug.kingdom")]
    public static string DeclareWar(List<string> args)
    {
        if (args.Count < 2)
        {
            return "Usage: coop.debug.kingdom.declare_war <faction1Id> <faction2Id> (run on the server)";
        }

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (TryGetFaction(objectManager, args[0], out IFaction faction1) == false)
        {
            return $"Faction not found with id: {args[0]}";
        }

        if (TryGetFaction(objectManager, args[1], out IFaction faction2) == false)
        {
            return $"Faction not found with id: {args[1]}";
        }

        DeclareWarAction.ApplyByDefault(faction1, faction2);
        return $"Declared war between '{faction1.Name}' and '{faction2.Name}'.";
    }

    // coop.debug.kingdom.make_peace
    /// <summary>
    /// Directly makes peace between two factions (run on the server).
    /// </summary>
    /// <param name="args">first arg : faction1Id ; second arg : faction2Id</param>
    /// <returns>result message</returns>
    [CommandLineArgumentFunction("make_peace", "coop.debug.kingdom")]
    public static string MakePeace(List<string> args)
    {
        if (args.Count < 2)
        {
            return "Usage: coop.debug.kingdom.make_peace <faction1Id> <faction2Id> (run on the server)";
        }

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (TryGetFaction(objectManager, args[0], out IFaction faction1) == false)
        {
            return $"Faction not found with id: {args[0]}";
        }

        if (TryGetFaction(objectManager, args[1], out IFaction faction2) == false)
        {
            return $"Faction not found with id: {args[1]}";
        }

        MakePeaceAction.Apply(faction1, faction2);
        return $"Made peace between '{faction1.Name}' and '{faction2.Name}'.";
    }

    private static string ChangeKingdomCollection(List<string> args, CollectionOperation operation)
    {
        if (ModInformation.IsClient)
        {
            return "Command is only available to run on the server";
        }

        if (args.Count < 3)
        {
            return operation == CollectionOperation.Add ? CollectionAddUsage : CollectionRemoveUsage;
        }

        var collectionName = NormalizeCollectionName(args[0]);
        if (collectionName == "activepolicies")
        {
            return ChangeActivePolicy(args, operation);
        }

        if (collectionName == "unresolveddecisions")
        {
            var forwardedArgs = args.Skip(1).ToList();
            return operation == CollectionOperation.Add
                ? AddDecision(forwardedArgs)
                : RemoveDecision(forwardedArgs);
        }

        if (!TryParseCollectionTarget(args[0], out var collectionType, out var parseMessage))
        {
            return parseMessage;
        }

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (objectManager.TryGetObject(args[1], out Kingdom kingdom) == false)
        {
            return $"Kingdom with ID: '{args[1]}' not found";
        }

        if (!TryResolveCollectionValue(objectManager, collectionType, args[2], out var value, out var resolveMessage))
        {
            return resolveMessage;
        }

        ApplyCollectionChange(kingdom, collectionType, operation, value);

        return $"{operation} {args[2]} in {args[0]} for kingdom {args[1]}.";
    }

    private static string ChangeActivePolicy(List<string> args, CollectionOperation operation)
    {
        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (objectManager.TryGetObject(args[1], out Kingdom kingdom) == false)
        {
            return $"Kingdom with ID: '{args[1]}' not found";
        }

        if (objectManager.TryGetObject(args[2], out PolicyObject policy) == false)
        {
            return $"PolicyObject with ID: '{args[2]}' not found";
        }

        if (operation == CollectionOperation.Add)
        {
            kingdom.AddPolicy(policy);
            return $"Added policy {args[2]} to kingdom {args[1]}.";
        }

        kingdom.RemovePolicy(policy);
        return $"Removed policy {args[2]} from kingdom {args[1]}.";
    }

    /// <summary>
    /// Resolves a faction id to either a Kingdom or a Clan.
    /// </summary>
    private static bool TryGetFaction(IObjectManager objectManager, string id, out IFaction faction)
    {
        if (objectManager.TryGetObject(id, out Kingdom kingdom))
        {
            faction = kingdom;
            return true;
        }
        if (objectManager.TryGetObject(id, out Clan clan))
        {
            faction = clan;
            return true;
        }
        faction = null;
        return false;
    }

    private static bool TryParseCollectionTarget(
        string value,
        out CollectionTarget collectionType,
        out string message)
    {
        collectionType = default;
        message = string.Empty;

        switch (NormalizeCollectionName(value))
        {
            case "armies":
                collectionType = CollectionTarget.Armies;
                return true;
            case "clans":
                collectionType = CollectionTarget.Clans;
                return true;
            case "fiefscache":
                collectionType = CollectionTarget.FiefsCache;
                return true;
            case "heroescache":
                collectionType = CollectionTarget.HeroesCache;
                return true;
            case "lordscache":
            case "alivelordscache":
                collectionType = CollectionTarget.AliveLordsCache;
                return true;
            case "deadlordscache":
                collectionType = CollectionTarget.DeadLordsCache;
                return true;
            case "settlementscache":
                collectionType = CollectionTarget.SettlementsCache;
                return true;
            case "townscache":
                collectionType = CollectionTarget.TownsCache;
                return true;
            case "villagescache":
                collectionType = CollectionTarget.VillagesCache;
                return true;
            case "warpartycomponentscache":
                collectionType = CollectionTarget.WarPartyComponentsCache;
                return true;
            default:
                message = "Unknown collection. Valid values: activePolicies, armies, clans, fiefsCache, heroesCache, lordsCache, aliveLordsCache, deadLordsCache, settlementsCache, townsCache, unresolvedDecisions, villagesCache, warPartyComponentsCache.";
                return false;
        }
    }

    private static string NormalizeCollectionName(string value)
    {
        return value.Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static IEnumerable<object> GetCollectionValues(Kingdom kingdom, CollectionTarget collectionType)
    {
        return collectionType switch
        {
            CollectionTarget.Armies => kingdom._armies?.Cast<object>() ?? Enumerable.Empty<object>(),
            CollectionTarget.Clans => kingdom._clans?.Cast<object>() ?? Enumerable.Empty<object>(),
            CollectionTarget.FiefsCache => kingdom._fiefsCache?.Cast<object>() ?? Enumerable.Empty<object>(),
            CollectionTarget.HeroesCache => kingdom._heroesCache?.Cast<object>() ?? Enumerable.Empty<object>(),
            CollectionTarget.AliveLordsCache => kingdom._aliveLordsCache?.Cast<object>() ?? Enumerable.Empty<object>(),
            CollectionTarget.DeadLordsCache => kingdom._deadLordsCache?.Cast<object>() ?? Enumerable.Empty<object>(),
            CollectionTarget.SettlementsCache => kingdom._settlementsCache?.Cast<object>() ?? Enumerable.Empty<object>(),
            CollectionTarget.TownsCache => kingdom._townsCache?.Cast<object>() ?? Enumerable.Empty<object>(),
            CollectionTarget.VillagesCache => kingdom._villagesCache?.Cast<object>() ?? Enumerable.Empty<object>(),
            CollectionTarget.WarPartyComponentsCache => kingdom._warPartyComponentsCache?.Cast<object>() ?? Enumerable.Empty<object>(),
            _ => Enumerable.Empty<object>(),
        };
    }

    private static void ApplyCollectionChange(
        Kingdom kingdom,
        CollectionTarget collectionType,
        CollectionOperation operation,
        object value)
    {
        switch (collectionType)
        {
            case CollectionTarget.Armies:
                if (operation == CollectionOperation.Add)
                {
                    KingdomCollectionSync.AddArmy(kingdom, (Army)value, publish: true);
                }
                else
                {
                    KingdomCollectionSync.RemoveArmy(kingdom, (Army)value, publish: true);
                }
                break;
            case CollectionTarget.Clans:
                if (operation == CollectionOperation.Add)
                {
                    KingdomCollectionSync.AddClan(kingdom, (Clan)value, publish: true);
                }
                else
                {
                    KingdomCollectionSync.RemoveClan(kingdom, (Clan)value, publish: true);
                }
                break;
            case CollectionTarget.FiefsCache:
                if (operation == CollectionOperation.Add)
                {
                    KingdomCollectionSync.AddFief(kingdom, (Town)value, publish: true);
                }
                else
                {
                    KingdomCollectionSync.RemoveFief(kingdom, (Town)value, publish: true);
                }
                break;
            case CollectionTarget.HeroesCache:
                if (operation == CollectionOperation.Add)
                {
                    KingdomCollectionSync.AddHero(kingdom, (Hero)value, publish: true);
                }
                else
                {
                    KingdomCollectionSync.RemoveHero(kingdom, (Hero)value, publish: true);
                }
                break;
            case CollectionTarget.AliveLordsCache:
                if (operation == CollectionOperation.Add)
                {
                    KingdomCollectionSync.AddAliveLord(kingdom, (Hero)value, publish: true);
                }
                else
                {
                    KingdomCollectionSync.RemoveAliveLord(kingdom, (Hero)value, publish: true);
                }
                break;
            case CollectionTarget.DeadLordsCache:
                if (operation == CollectionOperation.Add)
                {
                    KingdomCollectionSync.AddDeadLord(kingdom, (Hero)value, publish: true);
                }
                else
                {
                    KingdomCollectionSync.RemoveDeadLord(kingdom, (Hero)value, publish: true);
                }
                break;
            case CollectionTarget.SettlementsCache:
                if (operation == CollectionOperation.Add)
                {
                    KingdomCollectionSync.AddSettlement(kingdom, (Settlement)value, publish: true);
                }
                else
                {
                    KingdomCollectionSync.RemoveSettlement(kingdom, (Settlement)value, publish: true);
                }
                break;
            case CollectionTarget.TownsCache:
                if (operation == CollectionOperation.Add)
                {
                    KingdomCollectionSync.AddTown(kingdom, (Town)value, publish: true);
                }
                else
                {
                    KingdomCollectionSync.RemoveTown(kingdom, (Town)value, publish: true);
                }
                break;
            case CollectionTarget.VillagesCache:
                if (operation == CollectionOperation.Add)
                {
                    KingdomCollectionSync.AddVillage(kingdom, (Village)value, publish: true);
                }
                else
                {
                    KingdomCollectionSync.RemoveVillage(kingdom, (Village)value, publish: true);
                }
                break;
            case CollectionTarget.WarPartyComponentsCache:
                if (operation == CollectionOperation.Add)
                {
                    KingdomCollectionSync.AddWarPartyComponent(kingdom, (WarPartyComponent)value, publish: true);
                }
                else
                {
                    KingdomCollectionSync.RemoveWarPartyComponent(kingdom, (WarPartyComponent)value, publish: true);
                }
                break;
            default:
                Logger.Error("Unable to apply collection change because {CollectionTarget} does not have a matching handler.", collectionType);
                break;
        }
    }

    private static string FormatCollection(
        IObjectManager objectManager,
        string collectionName,
        IEnumerable<object> values)
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine($"{collectionName}:");

        var count = 0;
        foreach (var value in values)
        {
            count++;
            if (objectManager.TryGetId(value, out var id))
            {
                stringBuilder.AppendLine($"{count}. {id}");
            }
            else
            {
                stringBuilder.AppendLine($"{count}. {value?.GetType().Name ?? "<null>"}");
            }
        }

        if (count == 0)
        {
            stringBuilder.AppendLine("(none)");
        }

        return stringBuilder.ToString();
    }

    private static bool TryResolveCollectionValue(
        IObjectManager objectManager,
        CollectionTarget collectionType,
        string valueId,
        out object value,
        out string message)
    {
        value = null;
        message = string.Empty;

        switch (collectionType)
        {
            case CollectionTarget.Armies:
                return TryResolve<Army>(objectManager, valueId, out value, out message);
            case CollectionTarget.Clans:
                return TryResolve<Clan>(objectManager, valueId, out value, out message);
            case CollectionTarget.FiefsCache:
                return TryResolve<Town>(objectManager, valueId, out value, out message);
            case CollectionTarget.HeroesCache:
            case CollectionTarget.AliveLordsCache:
            case CollectionTarget.DeadLordsCache:
                return TryResolve<Hero>(objectManager, valueId, out value, out message);
            case CollectionTarget.SettlementsCache:
                return TryResolve<Settlement>(objectManager, valueId, out value, out message);
            case CollectionTarget.TownsCache:
                return TryResolve<Town>(objectManager, valueId, out value, out message);
            case CollectionTarget.VillagesCache:
                return TryResolve<Village>(objectManager, valueId, out value, out message);
            case CollectionTarget.WarPartyComponentsCache:
                return TryResolve<WarPartyComponent>(objectManager, valueId, out value, out message);
            default:
                message = $"Unsupported collection {collectionType}.";
                return false;
        }
    }

    private static bool TryResolve<T>(
        IObjectManager objectManager,
        string valueId,
        out object value,
        out string message)
    {
        value = null;

        if (objectManager.TryGetObject(valueId, out T resolved) == false)
        {
            message = $"{typeof(T).Name} with ID: '{valueId}' not found";
            return false;
        }

        value = resolved;
        message = string.Empty;
        return true;
    }

    private static bool TryGetKingdomDecisionByIndex(List<string> args, out Kingdom kingdom, out KingdomDecision decision, out int zeroBasedIndex, out string message)
    {
        kingdom = null;
        decision = null;
        zeroBasedIndex = -1;

        if (args.Count < 2)
        {
            message = "Usage: <kingdomId> <decisionIndex>";
            return false;
        }

        if (TryGetObjectManager(out var objectManager) == false)
        {
            message = "Unable to resolve ObjectManager";
            return false;
        }

        bool isFullClanId = args[0].StartsWith("Clan_", StringComparison.Ordinal);
        bool kingdomResolved = isFullClanId
            ? StartAllianceDecisionData.TryGetKingdomReference(objectManager, args[0], out kingdom)
            : objectManager.TryGetObject(args[0], out kingdom);
        if (!kingdomResolved)
        {
            message = $"Kingdom with ID: '{args[0]}' not found";
            return false;
        }

        if (!int.TryParse(args[1], out int index))
        {
            message = $"Decision index is not a number: {args[1]}";
            return false;
        }

        zeroBasedIndex = index - 1;
        if (zeroBasedIndex < 0 || zeroBasedIndex >= kingdom._unresolvedDecisions.Count)
        {
            message = "Decision index is out of bounds.";
            return false;
        }

        decision = kingdom._unresolvedDecisions[zeroBasedIndex];
        message = string.Empty;
        return true;
    }

    private static bool TryParseSupportWeight(string value, out Supporter.SupportWeights supportWeight)
    {
        if (Enum.TryParse(value, true, out supportWeight)) return true;
        if (!int.TryParse(value, out int supportWeightValue)) return false;

        supportWeight = (Supporter.SupportWeights)supportWeightValue;
        return Enum.IsDefined(typeof(Supporter.SupportWeights), supportWeight);
    }

    // coop.debug..kingdom.add_decision
    /// <summary>
    /// Adds a decision to a Kingdom.
    /// </summary>
    /// <param name="args">first arg : kingdomId ; second arg : decision to add</param>
    /// <returns></returns>
    [CommandLineArgumentFunction("add_decision", "coop.debug.kingdom")]
    public static string AddDecision(List<string> args)
    {
        if (args.Count < 4)
        {
            return AddBasicUsage;
        }

        string kingdomId = args[0];
        string clanId = args[1];
        string ignoreInfluence = args[2];
        string decisionType = args[3];

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }
        if (objectManager.TryGetObject(kingdomId, out Kingdom kingdom) == false)
        {
            return $"Kingdom with ID: '{kingdomId}' not found";
        }

        if (objectManager.TryGetObject(clanId, out Clan proposerClan) == false)
        {
            return $"Clan with ID: '{clanId}' not found";
        }

        if (!bool.TryParse(ignoreInfluence, out bool ignoreInfluenceCost))
        {
            return $"Couldnt convert ignoreInfluenceCost: {ignoreInfluence}";
        }

        if (!TryGetKingdomDecisionFunc.ContainsKey(decisionType))
        {
            return $"Kingdom decision type: {decisionType} does not exist.";
        }

        if (!TryGetKingdomDecisionFunc[decisionType](objectManager, args, proposerClan, out KingdomDecision kingdomDecision, out string message))
        {
            return message;
        }

        kingdom.AddDecision(kingdomDecision, ignoreInfluenceCost);
        return $"Kingdom decision added successfully.";
    }

    // coop.debug.kingdom.remove_decision
    /// <summary>
    /// Removes a decision from a Kingdom
    /// </summary>
    /// <param name="args">first arg : kingdomId ; second arg : index of decision to remove</param>
    /// <returns></returns>
    [CommandLineArgumentFunction("remove_decision", "coop.debug.kingdom")]
    public static string RemoveDecision(List<string> args)
    {
        if (args.Count != 2)
        {
            return RemoveUsage;
        }

        string kingdomId = args[0];
        string index = args[1];

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }
        if (objectManager.TryGetObject(kingdomId, out Kingdom kingdom) == false)
        {
            return $"Kingdom with ID: '{kingdomId}' not found";
        }

        if (!int.TryParse(index, out int idx))
        {
            return $"Argument2: {index} is not a number.";
        }

        var decisions = kingdom._unresolvedDecisions;
        if (idx > 0 && idx <= decisions.Count)
        {
            kingdom.RemoveDecision(decisions[idx - 1]);
        }
        else
        {
            return "Index is out of bounds.";
        }

        return $"Kingdom decision removed.";
    }

    /// <summary>
    /// Tries getting declare war decision from given arguments.
    /// </summary>
    /// <param name="objectManager">object manager.</param>
    /// <param name="args">argument list.</param>
    /// <param name="proposerClan">proposer clan of the kingdom decision.</param>
    /// <param name="kingdomDecision">kingdom decision result.</param>
    /// <param name="message">message result.</param>
    /// <returns>True if kingdomdecision is successfully returned, else false.</returns>
    private static bool TryGetDeclareWarDecision(IObjectManager objectManager, List<string> args, Clan proposerClan, out KingdomDecision kingdomDecision, out string message)
    {
        if (args.Count < 5)
        {
            kingdomDecision = null;
            message = AddDeclareWarDecisionUsage;
            return false;
        }

        string factionId = args[4];
        if (!objectManager.TryGetObject(factionId, out Kingdom kingdom) & !objectManager.TryGetObject(factionId, out Clan clanFaction))
        {
            kingdomDecision = null;
            message = $"Argument5: Faction is not found with id: {factionId}.";
            return false;
        }

        IFaction faction;
        if (kingdom != null)
        {
            faction = kingdom;
        }
        else
        {
            faction = clanFaction;
        }

        kingdomDecision = new DeclareWarDecision(proposerClan, faction);
        message = string.Empty;
        return true;
    }

    /// <summary>
    /// Tries getting expel clan from kingdom decision from given arguments.
    /// </summary>
    /// <param name="objectManager">object manager.</param>
    /// <param name="args">argument list.</param>
    /// <param name="proposerClan">proposer clan of the kingdom decision.</param>
    /// <param name="kingdomDecision">kingdom decision result.</param>
    /// <param name="message">message result.</param>
    /// <returns>True if kingdomdecision is successfully returned, else false.</returns>
    private static bool TryGetExpelClanFromKingdomDecision(IObjectManager objectManager, List<string> args, Clan proposerClan, out KingdomDecision kingdomDecision, out string message)
    {
        if (args.Count < 5)
        {
            kingdomDecision = null;
            message = AddExpelClanFromKingdomDecisionUsage;
            return false;
        }

        string clanId = args[4];
        if (!objectManager.TryGetObject(clanId, out Clan clan))
        {
            kingdomDecision = null;
            message = $"Argument5: Clan not found with id: {clanId}";
            return false;
        }
        kingdomDecision = new ExpelClanFromKingdomDecision(proposerClan, clan);
        message = string.Empty;
        return true;
    }

    /// <summary>
    /// Tries getting king selection kingdom decision from given arguments.
    /// </summary>
    /// <param name="objectManager">object manager.</param>
    /// <param name="args">argument list.</param>
    /// <param name="proposerClan">proposer clan of the kingdom decision.</param>
    /// <param name="kingdomDecision">kingdom decision result.</param>
    /// <param name="message">message result.</param>
    /// <returns>True if kingdomdecision is successfully returned, else false.</returns>
    private static bool TryGetKingSelectionKingdomDecision(IObjectManager objectManager, List<string> args, Clan proposerClan, out KingdomDecision kingdomDecision, out string message)
    {
        if (args.Count < 5)
        {
            kingdomDecision = null;
            message = AddKingSelectionKingdomDecisionUsage;
            return false;
        }

        string clanId = args[4];
        if (!objectManager.TryGetObject(clanId, out Clan clan))
        {
            kingdomDecision = null;
            message = $"Argument5: Clan not found with id: {clanId}";
            return false;
        }
        kingdomDecision = new KingSelectionKingdomDecision(proposerClan, clan);
        message = string.Empty;
        return true;
    }

    /// <summary>
    /// Tries getting kingdom policy decision from given arguments.
    /// </summary>
    /// <param name="objectManager">object manager.</param>
    /// <param name="args">argument list.</param>
    /// <param name="proposerClan">proposer clan of the kingdom decision.</param>
    /// <param name="kingdomDecision">kingdom decision result.</param>
    /// <param name="message">message result.</param>
    /// <returns>True if kingdomdecision is successfully returned, else false.</returns>
    private static bool TryGetKingdomPolicyDecision(IObjectManager objectManager, List<string> args, Clan proposerClan, out KingdomDecision kingdomDecision, out string message)
    {
        if (args.Count < 6)
        {
            kingdomDecision = null;
            message = AddKingdomPolicyDecisionUsage;
            return false;
        }

        string policyId = args[4];
        string isInvertedDecision = args[5];

        if (!objectManager.TryGetObject(policyId, out PolicyObject policy))
        {
            kingdomDecision = null;
            message = $"Argument5: PolicyObject not found by id: {policyId}";
            return false;
        }

        if (!bool.TryParse(isInvertedDecision, out bool isInverted))
        {
            kingdomDecision = null;
            message = $"Argument6: The given value is not a boolean value: {isInvertedDecision}";
            return false;
        }

        kingdomDecision = new KingdomPolicyDecision(proposerClan, policy, isInverted);
        message = string.Empty;
        return true;
    }

    /// <summary>
    /// Tries getting settlement claimant decision from given arguments.
    /// </summary>
    /// <param name="objectManager">object manager.</param>
    /// <param name="args">argument list.</param>
    /// <param name="proposerClan">proposer clan of the kingdom decision.</param>
    /// <param name="kingdomDecision">kingdom decision result.</param>
    /// <param name="message">message result.</param>
    /// <returns>True if kingdomdecision is successfully returned, else false.</returns>
    private static bool TryGetSettlementClaimantDecision(IObjectManager objectManager, List<string> args, Clan proposerClan, out KingdomDecision kingdomDecision, out string message)
    {
        if (args.Count < 7)
        {
            kingdomDecision = null;
            message = AddSettlementClaimantDecisionUsage;
            return false;
        }

        string settlementId = args[4];
        string capturerHeroId = args[5];
        string clanToExcludeId = args[6];

        if (!objectManager.TryGetObject(settlementId, out Settlement settlement))
        {
            kingdomDecision = null;
            message = $"Argument5: Settlement not found by id: {settlementId}";
            return false;
        }

        if (!objectManager.TryGetObject(capturerHeroId, out Hero capturerHero))
        {
            kingdomDecision = null;
            message = $"Argument6: Hero not found by id: {capturerHeroId}";
            return false;
        }
        if (!objectManager.TryGetObject(clanToExcludeId, out Clan clanToExclude))
        {
            kingdomDecision = null;
            message = $"Argument7: Clan not found by id: {clanToExcludeId}";
            return false;
        }

        kingdomDecision = new SettlementClaimantDecision(proposerClan, settlement, capturerHero, clanToExclude);
        message = string.Empty;
        return true;
    }

    /// <summary>
    /// Tries getting settlement claimant preliminary war decision from given arguments.
    /// </summary>
    /// <param name="objectManager">object manager.</param>
    /// <param name="args">argument list.</param>
    /// <param name="proposerClan">proposer clan of the kingdom decision.</param>
    /// <param name="kingdomDecision">kingdom decision result.</param>
    /// <param name="message">message result.</param>
    /// <returns>True if kingdomdecision is successfully returned, else false.</returns>
    private static bool TryGetSettlementClaimantPreliminaryDecision(IObjectManager objectManager, List<string> args, Clan proposerClan, out KingdomDecision kingdomDecision, out string message)
    {
        if (args.Count < 5)
        {
            kingdomDecision = null;
            message = AddSettlementClaimantPreliminaryDecisionUsage;
            return false;
        }
        string settlementId = args[4];

        if (!objectManager.TryGetObject(settlementId, out Settlement settlement))
        {
            kingdomDecision = null;
            message = $"Argument5: Settlement not found by id: {settlementId}";
            return false;
        }

        kingdomDecision = new SettlementClaimantPreliminaryDecision(proposerClan, settlement);
        message = string.Empty;
        return true;
    }

    private static bool TryGetAcceptCallToWarAgreementDecision(IObjectManager objectManager, List<string> args, Clan proposerClan, out KingdomDecision kingdomDecision, out string message)
    {
        if (args.Count < 6)
        {
            kingdomDecision = null;
            message = AddAcceptCallToWarAgreementDecisionUsage;
            return false;
        }

        if (!objectManager.TryGetObject(args[4], out Kingdom callingKingdom))
        {
            kingdomDecision = null;
            message = $"Argument5: Calling kingdom not found by id: {args[4]}";
            return false;
        }

        if (!objectManager.TryGetObject(args[5], out Kingdom kingdomToCallToWarAgainst))
        {
            kingdomDecision = null;
            message = $"Argument6: War target kingdom not found by id: {args[5]}";
            return false;
        }

        kingdomDecision = new AcceptCallToWarAgreementDecision(proposerClan, callingKingdom, kingdomToCallToWarAgainst);
        message = string.Empty;
        return true;
    }

    private static bool TryGetProposeCallToWarAgreementDecision(IObjectManager objectManager, List<string> args, Clan proposerClan, out KingdomDecision kingdomDecision, out string message)
    {
        if (args.Count < 6)
        {
            kingdomDecision = null;
            message = AddProposeCallToWarAgreementDecisionUsage;
            return false;
        }

        if (!objectManager.TryGetObject(args[4], out Kingdom calledKingdom))
        {
            kingdomDecision = null;
            message = $"Argument5: Called kingdom not found by id: {args[4]}";
            return false;
        }

        if (!objectManager.TryGetObject(args[5], out Kingdom kingdomToCallToWarAgainst))
        {
            kingdomDecision = null;
            message = $"Argument6: War target kingdom not found by id: {args[5]}";
            return false;
        }

        kingdomDecision = new ProposeCallToWarAgreementDecision(proposerClan, calledKingdom, kingdomToCallToWarAgainst);
        message = string.Empty;
        return true;
    }

    private static bool TryGetStartAllianceDecision(IObjectManager objectManager, List<string> args, Clan proposerClan, out KingdomDecision kingdomDecision, out string message)
    {
        if (args.Count < 5)
        {
            kingdomDecision = null;
            message = AddStartAllianceDecisionUsage;
            return false;
        }

        if (!objectManager.TryGetObject(args[4], out Kingdom kingdomToStartAllianceWith))
        {
            kingdomDecision = null;
            message = $"Argument5: Alliance target kingdom not found by id: {args[4]}";
            return false;
        }

        kingdomDecision = new StartAllianceDecision(proposerClan, kingdomToStartAllianceWith);
        message = string.Empty;
        return true;
    }

    private static bool TryGetTradeAgreementDecision(IObjectManager objectManager, List<string> args, Clan proposerClan, out KingdomDecision kingdomDecision, out string message)
    {
        if (args.Count < 5)
        {
            kingdomDecision = null;
            message = AddTradeAgreementDecisionUsage;
            return false;
        }

        if (!objectManager.TryGetObject(args[4], out Kingdom targetKingdom))
        {
            kingdomDecision = null;
            message = $"Argument5: Trade target kingdom not found by id: {args[4]}";
            return false;
        }

        kingdomDecision = new TradeAgreementDecision(proposerClan, targetKingdom);
        message = string.Empty;
        return true;
    }

    /// <summary>
    /// Tries getting make peace decision from given arguments.
    /// </summary>
    /// <param name="objectManager">object manager.</param>
    /// <param name="args">argument list.</param>
    /// <param name="proposerClan">proposer clan of the kingdom decision.</param>
    /// <param name="kingdomDecision">kingdom decision result.</param>
    /// <param name="message">message result.</param>
    /// <returns>True if kingdomdecision is successfully returned, else false.</returns>

    //private static bool TryGetMakePeaceKingdomDecision(IObjectManager objectManager, List<string> args, Clan proposerClan, out KingdomDecision kingdomDecision, out string message)
    //{
    //    if (args.Count < 7)
    //    {
    //        kingdomDecision = null;
    //        message = AddMakePeaceKingdomDecisionUsage;
    //        return false;
    //    }

    //    string factionId = args[4];
    //    string dailyTribute = args[5];
    //    string applyResults = args[6];

    //    if (!objectManager.TryGetObject(factionId, out Kingdom kingdom) & !objectManager.TryGetObject(factionId, out Clan clan))
    //    {
    //        kingdomDecision = null;
    //        message = $"Argument5: Faction is not found by Id: {factionId}";
    //        return false;
    //    }

    //    IFaction faction;
    //    if (kingdom != null)
    //    {
    //        faction = kingdom;
    //    }
    //    else
    //    {
    //        faction = clan;
    //    }

    //    if (!int.TryParse(dailyTribute, out int dailyTributeToBePaid))
    //    {
    //        kingdomDecision = null;
    //        message = $"Argument6: The given value is not an integer value: {dailyTribute}";
    //        return false;
    //    }

    //    if (!bool.TryParse(applyResults, out bool applyResult))
    //    {
    //        kingdomDecision = null;
    //        message = $"Argument7: The given value is not a boolean value: {applyResults}";
    //        return false;
    //    }

    //    kingdomDecision = new MakePeaceKingdomDecision(proposerClan, faction, dailyTributeToBePaid, applyResult);
    //    message = string.Empty;
    //    return true;
    //}
}
