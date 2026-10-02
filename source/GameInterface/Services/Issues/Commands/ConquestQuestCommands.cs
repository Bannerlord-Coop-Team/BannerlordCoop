#if DEBUG
using Common;
using Common.Commands;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsToOffloadStolenGoods;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Utils.Commands;
using Newtonsoft.Json;
using SandBox.GauntletUI;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.Issues.Commands;

using Issue = TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue;
using Quest = TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest;

public class ConquestQuestCommands
{
    public sealed class Journal : ICoopCommand
    {
        public string Prefix => "coop.debug.conquest";
        public string Name => "journal";
        public string Description => "Open, read or close the client's real quest journal, optionally selecting a visible conquest quest id.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("action", "open, read or close.", isRequired: true),
            new ExpectedArgs("quest_id", "An active or completed conquest quest id visible to this client.", isRequired: false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer || Campaign.Current == null || args.Count == 0)
                return new CoopCommandResult(false, "A client campaign and journal action are required.", "command_failed");
            var states = Game.Current?.GameStateManager;
            var screen = ScreenManager.TopScreen as GauntletQuestsScreen;
            switch (args[0])
            {
                case "open":
                    if (states?.ActiveState is not MapState && states?.ActiveState is not QuestsState)
                        return new CoopCommandResult(false, "Return to the campaign map before opening the journal.", "command_failed");
                    var id = args.Count > 1 ? args[1] : null;
                    var quest = Campaign.Current.QuestManager.Quests.OfType<Quest>().FirstOrDefault(item => item.StringId == id);
                    var log = Campaign.Current.LogEntryHistory.GameActionLogs.OfType<JournalLogEntry>()
                        .FirstOrDefault(item => item.Title.GetID() == "mvzh0HVk" && item.IsEnded() && item._relatedObjectIds.Contains(id));
                    if (id != null && quest == null && log == null)
                        return new CoopCommandResult(false, "That conquest quest is not visible in this client's journal.", "command_failed");
                    if (states.ActiveState is MapState)
                    {
                        var state = quest != null ? states.CreateState<QuestsState>(quest)
                            : log != null ? states.CreateState<QuestsState>(log) : states.CreateState<QuestsState>();
                        states.PushState(state);
                    }
                    else if (screen?._dataSource != null)
                    {
                        if (quest != null) screen._dataSource.SetSelectedQuest(quest);
                        if (log != null) screen._dataSource.SetSelectedLog(log);
                    }
                    else return new CoopCommandResult(false, "The quest screen is not ready.", "command_failed");
                    return new CoopCommandResult(true, "Quest journal requested; read it after the next rendered frame.");
                case "read":
                    if (states?.ActiveState is not QuestsState || screen?._dataSource == null)
                        return new CoopCommandResult(false, "The quest journal is not open.", "command_failed");
                    var journal = screen._dataSource;
                    return new CoopCommandResult(true, JsonConvert.SerializeObject(new
                    {
                        screen = screen.GetType().Name, title = journal.CurrentQuestTitle,
                        selectedQuest = journal.SelectedQuest?.Quest?.StringId,
                        selectedHistoryIds = journal.SelectedQuest?.QuestLogEntry?._relatedObjectIds,
                        active = journal.ActiveQuestsList.Select(item => new { id = item.Quest?.StringId, item.Name, item.IsSelected }).ToArray(),
                        completed = journal.OldQuestsList.Select(item => new { ids = item.QuestLogEntry?._relatedObjectIds, item.Name, item.IsSelected }).ToArray(),
                    }));
                case "close":
                    if (states?.ActiveState is not QuestsState || screen?._dataSource == null)
                        return new CoopCommandResult(false, "The quest journal is not open.", "command_failed");
                    screen._dataSource.ExecuteClose();
                    return new CoopCommandResult(true, "Quest journal closed.");
                default:
                    return new CoopCommandResult(false, "Use open, read or close.", "command_failed");
            }
        }
    }

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

    public sealed class RequestAccept : ICoopCommand
    {
        public string Prefix => "coop.debug.conquest";
        public string Name => "request_accept";
        public string Description => "Send one ordinary client acceptance request with the previously observed conquest generation.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("giver_id", "The giver hero registry id.", isRequired: true),
            new ExpectedArgs("generation", "The generation retained from conquest.read before this request.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer || !int.TryParse(args[1], out var generation) || generation < 0)
                return new CoopCommandResult(false, "A client and nonnegative observed generation are required.", "command_failed");
            if (!CommandHelpers.TryGetObjectManager(out var objects, out var error) ||
                !CommandHelpers.TryGetManagedObject<Hero>(objects, args[0], out var giver, out error))
                return new CoopCommandResult(false, error, "command_failed");
            if (giver.Issue is not Issue || !ContainerProvider.TryResolve<INetwork>(out var network))
                return new CoopCommandResult(false, "The conquest issue and client network are required.", "command_failed");
            network.SendAll(new RequestQuestTypeAcceptQuest(args[0], generation));
            return new CoopCommandResult(true, JsonConvert.SerializeObject(new { requested = true, giverId = args[0], generation }));
        }
    }

    public sealed class Eligibility : ICoopCommand
    {
        public string Prefix => "coop.debug.conquest";
        public string Name => "eligibility";
        public string Description => "Evaluate native acceptance conditions for a registered player; expired offers may be canceled by vanilla.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("giver_id", "The giver hero registry id.", isRequired: true),
            new ExpectedArgs("controller_id", "The registered player's controller id.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.conquest.eligibility"))
                return new CoopCommandResult(false, error, "command_failed");
            if (!CommandHelpers.TryGetObjectManager(out var objects, out error) ||
                !CommandHelpers.TryGetManagedObject<Hero>(objects, args[0], out var giver, out error))
                return new CoopCommandResult(false, error, "command_failed");
            if (giver.Issue is not Issue issue || !issue.IsOngoingWithoutQuest ||
                !ContainerProvider.TryResolve<IPlayerManager>(out var players) ||
                !players.TryGetPlayer(args[1], out var player) ||
                !objects.TryGetObject<Hero>(player.HeroId, out var hero) ||
                !objects.TryGetObject<MobileParty>(player.MobilePartyId, out var party))
                return new CoopCommandResult(false, "An unaccepted conquest offer and a registered player hero and party are required.", "command_failed");
            using (new MainHeroSubstitutionScope(hero, party))
            {
                var allowed = issue.CheckPreconditions(giver, out var explanation);
                return new CoopCommandResult(true, JsonConvert.SerializeObject(new
                {
                    player.ControllerId, player.HeroId, player.MobilePartyId, allowed,
                    explanation = explanation.ToString(), issue.IsOngoingWithoutQuest,
                }));
            }
        }
    }

    public sealed class Read : ICoopCommand
    {
        public string Prefix => "coop.debug.conquest";
        public string Name => "read";
        public string Description => "Read conquest identities, personal journal visibility, rewards and fortification state.";
        public CoopCommandSide Side => CoopCommandSide.Both;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("giver_id", "The giver hero registry id.", isRequired: true),
            new ExpectedArgs("quest_id", "Optional retained quest id, including a completed quest from an earlier issue.", isRequired: false),
        };

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
            var manager = Campaign.Current.QuestManager;
            var queriedQuestId = args.Count > 1 ? args[1] : issue?.IssueQuest?.StringId;
            var quest = manager._quests.OfType<Quest>().FirstOrDefault(item => item.StringId == queriedQuestId && item.QuestGiver == giver);
            ownership.TryGetQuestOwner(queriedQuestId, out var queriedQuestOwner);
            var target = quest?._targetSettlement ?? (args.Count == 1 ? issue?._targetSettlement : null);
            objects.TryGetId(target, out var targetId);
            objects.TryGetId(giver.MapFaction, out var giverFactionId);
            var playerRows = players.Players.Select(player =>
            {
                objects.TryGetObject<Hero>(player.HeroId, out var hero);
                objects.TryGetObject<MobileParty>(player.MobilePartyId, out var party);
                float? honorXp = hero != null && GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress.TryGet(hero, out var progress)
                    ? progress.GetPropertyValue(DefaultTraits.Honor) : null;
                Hero relationActor = null;
                Hero relationTarget = null;
                if (hero != null) Campaign.Current.Models.DiplomacyModel.GetHeroesForEffectiveRelation(hero, giver, out relationActor, out relationTarget);
                float? giverFactionCrimeRating = null;
                if (giverFactionId != null)
                {
                    player.CrimeRatings.TryGetValue(giverFactionId, out var rating);
                    giverFactionCrimeRating = rating;
                }
                return new { player.ControllerId, player.HeroId, player.MobilePartyId, registeredHero = hero != null,
                    registeredParty = party != null, gold = hero?.Gold, relation = hero?.GetRelation(giver),
                    renown = hero?.Clan?.Renown, influence = hero?.Clan?.Influence,
                    honor = hero?.GetTraitLevel(DefaultTraits.Honor), honorXp, prisoner = hero?.IsPrisoner,
                    partyActive = party?.IsActive, clanId = hero?.Clan?.StringId,
                    relationActor = relationActor?.StringId, relationTarget = relationTarget?.StringId,
                    charm = hero?.GetSkillValue(DefaultSkills.Charm), oratory = hero?.GetPerkValue(DefaultPerks.Charm.Oratory),
                    female = hero?.IsFemale, mercy = hero?.GetTraitLevel(DefaultTraits.Mercy),
                    inBloom = hero?.GetPerkValue(DefaultPerks.Charm.InBloom),
                    youngAndRespectful = hero?.GetPerkValue(DefaultPerks.Charm.YoungAndRespectful),
                    goodNatured = hero?.GetPerkValue(DefaultPerks.Charm.GoodNatured),
                    tribute = hero?.GetPerkValue(DefaultPerks.Charm.Tribute), giverFactionCrimeRating,
                    members = party?.MemberRoster.GetTroopRoster().Select(item => new
                        { id = item.Character.StringId, item.Number, item.WoundedNumber, item.Xp }).ToArray(),
                    prisoners = party?.PrisonRoster.GetTroopRoster().Select(item => new
                        { id = item.Character.StringId, item.Number, item.WoundedNumber }).ToArray(),
                    inventory = party?.ItemRoster.Select(item => new
                        { id = item.EquipmentElement.Item?.StringId, modifier = item.EquipmentElement.ItemModifier?.StringId, item.Amount }).ToArray() };
            }).ToArray();
            var history = Campaign.Current.LogEntryHistory.GameActionLogs.OfType<JournalLogEntry>()
                .Where(entry => entry.RelatedHero == giver && entry.Title.GetID() == "mvzh0HVk" &&
                    (args.Count == 1 || entry._relatedObjectIds.Contains(queriedQuestId)))
                .Select(entry => new { ids = entry._relatedObjectIds, visibleCompleted = entry.IsEnded(),
                    completion = entry._questCompletionDetail.ToString(), logs = entry.GetEntries().Select(log =>
                        new { text = log.LogText.ToString(), time = log.LogTime.NumTicks }).ToArray() }).ToArray();
            var hasMapPoint = giver.GetMapPoint() != null;
            float? distanceThreshold = hasMapPoint
                ? Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.Default) * 2f : null;
            var distances = hasMapPoint ? Campaign.Current.AllTowns.Concat(Campaign.Current.AllCastles).Select(town =>
            {
                var position = giver.GetCampaignPosition();
                var distance = Campaign.Current.Models.MapDistanceModel.GetDistance(town.Settlement, in position, false, MobileParty.NavigationType.Default);
                objects.TryGetId(town.Settlement, out var id);
                return new { id, town.Settlement.StringId, distance, enemy = town.MapFaction.IsAtWarWith(giver.MapFaction) };
            }).ToArray() : null;
            return new CoopCommandResult(true, JsonConvert.SerializeObject(new
            {
                localController = local.ControllerId, owner, generation, issueId = issue?.StringId, targetId,
                queriedQuestId, queriedQuestOwner, questObjectPresent = quest != null,
                questId = quest?.StringId, ongoing = quest?.IsOngoing, dueTime = quest?.QuestDueTime.NumTicks,
                giverId = giver.StringId, giverRegistryId = args[0], giverPower = giver.Power, giverAlive = giver.IsAlive,
                giverPrisoner = giver.IsPrisoner, giverClan = giver.Clan?.StringId, giverFaction = giver.MapFaction?.StringId,
                giverFemale = giver.IsFemale, giverMercy = giver.GetTraitLevel(DefaultTraits.Mercy),
                giverOccupation = giver.Occupation.ToString(), giver.IsFactionLeader, hasMapPoint, distanceThreshold, distances,
                crimeWarThreshold = Campaign.Current.Models.CrimeModel.DeclareWarCrimeRatingThreshold,
                issueCreated = issue?.IssueCreationTime.NumTicks, issueDue = issue?.IssueDueTime.NumTicks,
                nextIssueIndex = Campaign.Current.IssueManager._nextIssueUniqueIndex,
                hasIssueCooldown = Campaign.Current.IssueManager.HasIssueCoolDown(typeof(Issue), giver),
                currentTime = CampaignTime.Now.NumTicks, tasks = quest?.TaskList.Count,
                trackEnabled = quest?.IsTrackEnabled, hasDiscussion = quest?.IsThereDiscussDialogFlow,
                mirroredQuests = manager._quests.OfType<Quest>().Select(item => item.StringId).ToArray(),
                visibleQuests = manager.Quests.OfType<Quest>().Select(item => item.StringId).ToArray(),
                trackedQuests = manager.TrackedObjects.Values.SelectMany(items => items).OfType<Quest>()
                    .Select(item => item.StringId).Distinct().ToArray(),
                trackedObjects = manager._trackedObjects.Select(entry =>
                {
                    objects.TryGetId(entry.Key, out var id);
                    return new { id, type = entry.Key.GetType().Name, quests = entry.Value.Select(item => item.StringId).ToArray() };
                }).ToArray(),
                logs = quest?.JournalEntries.Select(log => new { text = log.LogText.ToString(), time = log.LogTime.NumTicks }).ToArray(),
                history, players = playerRows,
                fortifications = giver.Clan?.Settlements.Where(settlement => settlement.IsFortification)
                    .Select(settlement =>
                    {
                        var town = settlement.Town;
                        var security = Campaign.Current.Models.SettlementSecurityModel.CalculateSecurityChange(town, true);
                        var loyalty = Campaign.Current.Models.SettlementLoyaltyModel.CalculateLoyaltyChange(town, true);
                        objects.TryGetId(settlement, out var id);
                        return new { id, settlement.StringId, town.Security, town.Loyalty,
                            securityChange = security.ResultNumber, loyaltyChange = loyalty.ResultNumber,
                            securityInputs = security.GetLines().Select(line => new { line.name, line.number }).ToArray(),
                            loyaltyInputs = loyalty.GetLines().Select(line => new { line.name, line.number }).ToArray() };
                    }).ToArray(),
            }));
        }
    }
}
#endif
