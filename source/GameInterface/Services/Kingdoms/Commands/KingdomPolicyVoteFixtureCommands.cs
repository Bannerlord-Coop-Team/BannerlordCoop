#if DEBUG
using Common;
using Common.Messaging;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using SandBox.GauntletUI;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions.ItemTypes;
using TaleWorlds.Core;
using TaleWorlds.ScreenSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Kingdoms.Commands;

public class KingdomPolicyVoteFixtureCommands
{
    private static PolicyVoteFixture activeFixture;

    [CommandLineArgumentFunction("policy_vote_fixture_preflight", "coop.debug.kingdom")]
    public static string Preflight(List<string> args)
    {
        const string usage = "Usage: coop.debug.kingdom.policy_vote_fixture_preflight <controllerId> <policyId> <aiClanStringId>";
        if (!ModInformation.IsServer) return "This command can only be run on the server.";
        if (args.Count != 3) return usage;
        if (activeFixture != null) return "A policy vote fixture is already active.";
        if (!ContainerProvider.TryResolve(out IPlayerManager playerManager) ||
            !ContainerProvider.TryResolve(out IObjectManager objectManager))
        {
            return "Unable to resolve policy vote fixture services.";
        }
        if (!playerManager.TryGetPlayer(args[0], out var player))
        {
            return $"No registered player has controller id {args[0]}.";
        }
        if (!objectManager.TryGetObject(player.ClanId, out Clan playerClan))
        {
            return $"Unable to resolve clan {player.ClanId} for player {args[0]}.";
        }
        if (!TryGetPolicy(objectManager, args[1], out PolicyObject policy))
        {
            return $"Unable to resolve policy {args[1]}.";
        }
        if (!TryGetClan(objectManager, args[2], out Clan aiClan))
        {
            return $"Unable to resolve AI clan {args[2]}.";
        }

        string playerKingdom = playerClan.Kingdom?.StringId ?? "none";
        string aiKingdom = aiClan.Kingdom?.StringId ?? "none";
        bool valid = playerClan.Kingdom == null &&
                     playerClan.Leader != null &&
                     playerClan.Culture != null &&
                     aiClan.Kingdom != null &&
                     aiClan.Kingdom.RulingClan != aiClan &&
                     !aiClan.IsUnderMercenaryService &&
                     aiClan != playerClan;
        return $"POLICY_VOTE_FIXTURE_PREFLIGHT valid={Bool(valid)} controller={args[0]} " +
               $"playerClan={playerClan.StringId} playerKingdom={playerKingdom} " +
               $"policy={policy.StringId} aiClan={aiClan.StringId} aiKingdom={aiKingdom}";
    }

    [CommandLineArgumentFunction("policy_vote_fixture_prepare", "coop.debug.kingdom")]
    public static string Prepare(List<string> args)
    {
        const string usage = "Usage: coop.debug.kingdom.policy_vote_fixture_prepare <controllerId> <policyId> <aiClanStringId>";
        if (!ModInformation.IsServer) return "This command can only be run on the server.";
        if (args.Count != 3) return usage;
        if (activeFixture != null) return "A policy vote fixture is already active.";
        if (!ContainerProvider.TryResolve(out IPlayerManager playerManager) ||
            !ContainerProvider.TryResolve(out IObjectManager objectManager) ||
            !ContainerProvider.TryResolve(out IKingdomCreator kingdomCreator) ||
            !ContainerProvider.TryResolve(out IKingdomMembershipState kingdomMembershipState))
        {
            return "Unable to resolve policy vote fixture services.";
        }
        if (!playerManager.TryGetPlayer(args[0], out var player))
        {
            return $"No registered player has controller id {args[0]}.";
        }
        if (!objectManager.TryGetObject(player.ClanId, out Clan playerClan))
        {
            return $"Unable to resolve clan {player.ClanId} for player {args[0]}.";
        }
        if (playerClan.Kingdom != null)
        {
            return $"Player clan {playerClan.StringId} already belongs to kingdom {playerClan.Kingdom.StringId}.";
        }
        if (playerClan.Leader == null || playerClan.Culture == null)
        {
            return $"Player clan {playerClan.StringId} has no leader or culture.";
        }
        if (!TryGetPolicy(objectManager, args[1], out PolicyObject policy))
        {
            return $"Unable to resolve policy {args[1]}.";
        }
        if (!TryGetClan(objectManager, args[2], out Clan aiClan))
        {
            return $"Unable to resolve AI clan {args[2]}.";
        }
        Kingdom aiOriginalKingdom = aiClan.Kingdom;
        if (aiOriginalKingdom == null ||
            aiOriginalKingdom.RulingClan == aiClan ||
            aiClan.IsUnderMercenaryService ||
            aiClan == playerClan)
        {
            return $"AI clan {aiClan.StringId} must be a non-ruling vassal clan in an existing kingdom.";
        }

        if (!kingdomCreator.TryCreateKingdom(
                playerClan,
                "Vote Fallback Test",
                playerClan.Culture,
                args[0],
                out string kingdomId,
                out string createError))
        {
            return $"Unable to create the policy vote fixture kingdom: {createError}.";
        }
        if (!objectManager.TryGetObject(kingdomId, out Kingdom kingdom))
        {
            return $"Created kingdom {kingdomId} was not registered.";
        }

        activeFixture = new PolicyVoteFixture(
            args[0],
            kingdom,
            kingdomId,
            playerClan,
            playerClan.Influence,
            policy,
            aiClan,
            aiOriginalKingdom,
            kingdomMembershipState);

        try
        {
            if (kingdom.ActivePolicies.Contains(policy))
            {
                kingdom.RemovePolicy(policy);
            }

            float influenceTarget = Math.Max(playerClan.Influence, 5000f);
            if (influenceTarget > playerClan.Influence)
            {
                ChangeClanInfluenceAction.Apply(playerClan, influenceTarget - playerClan.Influence);
            }

            ChangeKingdomAction.ApplyByJoinToKingdom(aiClan, kingdom);
            if (aiClan.Kingdom != kingdom || kingdom.Clans.Count < 2)
            {
                string cleanup = RestoreFixture();
                return $"Unable to add AI clan {aiClan.StringId} to the fixture kingdom. {cleanup}";
            }

            objectManager.TryGetId(playerClan, out string playerClanId);
            objectManager.TryGetId(aiClan, out string aiClanId);
            objectManager.TryGetId(policy, out string policyId);
            return $"POLICY_VOTE_FIXTURE_READY controller={args[0]} kingdom={kingdomId} " +
                   $"playerClan={playerClanId} aiClan={aiClanId} policy={policyId}";
        }
        catch (Exception e)
        {
            string cleanup = RestoreFixture();
            return $"Policy vote fixture preparation failed: {e.GetType().Name}: {e.Message}. {cleanup}";
        }
    }

    [CommandLineArgumentFunction("policy_vote_fixture_open", "coop.debug.kingdom")]
    public static string OpenPolicyScreen(List<string> args)
    {
        const string usage = "Usage: coop.debug.kingdom.policy_vote_fixture_open <kingdomId> <policyId>";
        if (!ModInformation.IsClient) return "This command can only be run on a client.";
        if (args.Count != 2) return usage;
        if (!TryGetFixtureObjects(args, out _, out PolicyObject policy, out string error)) return error;
        if (Game.Current?.GameStateManager == null) return "The game-state manager is unavailable.";
        if (Game.Current.GameStateManager.ActiveState is KingdomState) return "The Kingdom screen is already open.";

        KingdomState state = Game.Current.GameStateManager.CreateState<KingdomState>();
        state.InitialSelectedPolicy = policy;
        Game.Current.GameStateManager.PushState(state, 0);
        return "POLICY_VOTE_SCREEN_OPENING";
    }

    [CommandLineArgumentFunction("policy_vote_fixture_start", "coop.debug.kingdom")]
    public static string StartPolicyVote(List<string> args)
    {
        const string usage = "Usage: coop.debug.kingdom.policy_vote_fixture_start <kingdomId> <policyId>";
        if (!ModInformation.IsClient) return "This command can only be run on a client.";
        if (args.Count != 2) return usage;
        if (!TryGetFixtureObjects(args, out Kingdom kingdom, out PolicyObject policy, out string error)) return error;
        if (!(ScreenManager.TopScreen is GauntletKingdomScreen kingdomScreen) ||
            kingdomScreen.DataSource?.Policy == null)
        {
            return "The Kingdom policy screen is not ready.";
        }
        if (kingdom.UnresolvedDecisions.OfType<KingdomPolicyDecision>()
            .Any(decision => decision.Policy == policy))
        {
            return $"A {policy.StringId} decision already exists before the enact action.";
        }

        kingdomScreen.DataSource.Policy.SelectPolicy(policy);
        kingdomScreen.DataSource.Policy.ExecuteProposeOrDisavow();
        KingdomDecision currentDecision = kingdomScreen.DataSource.Decision?.CurrentDecision?.KingdomDecisionMaker?._decision;
        return currentDecision is KingdomPolicyDecision policyDecision && policyDecision.Policy == policy
            ? "POLICY_VOTE_STARTED"
            : "The policy enact action did not open the expected decision.";
    }

    [CommandLineArgumentFunction("policy_vote_fixture_select_enact", "coop.debug.kingdom")]
    public static string SelectEnact(List<string> args)
    {
        if (!ModInformation.IsClient) return "This command can only be run on a client.";
        if (args.Count != 0) return "Usage: coop.debug.kingdom.policy_vote_fixture_select_enact";
        DecisionItemBaseVM decisionItem = GetCurrentDecisionItem();
        if (decisionItem == null) return "No active kingdom decision item.";

        DecisionOptionVM enactOption = decisionItem.DecisionOptionsList.FirstOrDefault(option =>
            option.Option is KingdomPolicyDecision.PolicyDecisionOutcome outcome &&
            outcome.ShouldDecisionBeEnforced);
        if (enactOption == null) return "The policy decision has no enact option.";

        enactOption.ExecuteSelection();
        return decisionItem._currentSelectedOption == enactOption && decisionItem.CanEndDecision
            ? "POLICY_VOTE_ENACT_SELECTED"
            : "The policy enact option did not become finalizable.";
    }

    [CommandLineArgumentFunction("policy_vote_fixture_submit", "coop.debug.kingdom")]
    public static string SubmitFinalSelection(List<string> args)
    {
        if (!ModInformation.IsClient) return "This command can only be run on a client.";
        if (args.Count != 0) return "Usage: coop.debug.kingdom.policy_vote_fixture_submit";
        DecisionItemBaseVM decisionItem = GetCurrentDecisionItem();
        if (decisionItem == null || !decisionItem.CanEndDecision)
        {
            return "No finalizable kingdom decision item.";
        }

        decisionItem.ExecuteFinalSelection();
        return "POLICY_VOTE_FINAL_SELECTION_EXECUTED";
    }

    [CommandLineArgumentFunction("policy_vote_fixture_state", "coop.debug.kingdom")]
    public static string State(List<string> args)
    {
        const string usage = "Usage: coop.debug.kingdom.policy_vote_fixture_state <kingdomId> <policyId>";
        if (args.Count != 2) return usage;
        if (!ContainerProvider.TryResolve(out IObjectManager objectManager))
        {
            return "Unable to resolve ObjectManager.";
        }

        bool kingdomFound = objectManager.TryGetObject(args[0], out Kingdom kingdom);
        TryGetPolicy(objectManager, args[1], out PolicyObject policy);
        KingdomPolicyDecision policyDecision = kingdom?._unresolvedDecisions?
            .OfType<KingdomPolicyDecision>()
            .FirstOrDefault(decision => decision.Policy == policy);
        DecisionItemBaseVM decisionItem = GetCurrentDecisionItem();
        var kingdomScreen = ScreenManager.TopScreen as GauntletKingdomScreen;
        bool policyScreenReady = kingdomScreen?.DataSource?.Policy?.CurrentSelectedPolicy?.Policy == policy;
        bool canPropose = policyScreenReady && kingdomScreen.DataSource.Policy.CanProposeOrDisavowPolicy;
        bool selectedEnact = decisionItem?._currentSelectedOption?.Option is
            KingdomPolicyDecision.PolicyDecisionOutcome outcome && outcome.ShouldDecisionBeEnforced;
        string role = ModInformation.IsServer ? "server" : "client";
        string localKingdom = Clan.PlayerClan?.Kingdom?.StringId ?? "none";

        return $"POLICY_VOTE_FIXTURE_STATE role={role} kingdomFound={Bool(kingdomFound)} " +
               $"kingdomEliminated={Bool(kingdom?.IsEliminated ?? false)} localKingdom={localKingdom} " +
               $"policyActive={Bool(kingdom?.ActivePolicies?.Contains(policy) ?? false)} " +
               $"unresolvedPolicy={Bool(policyDecision != null)} screenActive={Bool(ScreenManager.TopScreen is GauntletKingdomScreen)} " +
               $"policyScreenReady={Bool(policyScreenReady)} canPropose={Bool(canPropose)} " +
               $"decisionActive={Bool(decisionItem?.IsActive ?? false)} canEnd={Bool(decisionItem?.CanEndDecision ?? false)} " +
               $"finalSelectionDone={Bool(decisionItem?._finalSelectionDone ?? false)} selectedEnact={Bool(selectedEnact)}";
    }

    [CommandLineArgumentFunction("policy_vote_fixture_close", "coop.debug.kingdom")]
    public static string ClosePolicyScreen(List<string> args)
    {
        if (!ModInformation.IsClient) return "This command can only be run on a client.";
        if (args.Count != 0) return "Usage: coop.debug.kingdom.policy_vote_fixture_close";
        if (!(Game.Current?.GameStateManager?.ActiveState is KingdomState))
        {
            return "No active Kingdom screen.";
        }

        Game.Current.GameStateManager.PopState(0);
        return "POLICY_VOTE_SCREEN_CLOSED";
    }

    [CommandLineArgumentFunction("policy_vote_fixture_restore", "coop.debug.kingdom")]
    public static string Restore(List<string> args)
    {
        if (!ModInformation.IsServer) return "This command can only be run on the server.";
        if (args.Count != 0) return "Usage: coop.debug.kingdom.policy_vote_fixture_restore";
        return RestoreFixture();
    }

    private static string RestoreFixture()
    {
        PolicyVoteFixture fixture = activeFixture;
        if (fixture == null) return "No policy vote fixture is active.";

        try
        {
            foreach (KingdomPolicyDecision decision in fixture.Kingdom._unresolvedDecisions
                         .OfType<KingdomPolicyDecision>()
                         .Where(candidate => candidate.Policy == fixture.Policy)
                         .ToList())
            {
                fixture.Kingdom.RemoveDecision(decision);
            }
            if (fixture.Kingdom.ActivePolicies.Contains(fixture.Policy))
            {
                fixture.Kingdom.RemovePolicy(fixture.Policy);
            }
            if (fixture.AiClan.Kingdom != fixture.AiOriginalKingdom)
            {
                ChangeKingdomAction.ApplyByJoinToKingdom(fixture.AiClan, fixture.AiOriginalKingdom);
            }
            if (fixture.PlayerClan.Kingdom == fixture.Kingdom)
            {
                fixture.KingdomMembershipState.MoveClanToKingdom(
                    fixture.Kingdom,
                    kingdom: null,
                    clan: fixture.PlayerClan,
                    publishCollectionChanges: true,
                    republishExistingCollections: true);
            }
            float influenceDelta = fixture.PlayerInfluence - fixture.PlayerClan.Influence;
            if (Math.Abs(influenceDelta) > 0.001f)
            {
                ChangeClanInfluenceAction.Apply(fixture.PlayerClan, influenceDelta);
            }
            if (fixture.Kingdom.Clans.Count != 0)
            {
                return $"Fixture kingdom still contains {fixture.Kingdom.Clans.Count} clans.";
            }

            MessageBroker.Instance.Publish(fixture.Kingdom, new DestroyKingdom(fixture.Kingdom));
            if (!fixture.Kingdom.IsEliminated)
            {
                DestroyKingdomAction.Apply(fixture.Kingdom);
            }

            activeFixture = null;
            return $"POLICY_VOTE_FIXTURE_RESTORED controller={fixture.ControllerId} " +
                   $"kingdom={fixture.KingdomId} aiKingdom={fixture.AiOriginalKingdom.StringId}";
        }
        catch (Exception e)
        {
            return $"Policy vote fixture restore failed: {e.GetType().Name}: {e.Message}";
        }
    }

    private static bool TryGetFixtureObjects(
        List<string> args,
        out Kingdom kingdom,
        out PolicyObject policy,
        out string error)
    {
        kingdom = null;
        policy = null;
        error = null;
        if (!ContainerProvider.TryResolve(out IObjectManager objectManager))
        {
            error = "Unable to resolve ObjectManager.";
            return false;
        }
        if (!objectManager.TryGetObject(args[0], out kingdom))
        {
            error = $"Unable to resolve kingdom {args[0]}.";
            return false;
        }
        if (!TryGetPolicy(objectManager, args[1], out policy))
        {
            error = $"Unable to resolve policy {args[1]}.";
            return false;
        }
        return true;
    }

    private static bool TryGetPolicy(IObjectManager objectManager, string id, out PolicyObject policy)
    {
        if (objectManager.TryGetObject(id, out policy)) return true;
        policy = PolicyObject.All.FirstOrDefault(candidate => candidate.StringId == id);
        return policy != null;
    }

    private static bool TryGetClan(IObjectManager objectManager, string id, out Clan clan)
    {
        if (objectManager.TryGetObject(id, out clan)) return true;
        clan = Clan.All.FirstOrDefault(candidate => candidate.StringId == id);
        return clan != null;
    }

    private static DecisionItemBaseVM GetCurrentDecisionItem()
    {
        return (ScreenManager.TopScreen as GauntletKingdomScreen)?.DataSource?.Decision?.CurrentDecision;
    }

    private static string Bool(bool value)
    {
        return value ? "true" : "false";
    }

    private sealed class PolicyVoteFixture
    {
        public string ControllerId { get; }
        public Kingdom Kingdom { get; }
        public string KingdomId { get; }
        public Clan PlayerClan { get; }
        public float PlayerInfluence { get; }
        public PolicyObject Policy { get; }
        public Clan AiClan { get; }
        public Kingdom AiOriginalKingdom { get; }
        public IKingdomMembershipState KingdomMembershipState { get; }

        public PolicyVoteFixture(
            string controllerId,
            Kingdom kingdom,
            string kingdomId,
            Clan playerClan,
            float playerInfluence,
            PolicyObject policy,
            Clan aiClan,
            Kingdom aiOriginalKingdom,
            IKingdomMembershipState kingdomMembershipState)
        {
            ControllerId = controllerId;
            Kingdom = kingdom;
            KingdomId = kingdomId;
            PlayerClan = playerClan;
            PlayerInfluence = playerInfluence;
            Policy = policy;
            AiClan = aiClan;
            AiOriginalKingdom = aiOriginalKingdom;
            KingdomMembershipState = kingdomMembershipState;
        }
    }
}
#endif
