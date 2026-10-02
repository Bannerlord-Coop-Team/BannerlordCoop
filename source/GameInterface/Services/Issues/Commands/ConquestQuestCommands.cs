#if DEBUG
using Common.Commands;
using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsToOffloadStolenGoods;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Utils.Commands;
using Newtonsoft.Json;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Commands;

using Issue = TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue;
using Quest = TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest;

public class ConquestQuestCommands
{
    public sealed class Offer : ICoopCommand
    {
        public string Prefix => "coop.debug.conquest";
        public string Name => "offer";
        public string Description => "Evaluate the native conquest offer and create its selected issue without accepting it.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[] { new ExpectedArgs("giver_id", "The giver hero registry id.", isRequired: true) };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.conquest.offer"))
                return new CoopCommandResult(false, error, "command_failed");
            if (!CommandHelpers.TryGetObjectManager(out var objects, out error) ||
                !CommandHelpers.TryGetManagedObject<Hero>(objects, args[0], out var giver, out error))
                return new CoopCommandResult(false, error, "command_failed");
            if (giver.Issue != null) return new CoopCommandResult(false, "The giver already has an issue.", "command_failed");
            var behavior = Campaign.Current.GetCampaignBehavior<TheConquestOfSettlementIssueBehavior>();
            if (behavior == null) return new CoopCommandResult(false, "The conquest behavior is unavailable.", "command_failed");
            var eligible = behavior.ConditionsHold(giver, out var target);
            var created = false;
            if (eligible)
            {
                var potential = new PotentialIssueData(behavior.OnStartIssue, typeof(Issue), IssueBase.IssueFrequency.VeryCommon, target);
                created = Campaign.Current.IssueManager.CreateNewIssue(in potential, giver);
            }
            objects.TryGetId(target, out var targetId);
            return new CoopCommandResult(true, JsonConvert.SerializeObject(new { eligible, created, targetId, issueId = giver.Issue?.StringId }));
        }
    }

    public sealed class Accept : ICoopCommand
    {
        public string Prefix => "coop.debug.conquest";
        public string Name => "accept";
        public string Description => "Run the production quest acceptance handler for a registered player.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("giver_id", "The giver hero registry id.", isRequired: true),
            new ExpectedArgs("controller_id", "The accepting player's registered controller id.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.conquest.accept"))
                return new CoopCommandResult(false, error, "command_failed");
            if (!CommandHelpers.TryGetObjectManager(out var objects, out error) ||
                !CommandHelpers.TryGetManagedObject<Hero>(objects, args[0], out var giver, out error))
                return new CoopCommandResult(false, error, "command_failed");
            if (giver.Issue is not Issue || !ContainerProvider.TryResolve<IPlayerManager>(out var players) ||
                !players.TryGetPlayer(args[1], out _) || !ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownership))
                return new CoopCommandResult(false, "The conquest issue or registered player is unavailable.", "command_failed");
            MessageBroker.Instance.Publish(giver, new QuestTypeQuestSolutionAcceptTriggered(giver, args[1]));
            ownership.TryGetOwnerControllerId(giver, out var owner);
            var accepted = giver.Issue?.IssueQuest is Quest quest && quest.JournalEntries.Count != 0 && owner == args[1];
            return new CoopCommandResult(true, JsonConvert.SerializeObject(new { accepted, owner, questId = giver.Issue?.IssueQuest?.StringId }));
        }
    }

    public sealed class Read : ICoopCommand
    {
        public string Prefix => "coop.debug.conquest";
        public string Name => "read";
        public string Description => "Read conquest identities, personal journal visibility, rewards and fortification state.";
        public CoopCommandSide Side => CoopCommandSide.Both;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[] { new ExpectedArgs("giver_id", "The giver hero registry id.", isRequired: true) };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!CommandHelpers.TryGetObjectManager(out var objects, out var error) ||
                !CommandHelpers.TryGetManagedObject<Hero>(objects, args[0], out var giver, out error))
                return new CoopCommandResult(false, error, "command_failed");
            if (!ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownership) ||
                !ContainerProvider.TryResolve<IIssueGenerationRegistry>(out var generations) ||
                !ContainerProvider.TryResolve<IControllerIdProvider>(out var local) ||
                !ContainerProvider.TryResolve<IPlayerManager>(out var players))
                return new CoopCommandResult(false, "Quest services are unavailable.", "command_failed");
            ownership.TryGetOwnerControllerId(giver, out var owner);
            generations.TryGetGeneration(giver, out var generation);
            var issue = giver.Issue as Issue;
            var quest = issue?.IssueQuest as Quest;
            objects.TryGetId(issue?._targetSettlement, out var targetId);
            var manager = Campaign.Current.QuestManager;
            var playerRows = players.Players.Select(player =>
            {
                objects.TryGetObject<Hero>(player.HeroId, out var hero);
                objects.TryGetObject<MobileParty>(player.MobilePartyId, out var party);
                float? honorXp = hero != null && GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress.TryGet(hero, out var progress)
                    ? progress.GetPropertyValue(DefaultTraits.Honor) : null;
                return new { player.ControllerId, player.HeroId, player.MobilePartyId, registeredHero = hero != null,
                    registeredParty = party != null, gold = hero?.Gold, relation = hero?.GetRelation(giver),
                    renown = hero?.Clan?.Renown, influence = hero?.Clan?.Influence,
                    honor = hero?.GetTraitLevel(DefaultTraits.Honor), honorXp, prisoner = hero?.IsPrisoner,
                    partyActive = party?.IsActive };
            }).ToArray();
            var history = Campaign.Current.LogEntryHistory.GameActionLogs.OfType<JournalLogEntry>()
                .Where(entry => entry.RelatedHero == giver && entry.Title.GetID() == "mvzh0HVk")
                .Select(entry => new { ids = entry._relatedObjectIds, visibleCompleted = entry.IsEnded(),
                    completion = entry._questCompletionDetail.ToString(), logs = entry.GetEntries().Select(log =>
                        new { text = log.LogText.ToString(), time = log.LogTime.NumTicks }).ToArray() }).ToArray();
            return new CoopCommandResult(true, JsonConvert.SerializeObject(new
            {
                localController = local.ControllerId, owner, generation, issueId = issue?.StringId, targetId,
                questId = quest?.StringId, ongoing = quest?.IsOngoing, dueTime = quest?.QuestDueTime.NumTicks,
                visibleQuests = manager.Quests.OfType<Quest>().Select(item => item.StringId).ToArray(),
                trackedQuests = manager.TrackedObjects.Values.SelectMany(items => items).OfType<Quest>()
                    .Select(item => item.StringId).Distinct().ToArray(),
                logs = quest?.JournalEntries.Select(log => new { text = log.LogText.ToString(), time = log.LogTime.NumTicks }).ToArray(),
                history, players = playerRows,
                fortifications = giver.Clan?.Settlements.Where(settlement => settlement.IsFortification)
                    .Select(settlement => new { settlement.StringId, settlement.Town.Security, settlement.Town.Loyalty }).ToArray(),
            }));
        }
    }
}
#endif
