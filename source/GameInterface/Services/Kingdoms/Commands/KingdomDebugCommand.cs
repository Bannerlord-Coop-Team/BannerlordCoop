using Common.Commands;
using Autofac;
using Common;
using Common.Extensions;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.Kingdoms.Handlers;
using GameInterface.Services.Kingdoms.Data;
using GameInterface.Services.Kingdoms.Messages;
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
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions.ItemTypes;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Kingdoms.Commands;

/// <summary>
/// Commands for <see cref="Kingdom"/>
/// </summary>
public class KingdomDebugCommand
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    private static readonly ILogger Logger = LogManager.GetLogger<KingdomDebugCommand>();
    private static PolicyTimeoutFixture pendingPolicyTimeoutFixture;
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

    private static readonly string RemoveUsage = "Usage: coop.debug.kingdom.remove_decision <kingdomId> <Index>";
    private static readonly string AddBasicUsage = "Usage: coop.debug.kingdom.add_decision <kingdomId> <proposerClanId> <ignoreInfluenceCost> <decisionType> [decisionArg1] [decisionArg2] [decisionArg3]";
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
    private delegate bool KingdomDecisionDelegate(IObjectManager objectManager, IReadOnlyList<string> args, Clan proposerClan, out KingdomDecision kingdomDecision, out string message);
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

    public sealed class KingdomOpenCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.kingdom";

        public string Name => "open";

        public string Description => "Opens the kingdom screen on a client.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModInformation.IsClient) return Failed("Command can only be run on a client.");
            if (Clan.PlayerClan?.Kingdom == null) return Failed("The player clan is not in a kingdom.");
            if (Game.Current?.GameStateManager == null) return Failed("The game-state manager is unavailable.");
            if (Game.Current.GameStateManager.ActiveState is KingdomState) return Succeeded("KINGDOM_SCREEN_ALREADY_OPEN");

            KingdomState kingdomState = Game.Current.GameStateManager.CreateState<KingdomState>(
                (IFaction)Clan.PlayerClan);
            Game.Current.GameStateManager.PushState(kingdomState, 0);
            return Succeeded("KINGDOM_SCREEN_OPENED");
        }
    }

    public sealed class KingdomOpenDecisionCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.kingdom";

        public string Name => "open_decision";

        public string Description => "Opens one queued kingdom decision.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("kingdom_id", "The registered kingdom id."),
            new ExpectedArgs("decision_index", "The one-based decision index."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModInformation.IsClient) return Failed("Command can only be run on a client.");

            KingdomDecision decision;
            Kingdom playerKingdom = Clan.PlayerClan?.Kingdom;
            bool isPlayerKingdom = playerKingdom != null &&
                (string.Equals(playerKingdom.StringId, args[0], StringComparison.Ordinal) ||
                 string.Equals($"{nameof(Kingdom)}_{playerKingdom.StringId}", args[0], StringComparison.Ordinal));
            if (isPlayerKingdom)
            {
                if (!int.TryParse(args[1], out int index)) return Failed($"Decision index is not a number: {args[1]}");

                int zeroBasedIndex = index - 1;
                if (zeroBasedIndex < 0 || zeroBasedIndex >= playerKingdom._unresolvedDecisions.Count)
                    return Failed("Decision index is out of bounds.");

                decision = playerKingdom._unresolvedDecisions[zeroBasedIndex];
            }
            else if (!TryGetKingdomDecisionByIndex(args, out Kingdom _, out decision, out int _, out string message))
            {
                return Failed(message);
            }
            if (Game.Current?.GameStateManager == null) return Failed("The game-state manager is unavailable.");
            if (Game.Current.GameStateManager.ActiveState is KingdomState) return Succeeded("KINGDOM_SCREEN_ALREADY_OPEN");

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
                return Failed("The decision confirmation did not open.");
            }

            InformationManager.HideInquiry();
            inquiry.AffirmativeAction();
            return Succeeded("KINGDOM_DECISION_SCREEN_OPENED");
        }
    }

    public sealed class KingdomCloseCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.kingdom";

        public string Name => "close";

        public string Description => "Closes the kingdom screen on a client.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModInformation.IsClient) return Failed("Command can only be run on a client.");
            if (!(Game.Current?.GameStateManager?.ActiveState is KingdomState))
                return Failed("No active Kingdom screen.");

            Game.Current.GameStateManager.PopState(0);
            return Succeeded("KINGDOM_SCREEN_CLOSED");
        }
    }

    public sealed class KingdomScreenStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.kingdom";

        public string Name => "screen_state";

        public string Description => "Reports kingdom screen state.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModInformation.IsClient) return Failed("Command can only be run on a client.");

            var kingdomScreen = ScreenManager.TopScreen as GauntletKingdomScreen;
            return Succeeded($"KINGDOM_SCREEN_STATE active={Game.Current?.GameStateManager?.ActiveState is KingdomState} " +
                $"topScreen={kingdomScreen != null} dataSource={kingdomScreen?.DataSource != null} " +
                $"decisionActive={kingdomScreen?.DataSource?.Decision?.IsActive ?? false} " +
                $"clanShown={kingdomScreen?.DataSource?.Clan?.Show ?? false} " +
                $"kingdom={kingdomScreen?.DataSource?.Kingdom?.Name} " +
                $"clans={kingdomScreen?.DataSource?.Clan?.Clans?.Count ?? -1}");
        }
    }

    public sealed class KingdomPolicyTimeoutCaptureCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.kingdom";

        public string Name => "policy_timeout_capture";

        public string Description => "Captures the kingdom policy-timeout fixture.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("Command can only be run on the server.");
            if (pendingPolicyTimeoutFixture != null) return Failed("A policy-timeout fixture lifecycle is already active.");
            if (!TryGetObjectManager(out var objectManager) || !TryGetPlayerManager(out var playerManager))
                return Failed("Unable to resolve policy-timeout fixture services.");
            if (!playerManager.TryGetPlayer("testclient", out var player))
                return Failed("No registered player has controller id 'testclient'.");
            if (!objectManager.TryGetObject(player.ClanId, out Clan proposerClan) || proposerClan.Kingdom == null)
                return Failed("The testclient clan is not in a kingdom.");

            Kingdom kingdom = proposerClan.Kingdom;
            if (kingdom.UnresolvedDecisions.Count > 0)
                return Failed($"Kingdom {kingdom.StringId} already has an unresolved decision.");

            PolicyObject policy = PolicyObject.All
                .Where(candidate => !kingdom.ActivePolicies.Contains(candidate))
                .OrderBy(candidate => candidate.StringId)
                .FirstOrDefault();
            if (policy == null) return Failed($"Kingdom {kingdom.StringId} has every policy active.");
            if (!objectManager.TryGetIdWithLogging(kingdom, out string kingdomId) ||
                !objectManager.TryGetIdWithLogging(proposerClan, out string proposerClanId) ||
                !objectManager.TryGetIdWithLogging(policy, out string policyId))
                return Failed("Unable to resolve policy-timeout fixture ids.");

            return Succeeded(PolicyTimeoutJsonResult(new
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
            }));
        }
    }

    public sealed class KingdomPolicyTimeoutStageCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.kingdom";

        public string Name => "policy_timeout_stage";

        public string Description => "Stages the kingdom policy-timeout fixture.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("kingdom_id", "The registered kingdom id."),
            new ExpectedArgs("proposer_clan_id", "The registered proposer clan id."),
            new ExpectedArgs("policy_id", "The registered policy id."),
            new ExpectedArgs("policy_was_active", "Whether the policy was active at capture."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("Command can only be run on the server.");
            if (!bool.TryParse(args[3], out bool policyWasActive))
                return Failed($"Unable to parse {args[3]} as a boolean.");
            if (pendingPolicyTimeoutFixture != null) return Failed("A policy-timeout fixture lifecycle is already active.");
            if (!TryGetObjectManager(out var objectManager)) return Failed("Unable to resolve ObjectManager");
            if (!objectManager.TryGetObject(args[0], out Kingdom kingdom)) return Failed($"Kingdom with ID: '{args[0]}' not found");
            if (!objectManager.TryGetObject(args[1], out Clan proposerClan)) return Failed($"Clan with ID: '{args[1]}' not found");
            if (!objectManager.TryGetObject(args[2], out PolicyObject policy)) return Failed($"PolicyObject with ID: '{args[2]}' not found");
            if (proposerClan.Kingdom != kingdom) return Failed($"Clan {args[1]} is not in kingdom {args[0]}.");
            if (kingdom.UnresolvedDecisions.Count > 0)
                return Failed($"Kingdom {args[0]} no longer has a clean decision fixture.");
            if (kingdom.ActivePolicies.Contains(policy) != policyWasActive)
                return Failed($"Policy {args[2]} changed after fixture capture.");

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
            if (decisionIndex <= 0) return Failed("The policy-timeout decision was not added to the kingdom.");

            return Succeeded(PolicyTimeoutJsonResult(new
            {
                success = true,
                fixture.KingdomId,
                fixture.PolicyId,
                decisionIndex,
                votingDurationSeconds = (int)KingdomDecisionVoteManager.VotingRoundDuration.TotalSeconds
            }));
        }
    }

    public sealed class KingdomPolicyTimeoutStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.kingdom";

        public string Name => "policy_timeout_state";

        public string Description => "Reports kingdom policy-timeout fixture state.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer) return Failed("Command can only be run on a client.");

            var kingdomScreen = ScreenManager.TopScreen as GauntletKingdomScreen;
            DecisionItemBaseVM decision = kingdomScreen?.DataSource?.Decision?.CurrentDecision;
            string decisionTitle = decision?.TitleText ?? string.Empty;
            return Succeeded(PolicyTimeoutJsonResult(new
            {
                success = true,
                kingdomScreenActive = Game.Current?.GameStateManager?.ActiveState is KingdomState,
                topScreenIsKingdom = kingdomScreen != null,
                decisionPresent = decision != null,
                decisionActive = decision?.IsActive ?? false,
                decisionTitle,
                hasVotingCountdown = decisionTitle.IndexOf("Voting ends in", StringComparison.OrdinalIgnoreCase) >= 0,
                inquiryActive = InformationManager.IsAnyInquiryActive()
            }));
        }
    }

    public sealed class KingdomPolicyTimeoutRestoreCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.kingdom";

        public string Name => "policy_timeout_restore";

        public string Description => "Restores the kingdom policy-timeout fixture.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("kingdom_id", "The registered kingdom id."),
            new ExpectedArgs("proposer_clan_id", "The registered proposer clan id."),
            new ExpectedArgs("policy_id", "The registered policy id."),
            new ExpectedArgs("policy_was_active", "Whether the policy was active at capture."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("Command can only be run on the server.");
            if (!bool.TryParse(args[3], out bool policyWasActive))
                return Failed($"Unable to parse {args[3]} as a boolean.");
            if (!TryMatchPendingPolicyTimeoutFixture(args[0], args[1], args[2], policyWasActive, out var fixture, out string error))
                return Failed(error);

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
            return Succeeded(PolicyTimeoutJsonResult(new
            {
                success = true,
                fixture.KingdomId,
                fixture.PolicyId,
                restoredPolicyActive = fixture.Kingdom.ActivePolicies.Contains(fixture.Policy),
                unresolvedDecisionCount = fixture.Kingdom.UnresolvedDecisions.Count
            }));
        }
    }

    public sealed class KingdomPolicyTimeoutVerifyCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.kingdom";

        public string Name => "policy_timeout_verify";

        public string Description => "Verifies the kingdom policy-timeout fixture.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("kingdom_id", "The registered kingdom id."),
            new ExpectedArgs("policy_id", "The registered policy id."),
            new ExpectedArgs("policy_was_active", "Whether the policy was active at capture."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("Command can only be run on the server.");
            if (!bool.TryParse(args[2], out bool policyWasActive))
                return Failed($"Unable to parse {args[2]} as a boolean.");
            if (pendingPolicyTimeoutFixture != null) return Failed("The policy-timeout fixture lifecycle is still active.");
            if (!TryGetObjectManager(out var objectManager)) return Failed("Unable to resolve ObjectManager");
            if (!objectManager.TryGetObject(args[0], out Kingdom kingdom)) return Failed($"Kingdom with ID: '{args[0]}' not found");
            if (!objectManager.TryGetObject(args[1], out PolicyObject policy)) return Failed($"PolicyObject with ID: '{args[1]}' not found");

            bool policyIsActive = kingdom.ActivePolicies.Contains(policy);
            bool decisionPresent = kingdom.UnresolvedDecisions
                .OfType<KingdomPolicyDecision>()
                .Any(decision => decision.Policy == policy);
            bool success = policyIsActive == policyWasActive && !decisionPresent && kingdom.UnresolvedDecisions.Count == 0;
            string output = PolicyTimeoutJsonResult(new
            {
                success,
                kingdomId = args[0],
                policyId = args[1],
                expectedPolicyActive = policyWasActive,
                policyIsActive,
                decisionPresent,
                unresolvedDecisionCount = kingdom.UnresolvedDecisions.Count
            });
            return success ? Succeeded(output) : Failed(output);
        }
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

    private static string PolicyTimeoutJsonResult(object value) =>
        "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(value);

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

    // coop.debug.kingdom.create Derthert "Vlandia Reborn"
    /// <summary>
    /// Creates a kingdom ruled by the named hero's clan and replicates it to every client through the
    /// same notification the governor "create kingdom" dialog uses. Server only.
    /// </summary>
    /// <param name="args">Leader hero id or quoted display name, then the quoted kingdom name.</param>
    /// <returns>result message</returns>

    public sealed class KingdomCreateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.kingdom";

        public string Name => "create";

        public string Description => "Creates a kingdom for a clan leader on the server.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("leader_hero_name", "The exact leader display name or id. Quote multi-word names."),
            new ExpectedArgs("kingdom_name", "The kingdom display name. Quote multi-word values."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("This command can only be run on the server.");
            if (Campaign.Current == null) return Failed("No campaign is loaded.");
            if (!TryGetObjectManager(out var objectManager)) return Failed("Unable to resolve ObjectManager");
            if (!ContainerProvider.TryResolve<IKingdomCreator>(out var kingdomCreator)) return Failed("Unable to resolve KingdomCreator");

            if (!TryGetLeaderHero(objectManager, args[0], out Hero leader, out string heroError)) return Failed(heroError);
            if (leader.IsDead) return Failed($"{leader.Name} ({leader.StringId}) is dead and cannot rule a kingdom.");

            Clan clan = leader.Clan;
            if (clan == null) return Failed($"{leader.Name} ({leader.StringId}) does not belong to a clan.");

            // A kingdom is ruled by its ruling clan's leader, and clan leadership changes have no sync yet,
            // so reject rather than silently create a kingdom ruled by a different hero.
            if (clan.Leader != leader) return Failed($"{leader.Name} does not lead clan {clan.StringId}. Pass the clan leader instead.");

            string kingdomName = args[1];
            if (!KingdomHandler.CanCreateKingdomForClan(clan, kingdomName, out string reason))
            {
                return Failed($"Unable to create kingdom {kingdomName}: {reason}.");
            }

            CultureObject culture = clan.Culture ?? leader.Culture;
            if (culture == null) return Failed($"Clan {clan.StringId} has no culture for the new kingdom to inherit.");

            // A debug-created kingdom usually has no owning player; an empty controller id makes the
            // notification's settlement-restore steps a no-op on the server and every client.
            TryGetPlayerManager(out var playerManager);
            objectManager.TryGetId(clan, out string clanId);
            string controllerId = playerManager?.Players.FirstOrDefault(player => player.ClanId == clanId)?.ControllerId ?? string.Empty;

            if (!kingdomCreator.TryCreateKingdom(clan, kingdomName, culture, controllerId, out string kingdomId, out string createError))
            {
                return Failed($"Unable to create kingdom {kingdomName}: {createError}.");
            }

            return Succeeded($"Created kingdom '{kingdomName}' ({kingdomId}) ruled by {leader.Name} of clan {clan.StringId}.");
        }
    }

    /// <summary>
    /// Resolves the leader from a coop object manager id, a game StringId, or a display name. Quote a
    /// multi-word display name so it arrives in this fixed argument slot.
    /// </summary>
    private static bool TryGetLeaderHero(IObjectManager objectManager, string nameOrId, out Hero hero, out string error)
    {
        error = null;
        if (objectManager.TryGetObject(nameOrId, out hero)) return true;

        string heroName = nameOrId.Replace('_', ' ').Trim();
        hero = Hero.AllAliveHeroes.FirstOrDefault(candidate => candidate.StringId == nameOrId)
            ?? Hero.AllAliveHeroes.FirstOrDefault(candidate => string.Equals(candidate.Name?.ToString(), heroName, StringComparison.OrdinalIgnoreCase));
        if (hero != null) return true;

        error = $"No hero '{nameOrId}' found by coop id, game StringId, or name. Run coop.debug.hero.id \"<hero name>\" to look one up.";
        return false;
    }

    // coop.debug.kingdom.list
    /// <summary>
    /// Lists all the current Kingdoms
    /// </summary>
    /// <param name="args">actually none are being used..</param>
    /// <returns>strings of all the kingdoms</returns>

    public sealed class KingdomListCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.kingdom";

        public string Name => "list";

        public string Description => "Lists campaign kingdoms.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            StringBuilder stringBuilder = new StringBuilder();

            List<Kingdom> kingdoms = Campaign.Current.CampaignObjectManager.Kingdoms.ToList();
            kingdoms.ForEach((kingdom) =>
            {
                stringBuilder.Append(string.Format("Name: '{0}'\n Id : '{1}'\n", kingdom.Name, kingdom.StringId));
            });
            return Succeeded(stringBuilder.ToString());
        }
    }

    // coop.debug.kingdom.info <kingdomId>
    /// <summary>
    /// Reflection-dumps every field of a Kingdom so a server screenshot and a client screenshot can be
    /// compared field-for-field to confirm Kingdom field syncs still replicate.
    /// </summary>

    public sealed class KingdomInfoCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.kingdom";

        public string Name => "info";

        public string Description => "Reports state for a registered kingdom.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("kingdom_id", "The registered kingdom id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!TryGetObjectManager(out IObjectManager objectManager)) return Failed("Unable to resolve ObjectManager");
            if (objectManager.TryGetObject(args[0], out Kingdom kingdom) == false) return Failed($"Unable to find kingdom with id: {args[0]}");

            StringBuilder stringBuilder = new StringBuilder();
            foreach (var field in typeof(Kingdom).GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
            {
                stringBuilder.AppendLine($"{field.Name} = {field.GetValue(kingdom)}");
            }
            return Succeeded(stringBuilder.ToString());
        }
    }

    // coop.debug.kingdom.force_player_join_kingdom
    /// <summary>
    /// Forces a registered co-op player's clan to join a kingdom. Server only.
    /// </summary>
    /// <param name="args">controller id, kingdom id</param>
    /// <returns>result message</returns>

    public sealed class KingdomForcePlayerJoinCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.kingdom";

        public string Name => "force_player_join_kingdom";

        public string Description => "Moves a player clan into a kingdom.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controller_id", "The player controller id."),
            new ExpectedArgs("kingdom_id", "The registered kingdom id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModInformation.IsServer)
            {
                return Failed("This command can only be run on the server.");
            }

            if (TryGetPlayerManager(out var playerManager) == false)
            {
                return Failed("Unable to resolve PlayerManager");
            }

            if (TryGetObjectManager(out var objectManager) == false)
            {
                return Failed("Unable to resolve ObjectManager");
            }

            if (TryGetKingdomMembershipState(out var kingdomMembershipState) == false)
            {
                return Failed("Unable to resolve KingdomMembershipState");
            }

            string controllerId = args[0];
            string kingdomId = args[1];

            if (!playerManager.TryGetPlayer(controllerId, out var player))
            {
                return Failed($"Player not found with controller id: {controllerId}");
            }

            if (string.IsNullOrEmpty(player.ClanId))
            {
                return Failed($"Player {controllerId} does not have a clan id.");
            }

            if (!objectManager.TryGetObject(player.ClanId, out Clan clan))
            {
                return Failed($"Clan not found for player {controllerId} with clan id: {player.ClanId}");
            }

            if (!objectManager.TryGetObject(kingdomId, out Kingdom kingdom))
            {
                return Failed($"Kingdom not found with id: {kingdomId}");
            }

            Kingdom previousKingdom = clan.Kingdom;
            if (previousKingdom == kingdom)
            {
                return Succeeded($"Player {controllerId}'s clan {clan.StringId} is already in kingdom {kingdom.StringId}.");
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
                return Succeeded($"Tried to force player {controllerId}'s clan {clan.StringId} to join {kingdom.StringId}, but current kingdom is {currentKingdomId}.");
            }

            string previousKingdomId = previousKingdom?.StringId ?? "<none>";
            return Succeeded($"Forced player {controllerId}'s clan {clan.StringId} to join kingdom {kingdom.StringId}. Previous kingdom: {previousKingdomId}.");
        }
    }

    // coop.debug.kingdom.force_player_vassalage Player khuzait true

    public sealed class KingdomForcePlayerVassalageCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.kingdom";

        public string Name => "force_player_vassalage";

        public string Description => "Requests player vassalage in a kingdom.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controller_id", "The player controller id."),
            new ExpectedArgs("kingdom_id", "The registered kingdom id."),
            new ExpectedArgs("grant_rewards", "Whether vassalage rewards should be granted.", false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
            {
                return Failed("This command can only be run on the server.");
            }

            if (!TryGetPlayerManager(out var playerManager))
            {
                return Failed("Unable to resolve PlayerManager");
            }

            if (!playerManager.TryGetPlayer(args[0], out var player))
            {
                return Failed($"Player not found with controller id: {args[0]}");
            }

            if (!playerManager.TryGetPeer(args[0], out var peer))
            {
                return Failed($"Player {args[0]} does not have a connected peer.");
            }

            if (!TryGetObjectManager(out var objectManager) ||
                !objectManager.TryGetObject<Kingdom>(args[1], out var kingdom))
            {
                return Failed($"Kingdom not found with id: {args[1]}");
            }

            bool grantRewards = true;
            if (args.Count == 3 && !bool.TryParse(args[2], out grantRewards))
            {
                return Failed($"Unable to parse {args[2]} as a boolean.");
            }

            MessageBroker.Instance.Publish(peer, new RequestVassalService(kingdom.StringId, grantRewards));
            return Succeeded($"Queued vassalage for player {player.ControllerId} in kingdom {kingdom.StringId}. GrantRewards={grantRewards}.");
        }
    }

    // coop.debug.kingdom.add_decision_usage
    /// <summary>
    /// Lists all the usages of add_decision command.
    /// </summary>
    /// <param name="args">actually none are being used..</param>
    /// <returns>strings of all the usages</returns>

    public sealed class KingdomAddDecisionUsageCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.kingdom";

        public string Name => "add_decision_usage";

        public string Description => "Lists supported kingdom decision arguments.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
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

            return Succeeded(stringBuilder.ToString());
        }
    }

    // coop.debug.kingdom.remove_decision_usage
    /// <summary>
    /// Returns the usage of remove_decision command's usage.
    /// </summary>
    /// <param name="args">actually none are being used..</param>
    /// <returns>strings of usage.</returns>

    public sealed class KingdomRemoveDecisionUsageCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.kingdom";

        public string Name => "remove_decision_usage";

        public string Description => "Describes kingdom decision removal arguments.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.Append(RemoveUsage);

            return Succeeded(stringBuilder.ToString());
        }
    }

    // coop.debug.kingdom.list_decisions
    /// <summary>
    /// Lists all the decisions of a specific kingdom.
    /// </summary>
    /// <param name="args">actually none are being used..</param>
    /// <returns>strings of all the decisions of a specific kingdom</returns>

    public sealed class KingdomListDecisionsCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.kingdom";

        public string Name => "list_kingdom_decisions";

        public string Description => "Lists queued decisions for a kingdom.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("kingdom_id", "The registered kingdom id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (TryGetObjectManager(out var objectManager) == false)
            {
                return Failed("Unable to resolve ObjectManager");
            }

            if (objectManager.TryGetObject(args[0], out Kingdom kingdom) == false)
            {
                return Failed($"ID: '{args[0]}' not found");
            }

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append($"Kingdom decisions of Kingdom: {kingdom.Name}\n");

            int i = 1;
            foreach (KingdomDecision kingdomDecision in kingdom.UnresolvedDecisions)
            {
                stringBuilder.Append($"{i}. {kingdomDecision.GetType().Name}\n");
                i++;
            }

            return Succeeded(stringBuilder.ToString());
        }
    }

    // coop.debug.kingdom.decisions
    /// <summary>
    /// Lists active kingdom decisions and registered client vote state.
    /// </summary>
    /// <param name="args">first arg : kingdomId</param>
    /// <returns>strings of all active kingdom decisions with client votes</returns>

    public sealed class KingdomDecisionsCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.kingdom";

        public string Name => "decisions";

        public string Description => "Lists queued decisions and client votes for a kingdom.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("kingdom_id", "The registered kingdom id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (TryGetObjectManager(out var objectManager) == false)
            {
                return Failed("Unable to resolve ObjectManager");
            }

            if (TryGetPlayerManager(out var playerManager) == false)
            {
                return Failed("Unable to resolve PlayerManager");
            }

            if (TryGetKingdomDecisionVoteManager(out var voteManager) == false)
            {
                return Failed("Unable to resolve KingdomDecisionVoteManager");
            }

            if (objectManager.TryGetObject(args[0], out Kingdom kingdom) == false)
            {
                return Failed($"ID: '{args[0]}' not found");
            }

            IReadOnlyList<KingdomDecisionVoteManager.KingdomDecisionDebugInfo> decisionInfos =
                voteManager.GetDecisionDebugInfo(kingdom);

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"Active decisions of Kingdom: {kingdom.Name} ({kingdom.StringId})");
            stringBuilder.AppendLine($"Registered clients: {playerManager.Players.Count}");

            if (decisionInfos.Count == 0)
            {
                stringBuilder.AppendLine("(none)");
                return Succeeded(stringBuilder.ToString());
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

            return Succeeded(stringBuilder.ToString());
        }
    }

    // coop.debug.kingdom.list_decision_outcomes
    /// <summary>
    /// Lists the outcomes for a queued kingdom decision.
    /// </summary>
    /// <param name="args">first arg : kingdomId ; second arg : 1-based decision index</param>
    /// <returns>strings of all outcomes of a decision</returns>

    public sealed class KingdomListDecisionOutcomesCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.kingdom";

        public string Name => "list_decision_outcomes";

        public string Description => "Lists possible outcomes for a kingdom decision.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("kingdom_id", "The registered kingdom id."),
            new ExpectedArgs("decision_index", "The one-based decision index."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!TryGetKingdomDecisionByIndex(args, out Kingdom kingdom, out KingdomDecision decision, out int _, out string message))
            {
                return Failed(message);
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
            return Succeeded(stringBuilder.ToString());
        }
    }

    // coop.debug.kingdom.vote_decision
    /// <summary>
    /// Requests a vote for a queued kingdom decision from the local client.
    /// </summary>
    /// <param name="args">kingdomId, 1-based decision index, 1-based outcome index or abstain, support weight</param>
    /// <returns>result message</returns>

    public sealed class KingdomVoteDecisionCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.kingdom";

        public string Name => "vote_decision";

        public string Description => "Requests a client vote on a kingdom decision.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("kingdom_id", "The registered kingdom id."),
            new ExpectedArgs("decision_index", "The one-based decision index."),
            new ExpectedArgs("outcome", "A one-based outcome index or abstain."),
            new ExpectedArgs("support_weight", "The support weight name or value."),
            new ExpectedArgs("is_final", "Whether this is the final vote.", false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!TryGetKingdomDecisionByIndex(args, out Kingdom kingdom, out KingdomDecision decision, out int decisionIndex, out string message))
            {
                return Failed(message);
            }

            bool isAbstain = args[2].Equals("abstain", StringComparison.OrdinalIgnoreCase);
            int outcomeIndex = -1;
            if (!isAbstain)
            {
                if (!int.TryParse(args[2], out int parsedOutcomeIndex))
                {
                    return Failed($"Outcome index is not a number: {args[2]}");
                }
                outcomeIndex = parsedOutcomeIndex - 1;
            }

            if (!TryParseSupportWeight(args[3], out Supporter.SupportWeights supportWeight))
            {
                return Failed($"Support weight is invalid: {args[3]}. Use Choose, StayNeutral, SlightlyFavor, StronglyFavor, or FullyPush.");
            }

            bool finalVote = false;
            if (args.Count == 5 && !bool.TryParse(args[4], out finalVote))
            {
                return Failed($"Unable to parse {args[4]} as a boolean.");
            }

            if (TryGetObjectManager(out var objectManager) == false)
            {
                return Failed("Unable to resolve ObjectManager");
            }
            if (!objectManager.TryGetIdWithLogging(kingdom, out string kingdomId))
            {
                return Failed("Unable to resolve kingdom id.");
            }

            MessageBroker.Instance.Publish(decision, new KingdomDecisionVoteRequested(
                new KingdomDecisionVoteData(kingdomId, decisionIndex, outcomeIndex, (int)supportWeight, isAbstain, finalVote)));

            return Succeeded($"Requested vote for {decision.GetType().Name}: outcome={args[2]}, support={supportWeight}, final={finalVote}.");
        }
    }

    // coop.debug.kingdom.resolve_decision
    /// <summary>
    /// Resolves a queued player kingdom decision after every vote or the shared deadline.
    /// </summary>
    /// <param name="args">kingdomId, 1-based decision index</param>
    /// <returns>result message</returns>

    public sealed class KingdomResolveDecisionCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.kingdom";

        public string Name => "resolve_decision";

        public string Description => "Resolves a queued kingdom decision.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("kingdom_id", "The registered kingdom id."),
            new ExpectedArgs("decision_index", "The one-based decision index."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!TryGetKingdomDecisionByIndex(args, out Kingdom _, out KingdomDecision decision, out int _, out string message))
            {
                return Failed(message);
            }

            if (TryGetKingdomDecisionVoteManager(out var voteManager) == false)
            {
                return Failed("Unable to resolve KingdomDecisionVoteManager");
            }

            return voteManager.TryResolveDecision(decision)
                ? Succeeded($"Resolved {decision.GetType().Name} through player vote manager.")
                : Failed($"Could not resolve {decision.GetType().Name} through player vote manager.");
        }
    }

    // coop.debug.kingdom.list_policies
    /// <summary>
    /// Lists the active policies of a specific kingdom. Useful for verifying that a policy change
    /// resolved on the server has replicated to clients.
    /// </summary>
    /// <param name="args">first arg : kingdomId</param>
    /// <returns>strings of all the active policies of a specific kingdom</returns>

    public sealed class KingdomListPoliciesCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.kingdom";

        public string Name => "list_policies";

        public string Description => "Lists active policies for a kingdom.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("kingdom_id", "The registered kingdom id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (TryGetObjectManager(out var objectManager) == false)
            {
                return Failed("Unable to resolve ObjectManager");
            }

            if (objectManager.TryGetObject(args[0], out Kingdom kingdom) == false)
            {
                return Failed($"ID: '{args[0]}' not found");
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

            return Succeeded(stringBuilder.ToString());
        }
    }

    // coop.debug.kingdom.collection_list clans kingdom_V1
    /// <summary>
    /// Lists one of the synced Kingdom collection caches for server/client verification.
    /// </summary>
    /// <param name="args">collection name and kingdom id</param>
    /// <returns>IDs currently present in the selected collection</returns>

    public sealed class KingdomCollectionListCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.kingdom";

        public string Name => "collection_list";

        public string Description => "Lists a synced kingdom collection.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("collection", "The kingdom collection name."),
            new ExpectedArgs("kingdom_id", "The registered kingdom id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (TryGetObjectManager(out var objectManager) == false)
            {
                return Failed("Unable to resolve ObjectManager");
            }

            if (objectManager.TryGetObject(args[1], out Kingdom kingdom) == false)
            {
                return Failed($"Kingdom with ID: '{args[1]}' not found");
            }

            var collectionName = NormalizeCollectionName(args[0]);
            if (collectionName == "activepolicies")
            {
                return Succeeded(FormatCollection(objectManager, args[0], kingdom.ActivePolicies.Cast<object>()));
            }

            if (collectionName == "unresolveddecisions")
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append($"Kingdom decisions of Kingdom: {kingdom.Name}\n");

                int i = 1;
                foreach (KingdomDecision kingdomDecision in kingdom.UnresolvedDecisions)
                {
                    stringBuilder.Append($"{i}. {kingdomDecision.GetType().Name}\n");
                    i++;
                }

                return Succeeded(stringBuilder.ToString());
            }

            if (!TryParseCollectionTarget(args[0], out var collectionType, out var parseMessage))
            {
                return Failed(parseMessage);
            }

            return Succeeded(FormatCollection(objectManager, args[0], GetCollectionValues(kingdom, collectionType)));
        }
    }

    // coop.debug.kingdom.collection_add clans kingdom_V1 clan_1
    /// <summary>
    /// Adds an item to a synced Kingdom collection on the server and broadcasts the change to clients.
    /// </summary>
    /// <param name="args">collection name, kingdom id, and value id</param>
    /// <returns>Result of the collection add</returns>

    public sealed class KingdomCollectionAddCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.kingdom";

        public string Name => "collection_add";

        public string Description => "Adds a value to a synced kingdom collection.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("collection", "The kingdom collection name."),
            new ExpectedArgs("kingdom_id", "The registered kingdom id."),
            new ExpectedArgs("value_id", "The registered value id or proposer clan id."),
            new ExpectedArgs("ignore_influence_cost", "For unresolvedDecisions, whether influence is ignored.", false),
            new ExpectedArgs("decision_type", "For unresolvedDecisions, the decision type.", false),
            new ExpectedArgs("decision_arg1", "The first fixed decision-specific argument.", false),
            new ExpectedArgs("decision_arg2", "The second fixed decision-specific argument.", false),
            new ExpectedArgs("decision_arg3", "The third fixed decision-specific argument.", false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("Command is only available to run on the server");

            var collectionName = NormalizeCollectionName(args[0]);
            if (collectionName == "activepolicies")
                return ChangeActivePolicy(args, CollectionOperation.Add);

            if (collectionName == "unresolveddecisions")
            {
                var forwardedArgs = args.Skip(1).ToList();
                string kingdomId = forwardedArgs[0];
                string clanId = forwardedArgs[1];
                string ignoreInfluence = forwardedArgs[2];
                string decisionType = forwardedArgs[3];

                if (TryGetObjectManager(out var decisionObjectManager) == false)
                {
                    return Failed("Unable to resolve ObjectManager");
                }
                if (decisionObjectManager.TryGetObject(kingdomId, out Kingdom decisionKingdom) == false)
                {
                    return Failed($"Kingdom with ID: '{kingdomId}' not found");
                }

                if (decisionObjectManager.TryGetObject(clanId, out Clan proposerClan) == false)
                {
                    return Failed($"Clan with ID: '{clanId}' not found");
                }

                if (!bool.TryParse(ignoreInfluence, out bool ignoreInfluenceCost))
                {
                    return Failed($"Couldnt convert ignoreInfluenceCost: {ignoreInfluence}");
                }

                if (!TryGetKingdomDecisionFunc.ContainsKey(decisionType))
                {
                    return Failed($"Kingdom decision type: {decisionType} does not exist.");
                }

                if (!TryGetKingdomDecisionFunc[decisionType](decisionObjectManager, forwardedArgs, proposerClan, out KingdomDecision kingdomDecision, out string message))
                {
                    return Failed(message);
                }

                decisionKingdom.AddDecision(kingdomDecision, ignoreInfluenceCost);
                return Succeeded($"Kingdom decision added successfully.");
            }

            if (!TryParseCollectionTarget(args[0], out var collectionType, out var parseMessage))
                return Failed(parseMessage);
            if (!TryGetObjectManager(out var objectManager))
                return Failed("Unable to resolve ObjectManager");
            if (!objectManager.TryGetObject(args[1], out Kingdom kingdom))
                return Failed($"Kingdom with ID: '{args[1]}' not found");
            if (!TryResolveCollectionValue(objectManager, collectionType, args[2], out var value, out var resolveMessage))
                return Failed(resolveMessage);

            ApplyCollectionChange(kingdom, collectionType, CollectionOperation.Add, value);
            return Succeeded($"{CollectionOperation.Add} {args[2]} in {args[0]} for kingdom {args[1]}.");
        }
    }

    // coop.debug.kingdom.collection_remove clans kingdom_V1 clan_1
    /// <summary>
    /// Removes an item from a synced Kingdom collection on the server and broadcasts the change to clients.
    /// </summary>
    /// <param name="args">collection name, kingdom id, and value id</param>
    /// <returns>Result of the collection remove</returns>

    public sealed class KingdomCollectionRemoveCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.kingdom";

        public string Name => "collection_remove";

        public string Description => "Removes a value from a synced kingdom collection.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("collection", "The kingdom collection name."),
            new ExpectedArgs("kingdom_id", "The registered kingdom id."),
            new ExpectedArgs("value_id_or_index", "The registered value id or one-based decision index."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("Command is only available to run on the server");

            var collectionName = NormalizeCollectionName(args[0]);
            if (collectionName == "activepolicies")
                return ChangeActivePolicy(args, CollectionOperation.Remove);

            if (collectionName == "unresolveddecisions")
            {
                var forwardedArgs = args.Skip(1).ToList();
                string kingdomId = forwardedArgs[0];
                string index = forwardedArgs[1];

                if (TryGetObjectManager(out var decisionObjectManager) == false)
                {
                    return Failed("Unable to resolve ObjectManager");
                }
                if (decisionObjectManager.TryGetObject(kingdomId, out Kingdom decisionKingdom) == false)
                {
                    return Failed($"Kingdom with ID: '{kingdomId}' not found");
                }

                if (!int.TryParse(index, out int idx))
                {
                    return Failed($"Argument2: {index} is not a number.");
                }

                var decisions = decisionKingdom._unresolvedDecisions;
                if (idx > 0 && idx <= decisions.Count)
                {
                    decisionKingdom.RemoveDecision(decisions[idx - 1]);
                }
                else
                {
                    return Failed("Index is out of bounds.");
                }

                return Succeeded($"Kingdom decision removed.");
            }

            if (!TryParseCollectionTarget(args[0], out var collectionType, out var parseMessage))
                return Failed(parseMessage);
            if (!TryGetObjectManager(out var objectManager))
                return Failed("Unable to resolve ObjectManager");
            if (!objectManager.TryGetObject(args[1], out Kingdom kingdom))
                return Failed($"Kingdom with ID: '{args[1]}' not found");
            if (!TryResolveCollectionValue(objectManager, collectionType, args[2], out var value, out var resolveMessage))
                return Failed(resolveMessage);

            ApplyCollectionChange(kingdom, collectionType, CollectionOperation.Remove, value);
            return Succeeded($"{CollectionOperation.Remove} {args[2]} in {args[0]} for kingdom {args[1]}.");
        }
    }

    // coop.debug.kingdom.declare_war
    /// <summary>
    /// Directly declares war between two factions (run on the server). Deterministic alternative
    /// to a DeclareWarDecision, which the kingdom AI may vote against.
    /// </summary>
    /// <param name="args">first arg : faction1Id ; second arg : faction2Id</param>
    /// <returns>result message</returns>

    public sealed class KingdomDeclareWarCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.kingdom";

        public string Name => "declare_war";

        public string Description => "Declares war between two factions on the server.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("faction1_id", "The first registered faction id."),
            new ExpectedArgs("faction2_id", "The second registered faction id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (TryGetObjectManager(out var objectManager) == false)
            {
                return Failed("Unable to resolve ObjectManager");
            }

            if (TryGetFaction(objectManager, args[0], out IFaction faction1) == false)
            {
                return Failed($"Faction not found with id: {args[0]}");
            }

            if (TryGetFaction(objectManager, args[1], out IFaction faction2) == false)
            {
                return Failed($"Faction not found with id: {args[1]}");
            }

            DeclareWarAction.ApplyByDefault(faction1, faction2);
            return Succeeded($"Declared war between '{faction1.Name}' and '{faction2.Name}'.");
        }
    }

    // coop.debug.kingdom.make_peace
    /// <summary>
    /// Directly makes peace between two factions (run on the server).
    /// </summary>
    /// <param name="args">first arg : faction1Id ; second arg : faction2Id</param>
    /// <returns>result message</returns>

    public sealed class KingdomMakePeaceCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.kingdom";

        public string Name => "make_peace";

        public string Description => "Makes peace between two factions on the server.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("faction1_id", "The first registered faction id."),
            new ExpectedArgs("faction2_id", "The second registered faction id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (TryGetObjectManager(out var objectManager) == false)
            {
                return Failed("Unable to resolve ObjectManager");
            }

            if (TryGetFaction(objectManager, args[0], out IFaction faction1) == false)
            {
                return Failed($"Faction not found with id: {args[0]}");
            }

            if (TryGetFaction(objectManager, args[1], out IFaction faction2) == false)
            {
                return Failed($"Faction not found with id: {args[1]}");
            }

            MakePeaceAction.Apply(faction1, faction2);
            return Succeeded($"Made peace between '{faction1.Name}' and '{faction2.Name}'.");
        }
    }

    // coop.debug.kingdom.force_ally
    /// <summary>
    /// forms an alliance between two kingdoms (run on the server). Alternative
    /// to StartAllianceDecision, which the kingdom AI may vote against. If the kingdoms are at war,
    /// peace is made first.
    /// </summary>
    /// <param name="args">kingdom1Id, kingdom2Id</param>
    /// <returns>result message</returns>
    [CommandLineFunctionality.CommandLineArgumentFunction("force_ally", "coop.debug.kingdom")]
    public static string ForceAlly(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Command is only available to run on the server";
        }

        if (args.Count < 2)
        {
            return "Usage: coop.debug.kingdom.force_ally <kingdom1Id> <kingdom2Id> (run on the server)";
        }

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (TryGetKingdomPair(objectManager, args, out var kingdom1, out var kingdom2, out var pairError) == false)
        {
            return pairError;
        }

        var behavior = Campaign.Current.GetCampaignBehavior<AllianceCampaignBehavior>();
        if (behavior == null)
        {
            return "AllianceCampaignBehavior is not available.";
        }

        if (behavior.IsAllyWithKingdom(kingdom1, kingdom2))
        {
            return $"'{kingdom1.Name}' and '{kingdom2.Name}' are already allied.";
        }

        if (kingdom1.IsAtWarWith(kingdom2))
        {
            MakePeaceAction.Apply(kingdom1, kingdom2);
        }

        behavior.StartAlliance(kingdom1, kingdom2);
        return $"Forced alliance between '{kingdom1.Name}' and '{kingdom2.Name}'.";
    }

    // coop.debug.kingdom.force_trade_agreement
    /// <summary>
    /// forms a trade agreement between two kingdoms (run on the server). Alternative to TradeAgreementDecision,
    /// which the kingdom AI may vote against. If the kingdoms are at war, peace is made first.
    /// </summary>
    /// <param name="args">kingdom1Id, kingdom2Id</param>
    /// <returns>result message</returns>
    [CommandLineFunctionality.CommandLineArgumentFunction("force_trade_agreement", "coop.debug.kingdom")]
    public static string ForceTradeAgreement(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Command is only available to run on the server";
        }

        if (args.Count < 2)
        {
            return "Usage: coop.debug.kingdom.force_trade_agreement <kingdom1Id> <kingdom2Id> (run on the server)";
        }

        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";
        }

        if (TryGetKingdomPair(objectManager, args, out var kingdom1, out var kingdom2, out var pairError) == false)
        {
            return pairError;
        }

        var behavior = Campaign.Current.GetCampaignBehavior<TradeAgreementsCampaignBehavior>();
        if (behavior == null)
        {
            return "TradeAgreementsCampaignBehavior is not available.";
        }

        if (behavior.HasTradeAgreement(kingdom1, kingdom2, out _))
        {
            return $"'{kingdom1.Name}' and '{kingdom2.Name}' already have a trade agreement.";
        }

        if (kingdom1.IsAtWarWith(kingdom2))
        {
            MakePeaceAction.Apply(kingdom1, kingdom2);
        }

        behavior.MakeTradeAgreement(
            kingdom1,
            kingdom2,
            Campaign.Current.Models.TradeAgreementModel.GetTradeAgreementDurationInYears(kingdom1, kingdom2));

        return $"Forced trade agreement between '{kingdom1.Name}' and '{kingdom2.Name}'.";
    }

    internal static bool TryGetKingdomPair(IObjectManager objectManager, List<string> args, out Kingdom kingdom1, out Kingdom kingdom2, out string error)
    {
        kingdom2 = null;
        error = null;

        if (objectManager.TryGetObject(args[0], out kingdom1) == false)
        {
            error = $"Kingdom not found with id: {args[0]}";
            return false;
        }

        if (objectManager.TryGetObject(args[1], out kingdom2) == false)
        {
            error = $"Kingdom not found with id: {args[1]}";
            return false;
        }

        if (kingdom1 == kingdom2)
        {
            error = "The two kingdom ids must refer to different kingdoms.";
            return false;
        }

        if (kingdom1.IsEliminated)
        {
            error = $"Kingdom '{kingdom1.Name}' has been eliminated.";
            return false;
        }

        if (kingdom2.IsEliminated)
        {
            error = $"Kingdom '{kingdom2.Name}' has been eliminated.";
            return false;
        }

        return true;
    }

    private static CoopCommandResult ChangeActivePolicy(
        IReadOnlyList<string> args,
        CollectionOperation operation)
    {
        if (!TryGetObjectManager(out var objectManager))
            return Failed("Unable to resolve ObjectManager");
        if (!objectManager.TryGetObject(args[1], out Kingdom kingdom))
            return Failed($"Kingdom with ID: '{args[1]}' not found");
        if (!objectManager.TryGetObject(args[2], out PolicyObject policy))
            return Failed($"PolicyObject with ID: '{args[2]}' not found");

        if (operation == CollectionOperation.Add)
        {
            kingdom.AddPolicy(policy);
            return Succeeded($"Added policy {args[2]} to kingdom {args[1]}.");
        }

        kingdom.RemovePolicy(policy);
        return Succeeded($"Removed policy {args[2]} from kingdom {args[1]}.");
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

    private static bool TryGetKingdomDecisionByIndex(IReadOnlyList<string> args, out Kingdom kingdom, out KingdomDecision decision, out int zeroBasedIndex, out string message)
    {
        kingdom = null;
        decision = null;
        zeroBasedIndex = -1;

        if (TryGetObjectManager(out var objectManager) == false)
        {
            message = "Unable to resolve ObjectManager";
            return false;
        }

        if (objectManager.TryGetObject(args[0], out kingdom) == false)
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

    public sealed class KingdomAddDecisionCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.kingdom";

        public string Name => "add_decision";

        public string Description => "Adds a supported decision to a kingdom.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("kingdom_id", "The registered kingdom id."),
            new ExpectedArgs("proposer_clan_id", "The registered proposer clan id."),
            new ExpectedArgs("ignore_influence_cost", "Whether influence cost is ignored."),
            new ExpectedArgs("decision_type", "The decision type."),
            new ExpectedArgs("decision_arg1", "The first fixed decision-specific argument.", false),
            new ExpectedArgs("decision_arg2", "The second fixed decision-specific argument.", false),
            new ExpectedArgs("decision_arg3", "The third fixed decision-specific argument.", false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            string kingdomId = args[0];
            string clanId = args[1];
            string ignoreInfluence = args[2];
            string decisionType = args[3];

            if (TryGetObjectManager(out var objectManager) == false)
            {
                return Failed("Unable to resolve ObjectManager");
            }
            if (objectManager.TryGetObject(kingdomId, out Kingdom kingdom) == false)
            {
                return Failed($"Kingdom with ID: '{kingdomId}' not found");
            }

            if (objectManager.TryGetObject(clanId, out Clan proposerClan) == false)
            {
                return Failed($"Clan with ID: '{clanId}' not found");
            }

            if (!bool.TryParse(ignoreInfluence, out bool ignoreInfluenceCost))
            {
                return Failed($"Couldnt convert ignoreInfluenceCost: {ignoreInfluence}");
            }

            if (!TryGetKingdomDecisionFunc.ContainsKey(decisionType))
            {
                return Failed($"Kingdom decision type: {decisionType} does not exist.");
            }

            if (!TryGetKingdomDecisionFunc[decisionType](objectManager, args, proposerClan, out KingdomDecision kingdomDecision, out string message))
            {
                return Failed(message);
            }

            kingdom.AddDecision(kingdomDecision, ignoreInfluenceCost);
            return Succeeded($"Kingdom decision added successfully.");
        }
    }

    // coop.debug.kingdom.remove_decision
    /// <summary>
    /// Removes a decision from a Kingdom
    /// </summary>
    /// <param name="args">first arg : kingdomId ; second arg : index of decision to remove</param>
    /// <returns></returns>

    public sealed class KingdomRemoveDecisionCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.kingdom";

        public string Name => "remove_decision";

        public string Description => "Removes a queued decision from a kingdom.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("kingdom_id", "The registered kingdom id."),
            new ExpectedArgs("decision_index", "The one-based decision index."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            string kingdomId = args[0];
            string index = args[1];

            if (TryGetObjectManager(out var objectManager) == false)
            {
                return Failed("Unable to resolve ObjectManager");
            }
            if (objectManager.TryGetObject(kingdomId, out Kingdom kingdom) == false)
            {
                return Failed($"Kingdom with ID: '{kingdomId}' not found");
            }

            if (!int.TryParse(index, out int idx))
            {
                return Failed($"Argument2: {index} is not a number.");
            }

            var decisions = kingdom._unresolvedDecisions;
            if (idx > 0 && idx <= decisions.Count)
            {
                kingdom.RemoveDecision(decisions[idx - 1]);
            }
            else
            {
                return Failed("Index is out of bounds.");
            }

            return Succeeded($"Kingdom decision removed.");
        }
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
    private static bool TryGetDeclareWarDecision(IObjectManager objectManager, IReadOnlyList<string> args, Clan proposerClan, out KingdomDecision kingdomDecision, out string message)
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
    private static bool TryGetExpelClanFromKingdomDecision(IObjectManager objectManager, IReadOnlyList<string> args, Clan proposerClan, out KingdomDecision kingdomDecision, out string message)
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
    private static bool TryGetKingSelectionKingdomDecision(IObjectManager objectManager, IReadOnlyList<string> args, Clan proposerClan, out KingdomDecision kingdomDecision, out string message)
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
    private static bool TryGetKingdomPolicyDecision(IObjectManager objectManager, IReadOnlyList<string> args, Clan proposerClan, out KingdomDecision kingdomDecision, out string message)
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
    private static bool TryGetSettlementClaimantDecision(IObjectManager objectManager, IReadOnlyList<string> args, Clan proposerClan, out KingdomDecision kingdomDecision, out string message)
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
    private static bool TryGetSettlementClaimantPreliminaryDecision(IObjectManager objectManager, IReadOnlyList<string> args, Clan proposerClan, out KingdomDecision kingdomDecision, out string message)
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

    private static bool TryGetAcceptCallToWarAgreementDecision(IObjectManager objectManager, IReadOnlyList<string> args, Clan proposerClan, out KingdomDecision kingdomDecision, out string message)
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

    private static bool TryGetProposeCallToWarAgreementDecision(IObjectManager objectManager, IReadOnlyList<string> args, Clan proposerClan, out KingdomDecision kingdomDecision, out string message)
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

    private static bool TryGetStartAllianceDecision(IObjectManager objectManager, IReadOnlyList<string> args, Clan proposerClan, out KingdomDecision kingdomDecision, out string message)
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

    private static bool TryGetTradeAgreementDecision(IObjectManager objectManager, IReadOnlyList<string> args, Clan proposerClan, out KingdomDecision kingdomDecision, out string message)
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

    //private static bool TryGetMakePeaceKingdomDecision(IObjectManager objectManager, IReadOnlyList<string> args, Clan proposerClan, out KingdomDecision kingdomDecision, out string message)
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
