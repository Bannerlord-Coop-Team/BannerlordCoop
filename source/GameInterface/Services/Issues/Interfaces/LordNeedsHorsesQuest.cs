using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Serialization;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Data;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Generic.Dispatch;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsToOffloadStolenGoods;
using GameInterface.Services.Issues.Generic.Migrated.LordNeedsHorses;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Issues.Interfaces;

using Issue = LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue;
using Quest = LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssueQuest;

internal interface ILordNeedsHorsesQuest : IRaceArbitratedAcceptMirrorStrategy<LordNeedsHorsesAcceptFields>,
    IAlternativeAcceptMirrorStrategy<NetworkLordNeedsHorsesJournal>
{
    bool IsLocalOwner(Hero giver);
    bool IsLocalJournalOwner(MBObjectBase subject, Hero giver);
    bool TryResolvePlayer(Hero giver, out Hero hero, out MobileParty party);
    IDisposable SelectPlayer(Hero giver);
    void RequestOutcome(Quest quest, IssueFinalizeReason reason);
    void WaitForAcceptance(Hero giver);
    void ResumeAcceptanceConversation(Hero giver, bool accepted);
    bool HasConflictingQuest(Issue issue);
    JournalLogEntry CreateJournal(Issue issue);
    IDisposable SelectCoercionTraitProgress(Hero giver);
    void ApplyTraitProgress(Hero hero, int progress);
    void RestoreLocalTraitProgress();
    void PrepareTroopSelection(Issue issue, PartyScreenLogic screen, ref PartyScreenLogicInitializationData data);
    bool TryCompleteTroopSelection(PartyScreenLogic screen, out bool accepted);
    bool ValidateAlternativeTroops(Issue issue, TroopRoster troops, out TextObject explanation);
    int AlternativeTroopWages(Issue issue, TroopRoster troops);
    NetworkLordNeedsHorsesJournal CaptureJournal(Issue issue, IssueBase.IssueUpdateDetails status);
    void ApplyJournal(Issue issue, NetworkLordNeedsHorsesJournal data);
}

internal sealed class LordNeedsHorsesQuest : ILordNeedsHorsesQuest
{
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IControllerIdProvider controller;
    private readonly IMessageBroker messageBroker;
    private readonly IBinaryPackageFactory packages;
    private readonly IIssueGenerationRegistry generations;

    public LordNeedsHorsesQuest(IObjectManager objectManager, IPlayerManager playerManager,
        IIssueOwnershipRegistry ownership, IControllerIdProvider controller, IMessageBroker messageBroker,
        IBinaryPackageFactory packages, IIssueGenerationRegistry generations)
    {
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.ownership = ownership;
        this.controller = controller;
        this.messageBroker = messageBroker;
        this.packages = packages;
        this.generations = generations;
    }

    public bool IsLocalOwner(Hero giver) => ModInformation.IsClient && ownership.IsLocalPeerOwner(giver);

    public bool IsLocalJournalOwner(MBObjectBase subject, Hero giver)
    {
        if (ModInformation.IsServer) return false;
        var journal = Campaign.Current.LogEntryHistory.FindLastGameActionLog(
            (LordNeedsHorsesJournalLogEntry entry) => entry.IsRelatedTo(subject));
        // Issue finalization clears active ownership before later quest-completed listeners run.
        return journal != null ? journal.OwnerControllerId == controller.ControllerId : ownership.IsLocalPeerOwner(giver);
    }

    public NetworkLordNeedsHorsesJournal CaptureJournal(Issue issue, IssueBase.IssueUpdateDetails status)
    {
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId) ||
            !generations.TryGetGeneration(issue.IssueOwner, out var generation))
            throw new InvalidOperationException("Horse quest journal has no registered issue generation");
        var quest = issue.IssueQuest as Quest;
        var entries = (quest?.JournalEntries ?? issue.JournalEntries).Select(log =>
            new LordNeedsHorsesJournalEntry(log.LogTime, PackText(log.LogText), PackText(log.TaskName),
                log.CurrentProgress, log.Range, (int)log.Type)).ToArray();
        return new NetworkLordNeedsHorsesJournal(ownerId, generation, entries,
            quest?._numMountsInInventory ?? 0, quest != null, (int)status, issue._issueDifficultyMultiplier, issue._areIssueEffectsResolved);
    }

    private byte[] PackText(TextObject text) => BinaryPackageSerializer.Serialize(packages.GetBinaryPackage(text));

    private TextObject UnpackText(byte[] bytes) =>
        BinaryPackageSerializer.Deserialize<IBinaryPackage>(bytes).Unpack<TextObject>(packages);

    public void ApplyJournal(Issue issue, NetworkLordNeedsHorsesJournal data)
    {
        var quest = issue.IssueQuest as Quest;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId) || ownerId != data.OwnerId ||
            !generations.TryGetGeneration(issue.IssueOwner, out var generation) || generation != data.Generation ||
            data.Entries == null || data.IsQuest != (quest != null))
            throw new InvalidOperationException("Horse quest journal does not match the accepted solution");
        var entries = data.Entries.Select(log => new JournalLog(log.Time, UnpackText(log.Text), UnpackText(log.Task),
            log.Progress, log.Range, (LogType)log.Type)).ToArray();
        using (new AllowedThread())
        {
            issue._issueDifficultyMultiplier = data.DifficultyMultiplier;
            issue._areIssueEffectsResolved = data.EffectsResolved;
            if (data.IsQuest)
            {
                if (IsLocalOwner(issue.IssueOwner))
                {
                    if (quest._numMountsInInventory < quest._numMountsToBeDelivered && data.Progress >= quest._numMountsToBeDelivered)
                        MBInformationManager.AddQuickInformation(quest.OnQuestMountRequirementSatisfiedQuickText);
                    else if (quest._numMountsInInventory >= quest._numMountsToBeDelivered && data.Progress < quest._numMountsToBeDelivered)
                        MBInformationManager.AddQuickInformation(quest.OnQuestMountRequirementNotSatisfiedQuickText);
                }
                quest._journalEntries.Clear();
                quest._journalEntries.AddRange(entries);
                quest._questJournalEntry = entries.FirstOrDefault(log => log.Type == LogType.Discreate);
                quest._numMountsInInventory = data.Progress;
                CampaignEventDispatcher.Instance.OnQuestLogAdded(quest, hideInformation: true);
            }
            else
            {
                issue._journalEntries.Clear();
                issue._journalEntries.AddRange(entries);
                CampaignEventDispatcher.Instance.OnIssueLogAdded(issue, hideInformation: true);
                var history = Campaign.Current.GetCampaignBehavior<JournalLogsCampaignBehavior>().GetRelatedLog(issue);
                if (history == null)
                {
                    history = CreateJournal(issue);
                    LogEntry.AddLogEntry(history);
                }
                history.Update(issue.JournalEntries, (IssueBase.IssueUpdateDetails)data.IssueStatus);
                if (data.IssueStatus != (int)IssueBase.IssueUpdateDetails.None)
                {
                    Campaign.Current.GetCampaignBehavior<SandBox.CampaignBehaviors.DefaultNotificationsCampaignBehavior>()
                        ?.OnIssueUpdated(issue, (IssueBase.IssueUpdateDetails)data.IssueStatus, null);
                }
            }
        }
    }

    public void ReplayAlternativeAccepted(Hero owner)
    {
        // The shared alternative runner has already executed vanilla with the real player's scope.
    }

    public bool TryCaptureAlternativeFields(Hero owner, out NetworkLordNeedsHorsesJournal fields)
    {
        fields = default;
        if (owner?.Issue is not Issue { IsSolvingWithAlternative: true } issue) return false;
        fields = CaptureJournal(issue, IssueBase.IssueUpdateDetails.PlayerSentTroopsToQuest);
        return true;
    }

    public void MirrorAlternativeAccepted(Hero owner, NetworkLordNeedsHorsesJournal fields)
    {
        if (owner?.Issue is Issue issue) ApplyJournal(issue, fields);
    }

    void IAlternativeAcceptMirrorStrategy<NetworkLordNeedsHorsesJournal>.RejectAcceptance(Hero owner)
    {
        if (ModInformation.IsServer || owner?.Issue is not Issue issue || ownership.TryGetOwnerControllerId(owner, out _)) return;
        using (new AllowedThread())
        {
            issue.AlternativeSolutionSentTroops.Clear();
        }
    }

    public void PrepareTroopSelection(Issue issue, PartyScreenLogic screen, ref PartyScreenLogicInitializationData data)
    {
        if (ModInformation.IsServer || !issue.IsOngoingWithoutQuest ||
            !ReferenceEquals(data.LeftMemberRoster, issue.AlternativeSolutionSentTroops)) return;
        // Detached selection rosters leave party changes to the accepted server request.
        var available = TroopRoster.CreateDummyTroopRoster();
        available.Add(data.RightMemberRoster);
        var selected = TroopRoster.CreateDummyTroopRoster();
        selected.Add(data.LeftMemberRoster);
        foreach (var troop in selected.GetTroopRoster())
            available.AddToCounts(troop.Character, -troop.Number, false, -troop.WoundedNumber, -troop.Xp);
        data.RightMemberRoster = available;
        data.LeftMemberRoster = selected;
        var prisoners = TroopRoster.CreateDummyTroopRoster();
        prisoners.Add(data.RightPrisonerRoster);
        data.RightPrisonerRoster = prisoners;
        data.IsTroopUpgradesDisabled = true;
        LordNeedsHorsesQuestType.TroopSelections.Add(screen, issue);
    }

    public bool TryCompleteTroopSelection(PartyScreenLogic screen, out bool accepted)
    {
        accepted = false;
        if (!LordNeedsHorsesQuestType.TroopSelections.TryGetValue(screen, out var issue)) return false;
        if (issue.IssueOwner.Issue != issue || !issue.IsOngoingWithoutQuest) return true;
        var selected = screen.MemberRosters[0];
        if (!ValidateAlternativeTroops(issue, selected, out var explanation))
        {
            MBInformationManager.AddQuickInformation(explanation);
            return true;
        }
        issue.AlternativeSolutionSentTroops.Clear();
        issue.AlternativeSolutionSentTroops.Add(selected);
        screen._initialData.CopyFromScreenData(screen.CurrentData);
        accepted = true;
        return true;
    }

    public bool ValidateAlternativeTroops(Issue issue, TroopRoster troops, out TextObject explanation)
    {
        if (!Helpers.QuestHelper.CheckRosterForAlternativeSolution(troops,
            issue.GetTotalAlternativeSolutionNeededMenCount(), out explanation, 2, mountedRequired: true)) return false;
        var companion = troops.GetTroopRoster().FirstOrDefault(troop => troop.Character.IsHero).Character?.HeroObject;
        if (troops.TotalHeroes != 1 || companion == null || companion == Hero.MainHero ||
            companion.Clan != Hero.MainHero.Clan || companion.PartyBelongedTo != MobileParty.MainParty ||
            !companion.CanHaveCampaignIssues() || companion.IsWounded || companion.IsPregnant)
        {
            explanation = new TextObject("{=DBabgrcC}This hero is not available right now.");
            return false;
        }
        return Helpers.QuestHelper.CheckGoldForAlternativeSolution(
            issue.AlternativeSolutionGoldRequirement + AlternativeTroopWages(issue, troops), out explanation);
    }

    public int AlternativeTroopWages(Issue issue, TroopRoster troops)
    {
        var companion = troops.GetTroopRoster().First(troop => troop.Character.IsHero).Character.HeroObject;
        int duration = (int)Campaign.Current.Models.IssueModel.GetDurationOfResolutionForHero(companion, issue).ToDays;
        return troops.GetTroopRoster().Sum(troop => troop.Character.TroopWage * troop.Number * duration);
    }

    public void RestoreLocalTraitProgress()
    {
        if (ModInformation.IsServer || !playerManager.TryGetPlayer(controller.ControllerId, out var player) ||
            !objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero) ||
            !GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress.TryGet(hero, out var traits) ||
            !traits.GetProperties().Contains(DefaultTraits.Honor)) return;
        Campaign.Current.PlayerTraitDeveloper.SetPropertyValue(DefaultTraits.Honor, traits.GetPropertyValue(DefaultTraits.Honor));
    }

    public IDisposable SelectCoercionTraitProgress(Hero giver)
    {
        if (!TryResolvePlayer(giver, out var hero, out _))
            throw new InvalidOperationException("Horse quest coercion has no registered player owner");
        return new CoercionTraitScope(hero, messageBroker);
    }

    public void ApplyTraitProgress(Hero hero, int progress)
    {
        var registry = GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress;
        if (!registry.TryGet(hero, out var traits)) traits = new PropertyOwner<PropertyObject>();
        traits.SetPropertyValue(DefaultTraits.Honor, progress);
        registry.Set(hero, traits);
        if (ModInformation.IsClient && playerManager.TryGetPlayer(controller.ControllerId, out var player) &&
            objectManager.TryGetIdWithLogging(hero, out var heroId) && player.HeroId == heroId)
            Campaign.Current.PlayerTraitDeveloper.SetPropertyValue(DefaultTraits.Honor, progress);
    }

    private sealed class CoercionTraitScope : IDisposable
    {
        private readonly Hero hero;
        private readonly IMessageBroker broker;
        private readonly PropertyOwner<PropertyObject> previous;
        private readonly PropertyOwner<PropertyObject> progress;
        private readonly int before;

        public CoercionTraitScope(Hero hero, IMessageBroker broker)
        {
            this.hero = hero;
            this.broker = broker;
            previous = Campaign.Current.PlayerTraitDeveloper;
            var registry = GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress;
            if (!registry.TryGet(hero, out var stored)) stored = new PropertyOwner<PropertyObject>();
            progress = stored;
            if (!progress.GetProperties().Contains(DefaultTraits.Honor))
                progress.SetPropertyValue(DefaultTraits.Honor,
                    Campaign.Current.Models.CharacterDevelopmentModel.GetTraitXpRequiredForTraitLevel(DefaultTraits.Honor, hero.GetTraitLevel(DefaultTraits.Honor)));
            registry.Set(hero, progress);
            before = progress.GetPropertyValue(DefaultTraits.Honor);
            Campaign.Current.PlayerTraitDeveloper = progress;
        }

        public void Dispose()
        {
            Campaign.Current.PlayerTraitDeveloper = previous;
            var after = progress.GetPropertyValue(DefaultTraits.Honor);
            if (after != before) broker.Publish(hero, new LordNeedsHorsesTraitProgressChanged(hero, after));
        }
    }

    public JournalLogEntry CreateJournal(Issue issue)
    {
        if (!ownership.TryGetOwnerControllerId(issue.IssueOwner, out var controllerId))
        {
            if (!QuestSolutionStartAuthorityGuard.IsActive && !AlternativeSolutionStartAuthorityGuard.IsActive)
                throw new InvalidOperationException("Cannot create a horse quest journal without an owner");
            if (objectManager.TryGetIdWithLogging(Hero.MainHero, out var heroId))
            {
                foreach (var player in playerManager.Players)
                {
                    if (player.HeroId == heroId) controllerId = player.ControllerId;
                }
            }
        }
        if (controllerId == null) throw new InvalidOperationException("Horse quest journal player is unavailable");
        return issue.IssueQuest is Quest quest
            ? new LordNeedsHorsesJournalLogEntry(controllerId, quest.Title, issue.IssueOwner, issue, quest)
            : new LordNeedsHorsesJournalLogEntry(controllerId, issue.Title, issue.IssueOwner, issue);
    }

    public bool HasConflictingQuest(Issue issue)
    {
        foreach (var entry in Campaign.Current.IssueManager.Issues)
        {
            if (entry.Value is not Issue other || other == issue ||
                (!other.IsSolvingWithQuest && !other.IsSolvingWithAlternative)) continue;
            if (!TryResolvePlayer(entry.Key, out var hero, out _) || hero == Hero.MainHero) return true;
        }
        return false;
    }

    public bool TryResolvePlayer(Hero giver, out Hero hero, out MobileParty party)
    {
        hero = null;
        party = null;
        return ownership.TryGetOwnerControllerId(giver, out var controllerId) &&
            playerManager.TryGetPlayer(controllerId, out var player) &&
            objectManager.TryGetObjectWithLogging(player.HeroId, out hero) &&
            objectManager.TryGetObjectWithLogging(player.MobilePartyId, out party);
    }

    public IDisposable SelectPlayer(Hero giver)
    {
        if (!TryResolvePlayer(giver, out var hero, out var party))
            throw new InvalidOperationException("Lord Needs Horses has no registered player owner");
        return new MainHeroSubstitutionScope(hero, party);
    }

    public void RequestOutcome(Quest quest, IssueFinalizeReason reason)
    {
        if (!IsLocalOwner(quest.QuestGiver) || !quest.IsOngoing) return;
        messageBroker.Publish(quest.QuestGiver,
            new QuestTerminalOutcomeTriggered(quest.QuestGiver, controller.ControllerId, reason));
    }

    public void WaitForAcceptance(Hero giver)
    {
        if (ModInformation.IsServer) return;
        var conversation = Campaign.Current.ConversationManager;
        if (!conversation.IsConversationInProgress || conversation.OneToOneConversationHero != giver ||
            conversation.ActiveToken != conversation.GetStateIndex("issue_classic_quest_start")) return;
        LordNeedsHorsesQuestType.PendingAcceptances.Remove(conversation);
        LordNeedsHorsesQuestType.PendingAcceptances.Add(conversation, giver);
    }

    public void ResumeAcceptanceConversation(Hero giver, bool accepted)
    {
        if (ModInformation.IsServer) return;
        var conversation = Campaign.Current.ConversationManager;
        if (!LordNeedsHorsesQuestType.PendingAcceptances.TryGetValue(conversation, out var waitingFor) || waitingFor != giver) return;
        LordNeedsHorsesQuestType.PendingAcceptances.Remove(conversation);
        if (!conversation.IsConversationInProgress || conversation.OneToOneConversationHero != giver ||
            conversation.ActiveToken != conversation.GetStateIndex("issue_classic_quest_start")) return;
        if (accepted && IsLocalOwner(giver)) conversation.DoOptionContinue();
        else conversation.EndConversation();
    }

    public void ReplayQuestAccepted(Hero owner)
    {
        if (owner?.Issue is not Issue issue || !issue.IsOngoingWithoutQuest) return;
        if (!issue.IssueStayAliveConditions() || !issue.CheckPreconditions(owner, out _)) return;

        using (new IssueDispatchReplayGuard())
        using (new QuestSolutionStartAuthorityGuard())
        {
            Campaign.Current.IssueManager.StartIssueQuest(owner);
            if (issue.IssueQuest is Quest quest && quest._questJournalEntry == null)
                quest.OnQuestAccepted();
        }
    }

    public bool TryCaptureQuestFields(Hero owner, out LordNeedsHorsesAcceptFields fields)
    {
        fields = default;
        if (owner?.Issue?.IssueQuest is not Quest quest || !quest.IsOngoing ||
            !objectManager.TryGetIdWithLogging(Hero.MainHero, out var heroId) ||
            !objectManager.TryGetIdWithLogging(MobileParty.MainParty, out var partyId)) return false;

        fields = new LordNeedsHorsesAcceptFields(heroId, partyId, quest.QuestDueTime,
            CaptureJournal((Issue)owner.Issue, IssueBase.IssueUpdateDetails.None));
        return true;
    }

    public void MirrorQuestAccepted(Hero owner, LordNeedsHorsesAcceptFields fields)
    {
        if (owner?.Issue is not Issue issue) return;
        if (!objectManager.TryGetObjectWithLogging<Hero>(fields.PlayerHeroId, out var hero) ||
            !objectManager.TryGetObjectWithLogging<MobileParty>(fields.PlayerPartyId, out var party))
            throw new InvalidOperationException("Lord Needs Horses acceptance references an unavailable player");

        using (new MainHeroSubstitutionScope(hero, party))
        using (new AllowedThread())
        using (new QuestSolutionStartAuthorityGuard())
        using (new IssueDispatchReplayGuard())
        {
            if (issue.IsOngoingWithoutQuest)
                Campaign.Current.IssueManager.StartIssueQuest(owner);
            if (issue.IssueQuest is not Quest quest) return;
            quest.QuestDueTime = fields.DueTime;
            if (quest._questJournalEntry == null) quest.OnQuestAccepted();
            ApplyJournal(issue, fields.Journal);
        }
        ResumeAcceptanceConversation(owner, accepted: true);
    }

    public void RejectAcceptance(Hero owner)
    {
        if (owner?.Issue?.IssueQuest is not Quest || ownership.TryGetOwnerControllerId(owner, out _)) return;
        using (new AllowedThread())
        using (new IssueFinalizeAuthorityGuard())
            owner.Issue.CompleteIssueWithCancel();
    }
}
