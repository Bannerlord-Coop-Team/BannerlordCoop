#if DEBUG
using Common;
using Common.Commands;
using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.Commands;
using Newtonsoft.Json;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Commands;

// Temporary #3725 personal-quest isolation diagnostics, excluded from the product fix.
public static class QuestIsolationFixtureCommands
{
    private const string CommandPrefix = "coop.debug.issues";

    private static CoopCommandResult Succeeded(object value) =>
        new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(value));

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    private static string IdOf(IObjectManager objectManager, object obj) =>
        obj != null && objectManager.TryGetId(obj, out string id) ? id : null;

    private static GangLeaderNeedsToOffloadStolenGoodsIssueBehavior Behavior() =>
        Campaign.Current?.GetCampaignBehavior<GangLeaderNeedsToOffloadStolenGoodsIssueBehavior>();

    public sealed class CandidatesCoopCommand : ICoopCommand
    {
        private readonly IObjectManager objectManager;

        public CandidatesCoopCommand(IObjectManager objectManager)
        {
            this.objectManager = objectManager;
        }

        public string Prefix => CommandPrefix;
        public string Name => "isolation_candidates";
        public string Description => "Lists gang leaders that vanilla would offer the stolen-goods issue to.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.issues.isolation_candidates")) return Failed(error);

            var behavior = Behavior();
            if (behavior == null) return Failed("GangLeaderNeedsToOffloadStolenGoodsIssueBehavior is not registered.");

            var candidates = Hero.AllAliveHeroes
                .Where(hero => hero.IsGangLeader && hero.Issue == null && hero.CurrentSettlement?.IsTown == true)
                .Select(hero => behavior.ConditionsHold(hero, out var hideout) ? new
                {
                    heroId = IdOf(objectManager, hero),
                    name = hero.Name?.ToString(),
                    settlementId = IdOf(objectManager, hero.CurrentSettlement),
                    settlementStringId = hero.CurrentSettlement.StringId,
                    settlementName = hero.CurrentSettlement.Name?.ToString(),
                    hideoutId = IdOf(objectManager, hideout),
                    hideoutStringId = hideout?.StringId,
                } : null)
                .Where(candidate => candidate?.heroId != null && candidate.settlementId != null && candidate.hideoutId != null)
                .ToArray();

            return Succeeded(new { candidates });
        }
    }

    public sealed class CreateCoopCommand : ICoopCommand
    {
        private readonly IObjectManager objectManager;
        private readonly IIssueOwnershipRegistry ownershipRegistry;
        private readonly IIssueGenerationRegistry generationRegistry;
        private readonly IIssueConversationTracker conversationTracker;
        private readonly IControllerIdProvider controllerIdProvider;

        public CreateCoopCommand(
            IObjectManager objectManager,
            IIssueOwnershipRegistry ownershipRegistry,
            IIssueGenerationRegistry generationRegistry,
            IIssueConversationTracker conversationTracker,
            IControllerIdProvider controllerIdProvider)
        {
            this.objectManager = objectManager;
            this.ownershipRegistry = ownershipRegistry;
            this.generationRegistry = generationRegistry;
            this.conversationTracker = conversationTracker;
            this.controllerIdProvider = controllerIdProvider;
        }

        public string Prefix => CommandPrefix;
        public string Name => "isolation_create";
        public string Description => "Creates the stolen-goods issue through vanilla's own selection, without starting the quest.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered gang leader hero id.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.issues.isolation_create")) return Failed(error);
            if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, args[0], out var hero, out error)) return Failed(error);
            if (hero.Issue != null) return Failed($"Hero '{hero.Name}' already has issue {hero.Issue.GetType().Name}.");

            var behavior = Behavior();
            if (behavior == null) return Failed("GangLeaderNeedsToOffloadStolenGoodsIssueBehavior is not registered.");
            if (!behavior.ConditionsHold(hero, out var hideout)) return Failed($"Vanilla conditions do not hold for hero '{hero.Name}'.");

            var pid = new PotentialIssueData(behavior.OnSelected,
                typeof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue),
                IssueBase.IssueFrequency.Common, hideout);
            if (!Campaign.Current.IssueManager.CreateNewIssue(in pid, hero)) return Failed("IssueManager.CreateNewIssue returned false.");

            return Succeeded(Snapshot(hero, objectManager, ownershipRegistry, generationRegistry, conversationTracker, controllerIdProvider));
        }
    }

    public sealed class OpenConversationCoopCommand : ICoopCommand
    {
        private readonly IObjectManager objectManager;
        private readonly IControllerIdProvider controllerIdProvider;

        public OpenConversationCoopCommand(IObjectManager objectManager, IControllerIdProvider controllerIdProvider)
        {
            this.objectManager = objectManager;
            this.controllerIdProvider = controllerIdProvider;
        }

        public string Prefix => CommandPrefix;
        public string Name => "isolation_open_conversation";
        public string Description => "Publishes the issue conversation-opened event that BeginConversation publishes.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered issue giver hero id.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer) return Failed("coop.debug.issues.isolation_open_conversation is client-only.");
            if (!objectManager.TryGetObject<Hero>(args[0], out var hero)) return Failed($"Unknown hero id '{args[0]}'.");
            if (hero.Issue == null) return Failed($"Hero '{hero.Name}' has no issue.");

            // Same event IssueConversationOpenedPatch publishes; the server still applies its presence and generation checks.
            MessageBroker.Instance.Publish(hero, new IssueConversationOpenedLocally(hero, controllerIdProvider.ControllerId));
            return Succeeded(new { published = true, heroId = args[0], controllerId = controllerIdProvider.ControllerId });
        }
    }

    public sealed class AcceptCoopCommand : ICoopCommand
    {
        private readonly IObjectManager objectManager;
        private readonly IIssueConversationTracker conversationTracker;
        private readonly IControllerIdProvider controllerIdProvider;

        public AcceptCoopCommand(
            IObjectManager objectManager,
            IIssueConversationTracker conversationTracker,
            IControllerIdProvider controllerIdProvider)
        {
            this.objectManager = objectManager;
            this.conversationTracker = conversationTracker;
            this.controllerIdProvider = controllerIdProvider;
        }

        public string Prefix => CommandPrefix;
        public string Name => "isolation_accept";
        public string Description => "Accepts the quest solution through IssueManager.StartIssueQuest, as the dialog consequence does.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered issue giver hero id.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer) return Failed("coop.debug.issues.isolation_accept is client-only.");
            if (!objectManager.TryGetObject<Hero>(args[0], out var hero)) return Failed($"Unknown hero id '{args[0]}'.");
            if (!conversationTracker.TryGetTrackedRequester(args[0], controllerIdProvider.ControllerId, out var generation))
                return Failed("The server has not allowed a tracked conversation with this issue giver yet.");

            bool requested = Campaign.Current.IssueManager.StartIssueQuest(hero);
            return Succeeded(new { requested, generation, heroId = args[0], controllerId = controllerIdProvider.ControllerId });
        }
    }

    public sealed class StateCoopCommand : ICoopCommand
    {
        private readonly IObjectManager objectManager;
        private readonly IIssueOwnershipRegistry ownershipRegistry;
        private readonly IIssueGenerationRegistry generationRegistry;
        private readonly IIssueConversationTracker conversationTracker;
        private readonly IControllerIdProvider controllerIdProvider;

        public StateCoopCommand(
            IObjectManager objectManager,
            IIssueOwnershipRegistry ownershipRegistry,
            IIssueGenerationRegistry generationRegistry,
            IIssueConversationTracker conversationTracker,
            IControllerIdProvider controllerIdProvider)
        {
            this.objectManager = objectManager;
            this.ownershipRegistry = ownershipRegistry;
            this.generationRegistry = generationRegistry;
            this.conversationTracker = conversationTracker;
            this.controllerIdProvider = controllerIdProvider;
        }

        public string Prefix => CommandPrefix;
        public string Name => "isolation_state";
        public string Description => "Reads the issue, quest, owner and journal state for one issue giver.";
        public CoopCommandSide Side => CoopCommandSide.Both;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered issue giver hero id.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!objectManager.TryGetObject<Hero>(args[0], out var hero)) return Failed($"Unknown hero id '{args[0]}'.");

            return Succeeded(Snapshot(hero, objectManager, ownershipRegistry, generationRegistry, conversationTracker, controllerIdProvider));
        }
    }

    internal static object Snapshot(
        Hero hero,
        IObjectManager objectManager,
        IIssueOwnershipRegistry ownershipRegistry,
        IIssueGenerationRegistry generationRegistry,
        IIssueConversationTracker conversationTracker,
        IControllerIdProvider controllerIdProvider)
    {
        var heroId = IdOf(objectManager, hero);
        var issue = hero.Issue;
        var quest = issue?.IssueQuest;
        var stolenGoods = quest as GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest;
        var controllerId = controllerIdProvider?.ControllerId;
        bool hasOwner = ownershipRegistry.TryGetOwnerControllerId(hero, out var ownerControllerId);
        bool hasGeneration = generationRegistry.TryGetGeneration(hero, out var generation);
        int conversationGeneration = 0;
        bool hasConversation = controllerId != null && heroId != null &&
            conversationTracker.TryGetTrackedRequester(heroId, controllerId, out conversationGeneration);

        return new
        {
            side = ModInformation.IsServer ? "server" : "client",
            controllerId,
            heroId,
            heroName = hero.Name?.ToString(),
            settlementId = IdOf(objectManager, hero.CurrentSettlement),
            mainPartySettlementStringId = MobileParty.MainParty?.CurrentSettlement?.StringId,
            issue = issue == null ? null : new
            {
                type = issue.GetType().Name,
                stringId = issue.StringId,
                ongoingWithoutQuest = issue.IsOngoingWithoutQuest,
                solvingWithQuest = issue.IsSolvingWithQuest,
                solvingWithAlternative = issue.IsSolvingWithAlternative,
                dueHours = issue.IssueDueTime.ToHours,
                generation = hasGeneration ? generation : (int?)null,
            },
            owner = new
            {
                controllerId = hasOwner ? ownerControllerId : null,
                isLocalPeerOwner = ownershipRegistry.IsLocalPeerOwner(hero),
            },
            conversationGeneration = hasConversation ? conversationGeneration : (int?)null,
            quest = quest == null ? null : new
            {
                type = quest.GetType().Name,
                stringId = quest.StringId,
                ongoing = quest.IsOngoing,
                finalized = quest.IsFinalized,
                dueHours = quest.QuestDueTime.ToHours,
                journal = quest.JournalEntries.Select(entry => new
                {
                    hours = entry.LogTime.ToHours,
                    text = entry.LogText?.ToString(),
                }).ToArray(),
            },
            stolenGoods = stolenGoods == null ? null : new
            {
                isPayingForGoods = stolenGoods._isPayingForGoods,
                isFightingForGoods = stolenGoods._isFightingForGoods,
                playerHasTheGoods = stolenGoods._playerHasTheGoods,
                talkedWithBanditLeader = stolenGoods._talkedWithBanditLeader,
                counterOfferGiven = stolenGoods._counterOfferGiven,
                stolenTradeGoodAmount = stolenGoods._stolenTradeGoodAmount,
                stolenTradeGoodPrice = stolenGoods._stolenTradeGoodPrice,
                rewardGold = stolenGoods.RewardGold,
            },
        };
    }
}
#endif
