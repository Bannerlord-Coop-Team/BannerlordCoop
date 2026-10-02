using Common.Messaging;
using Common;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Generic.Dispatch;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Issues.Handlers;
using GameInterface.Services.ObjectManager;
using ProtoBuf;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Generic.Migrated.Smugglers;

using Issue = SmugglersIssueBehavior.SmugglersIssue;
using Quest = SmugglersIssueBehavior.SmugglersIssueQuest;

[ProtoContract(SkipConstructor = true)]
internal readonly struct SmugglersQuestAcceptFields
{
    [ProtoMember(1)]
    public readonly string QuestId;
    [ProtoMember(2)]
    public readonly string PartyId;
    [ProtoMember(3)]
    public readonly float Difficulty;
    [ProtoMember(4)]
    public readonly int RewardGold;
    [ProtoMember(5)]
    public readonly long DueTimeTicks;
    [ProtoMember(6)]
    public readonly string PlayerHeroId;
    [ProtoMember(7)]
    public readonly long StartLogTimeTicks;

    public SmugglersQuestAcceptFields(string questId, string partyId, float difficulty, int rewardGold, long dueTimeTicks, string playerHeroId, long startLogTimeTicks)
    {
        QuestId = questId;
        PartyId = partyId;
        Difficulty = difficulty;
        RewardGold = rewardGold;
        DueTimeTicks = dueTimeTicks;
        PlayerHeroId = playerHeroId;
        StartLogTimeTicks = startLogTimeTicks;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct SmugglersAlternativeAcceptFields
{
    [ProtoMember(1)]
    public readonly float Difficulty;
    [ProtoMember(2)]
    public readonly AlternativeSolutionVanillaState State;
    [ProtoMember(3)]
    public readonly string PlayerHeroId;
    [ProtoMember(4)]
    public readonly SmugglersJournalEntry StartLog;

    public SmugglersAlternativeAcceptFields(float difficulty, AlternativeSolutionVanillaState state, string playerHeroId, SmugglersJournalEntry startLog)
    {
        Difficulty = difficulty;
        State = state;
        PlayerHeroId = playerHeroId;
        StartLog = startLog;
    }
}

[QuestTypeModule]
internal static class SmugglersQuestType
{
    internal const byte PersuasionProof = 1;
    internal const byte BribeProof = 2;
    private static readonly ConditionalWeakTable<Quest, object> ObservedSuccess = new();

    internal static void RequestSuccess(Quest quest, byte proof)
    {
        if (!quest.IsOngoing) return;
        if (!ContainerProvider.TryResolve<ISmugglersQuestAuthority>(out var authority) || !authority.IsLocalOwner(quest)) return;
        if (!ContainerProvider.TryResolve<IControllerIdProvider>(out var controller)) return;
        ObservedSuccess.Remove(quest);
        ObservedSuccess.Add(quest, proof);
        MessageBroker.Instance.Publish(quest, new QuestTerminalOutcomeTriggered(quest.QuestGiver,
            controller.ControllerId, IssueFinalizeReason.QuestSuccess));
    }

    private static bool ValidateSuccess(Issue issue, MobileParty party)
    {
        if (issue.IssueQuest is not Quest quest || !quest.IsOngoing || quest._smugglerParty?.IsActive != true) return false;
        return QuestSuccessProofContext.Current == PersuasionProof
            || (QuestSuccessProofContext.Current == BribeProof && party?.LeaderHero?.Gold >= quest.BribeAmount);
    }

    private static void ApplySuccess(Quest quest)
    {
        if (QuestSuccessProofContext.Current == BribeProof)
        {
            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, quest.BribeAmount);
            quest.SucceedQuest(quest.QuestSuccessWithBribeLog);
        }
        else if (QuestSuccessProofContext.Current == PersuasionProof)
        {
            quest.SucceedQuest(quest.QuestSuccessWithPersuasionLog);
        }
    }

    private sealed class QuestAcceptStrategy : IRaceArbitratedAcceptMirrorStrategy<SmugglersQuestAcceptFields>
    {
        public void ReplayQuestAccepted(Hero owner)
        {
            if (owner?.Issue is not Issue issue || !issue.IsOngoingWithoutQuest) return;
            if (!issue.CheckPreconditions(owner, out _)) return;
            if (!ContainerProvider.TryResolve<ISmugglersQuestOwners>(out var owners)) return;
            if (!ContainerProvider.TryResolve<QuestTraitProgressHandler>(out var traits) || !traits.HasProgress(Hero.MainHero)) return;

            using (new IssueDispatchReplayGuard())
            {
                if (Campaign.Current.IssueManager.StartIssueQuest(owner) && issue.IssueQuest is Quest quest)
                {
                    owners.Set(quest, Hero.MainHero);
                    // The server runs this inside the requesting player's acceptance scope.
                    quest.QuestAcceptedConsequences();
                }
            }
        }

        public bool TryCaptureQuestFields(Hero owner, out SmugglersQuestAcceptFields fields)
        {
            fields = default;
            if (owner?.Issue?.IssueQuest is not Quest quest || !quest.IsOngoing) return false;
            if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return false;
            if (!objectManager.TryGetIdWithLogging(quest._smugglerParty, out var partyId)) return false;
            if (!objectManager.TryGetIdWithLogging(Hero.MainHero, out var playerId)) return false;

            fields = new SmugglersQuestAcceptFields(quest.StringId, partyId, quest._issueDifficulty,
                quest.RewardGold, quest.QuestDueTime.NumTicks, playerId, quest.JournalEntries[0].LogTime.NumTicks);
            return true;
        }

        public void MirrorQuestAccepted(Hero owner, SmugglersQuestAcceptFields fields)
        {
            if (owner?.Issue is not Issue issue || !issue.IsOngoingWithoutQuest) return;
            if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(fields.PartyId, out var party)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(fields.PlayerHeroId, out var player)) return;
            if (!ContainerProvider.TryResolve<ISmugglersQuestOwners>(out var owners)) return;

            using (new AllowedThread())
            {
                var quest = new Quest(fields.QuestId, owner, issue._targetSettlement, issue._originSettlement,
                    fields.Difficulty, new CampaignTime(fields.DueTimeTicks), fields.RewardGold);
                issue.IssueQuest = quest;
                issue._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
                issue._issueDifficultyMultiplier = fields.Difficulty;
                issue.IsTriedToSolveBefore = true;
                issue.IssueDueTime = CampaignTime.Never;
                quest._smugglerParty = party;
                owners.Set(quest, player);
                quest.StartQuest();
                quest._journalEntries.Add(new JournalLog(new CampaignTime(fields.StartLogTimeTicks), quest.QuestStartedLog));
                CampaignEventDispatcher.Instance.OnQuestLogAdded(quest, true);
                quest.AddTrackedObject(issue._targetSettlement);
                quest.AddTrackedObject(issue._originSettlement);
                CampaignEventDispatcher.Instance.OnIssueUpdated(issue,
                    IssueBase.IssueUpdateDetails.PlayerStartedIssueQuestClassicSolution, player);
            }
        }

        public void RejectAcceptance(Hero owner) => Reject(owner);
    }

    private sealed class AlternativeAcceptStrategy : IAlternativeAcceptMirrorStrategy<SmugglersAlternativeAcceptFields>
    {
        public void ReplayAlternativeAccepted(Hero owner)
        {
            // The generic handler already started it inside the requesting player's scope.
        }

        public bool TryCaptureAlternativeFields(Hero owner, out SmugglersAlternativeAcceptFields fields)
        {
            fields = default;
            if (owner?.Issue is not Issue issue || !issue.IsSolvingWithAlternative) return false;
            if (!ContainerProvider.TryResolve<ISmugglersQuestOwners>(out var owners) || !owners.TryGet(issue, out var player)) return false;
            if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return false;
            if (!objectManager.TryGetIdWithLogging(player, out var playerId)) return false;

            fields = new SmugglersAlternativeAcceptFields(issue.IssueDifficultyMultiplier,
                AlternativeSolutionVanillaStateSync.Capture(issue), playerId, new SmugglersJournalEntry(issue.JournalEntries[0]));
            return true;
        }

        public void MirrorAlternativeAccepted(Hero owner, SmugglersAlternativeAcceptFields fields)
        {
            if (owner?.Issue is not Issue issue || !issue.IsOngoingWithoutQuest) return;
            if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(fields.PlayerHeroId, out var player)) return;
            if (!ContainerProvider.TryResolve<ISmugglersQuestOwners>(out var owners)) return;

            using (new AllowedThread())
            {
                owners.Set(issue, player);
                issue._issueDifficultyMultiplier = fields.Difficulty;
                issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
                issue.IsTriedToSolveBefore = true;
                AlternativeSolutionVanillaStateSync.Apply(issue, fields.State, fields.StartLog.ToJournalLog());
            }
        }

        public void RejectAcceptance(Hero owner)
        {
            // The generic handler restores the request's retained selection, never the winner's roster.
        }
    }

    private static void Reject(Hero owner)
    {
        if (owner?.Issue == null) return;
        if (!ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownershipRegistry)) return;
        // A losing request can arrive after the winning player's quest was mirrored.
        if (ownershipRegistry.TryGetOwnerControllerId(owner, out _)) return;

        if (ModInformation.IsClient)
        {
            AcceptMirrorSupport.RejectAcceptance(owner);
            return;
        }

        if (owner?.Issue?.IssueQuest == null) return;
        using (new IssueFinalizeAuthorityGuard())
        {
            owner.Issue.CompleteIssueWithCancel();
        }
    }

    static SmugglersQuestType()
    {
        QuestTypeRegistry.Register(QuestDescriptorBuilder.For<Issue, Quest>("Smugglers")
            .WithCreationTrigger(issue => MessageBroker.Instance.Publish(issue.IssueOwner, new SmugglersIssueCreated(issue)))
            .WithQuestSolutionAccept(new QuestAcceptStrategy())
            .WithQuestSolutionAcceptTrigger((owner, controller) =>
            {
                if (ContainerProvider.TryResolve<QuestTraitProgressHandler>(out var traits)) traits.Begin();
            })
            .WithAlternativeAccept(new AlternativeAcceptStrategy())
            .WithQuestSuccessValidation(ValidateSuccess)
            .WithQuestSuccessProofCapture(issue => issue.IssueQuest is Quest quest
                && ObservedSuccess.TryGetValue(quest, out var proof) ? (byte)proof : (byte)0)
            .WithQuestSuccessConsequence(ApplySuccess)
            .Build());
    }
}
