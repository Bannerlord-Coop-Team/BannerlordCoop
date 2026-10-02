using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Registry.Messages;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsToOffloadStolenGoods;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Initialization;
using GameInterface.Services.ObjectManager;
using Helpers;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Handlers;

using Quest = ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;
using QuestState = ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest.ExtortionByDesertersQuestState;
using Issue = ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssue;

internal sealed class ExtortionQuestStateHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IExtortionQuestWorld world;
    private readonly IExtortionQuestJournal journal;
    private readonly IMapEventInitializationBarrier initialization;

    public ExtortionQuestStateHandler(IMessageBroker broker, INetwork network, IObjectManager objects,
        IIssueOwnershipRegistry ownership, IExtortionQuestWorld world, IExtortionQuestJournal journal,
        IMapEventInitializationBarrier initialization)
    {
        this.broker = broker;
        this.network = network;
        this.objects = objects;
        this.ownership = ownership;
        this.world = world;
        this.journal = journal;
        this.initialization = initialization;
        ownership.OwnershipAssigned += journal.AssignOwner;
        broker.Subscribe<ExtortionQuestChanged>(Send);
        broker.Subscribe<NetworkExtortionQuestState>(Receive);
        broker.Subscribe<MapEventFinalized>(BattleFinalized);
        broker.Subscribe<ExtortionAlternativeChanged>(SendAlternative);
        broker.Subscribe<NetworkExtortionAlternativeState>(ReceiveAlternative);
        broker.Subscribe<ExtortionTraitXpChanged>(SendTraitXp);
        broker.Subscribe<NetworkExtortionTraitXp>(ReceiveTraitXp);
        broker.Subscribe<PlayerHeirSelectionCompleted>(PlayerReplaced);
        broker.Subscribe<AllGameObjectsRegistered>(Loaded);
    }

    public void Dispose()
    {
        ownership.OwnershipAssigned -= journal.AssignOwner;
        broker.Unsubscribe<ExtortionQuestChanged>(Send);
        broker.Unsubscribe<NetworkExtortionQuestState>(Receive);
        broker.Unsubscribe<MapEventFinalized>(BattleFinalized);
        broker.Unsubscribe<ExtortionAlternativeChanged>(SendAlternative);
        broker.Unsubscribe<NetworkExtortionAlternativeState>(ReceiveAlternative);
        broker.Unsubscribe<ExtortionTraitXpChanged>(SendTraitXp);
        broker.Unsubscribe<NetworkExtortionTraitXp>(ReceiveTraitXp);
        broker.Unsubscribe<PlayerHeirSelectionCompleted>(PlayerReplaced);
        broker.Unsubscribe<AllGameObjectsRegistered>(Loaded);
    }

    private void Loaded(MessagePayload<AllGameObjectsRegistered> payload)
    {
        if (ModInformation.IsClient) return;
        // The saved player registrations are restored by another subscriber to this event.
        GameThread.EnqueueSafe(() =>
        {
            foreach (var issue in Campaign.Current.IssueManager.Issues.Values.OfType<Issue>().ToArray())
                if (issue.IssueOwner.IsNotable && issue.IssueOwner.CurrentSettlement == null)
                    issue.CompleteIssueWithCancel();
        });
    }

    private void PlayerReplaced(MessagePayload<PlayerHeirSelectionCompleted> payload)
    {
        if (ModInformation.IsServer) world.PlayerReplaced(payload.What.PlayerHero);
    }

    private void SendTraitXp(MessagePayload<ExtortionTraitXpChanged> payload)
    {
        var data = payload.What;
        if (ModInformation.IsClient || !objects.TryGetIdWithLogging(data.Hero, out var heroId) ||
            !objects.TryGetIdWithLogging(data.Trait, out var traitId)) return;
        network.SendAll(new NetworkExtortionTraitXp(heroId, traitId, data.Xp, data.Hero.GetTraitLevel(data.Trait)));
    }

    private void ReceiveTraitXp(MessagePayload<NetworkExtortionTraitXp> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objects.TryGetObjectWithLogging<Hero>(data.HeroId, out var hero) ||
                !objects.TryGetObjectWithLogging<TraitObject>(data.TraitId, out var trait)) return;
            using (new AllowedThread())
            {
                var registry = GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress;
                if (!registry.TryGet(hero, out var progress)) progress = new PropertyOwner<PropertyObject>();
                progress.SetPropertyValue(trait, data.Xp);
                registry.Set(hero, progress);
                var previousLevel = hero.GetTraitLevel(trait);
                hero.SetTraitLevel(trait, data.Level);
                if (hero != Hero.MainHero) return;
                Campaign.Current.PlayerTraitDeveloper.SetPropertyValue(trait, data.Xp);
                if (previousLevel != data.Level)
                    CampaignEventDispatcher.Instance.OnPlayerTraitChanged(trait, previousLevel);
            }
        });
    }

    private void SendAlternative(MessagePayload<ExtortionAlternativeChanged> payload)
    {
        var issue = payload.What.Issue;
        if (ModInformation.IsClient || !issue.IsSolvingWithAlternative ||
            !ownership.TryGetOwnerControllerId(issue.IssueOwner, out _) ||
            !objects.TryGetIdWithLogging(issue.IssueOwner, out var giverId)) return;
        network.SendAll(new NetworkExtortionAlternativeState(giverId, issue.StringId, issue._areIssueEffectsResolved,
            issue.JournalEntries.Select(entry => new ExtortionJournalEntry(entry)).ToArray(), payload.What.Status));
    }

    private void ReceiveAlternative(MessagePayload<NetworkExtortionAlternativeState> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objects.TryGetObjectWithLogging<Hero>(data.GiverId, out var giver) ||
                giver.Issue is not Issue issue || issue.StringId != data.IssueId || !issue.IsSolvingWithAlternative ||
                data.Journal == null || !Enum.IsDefined(typeof(IssueBase.IssueUpdateDetails), data.Status)) return;
            using (new AllowedThread())
            {
                issue._areIssueEffectsResolved = data.EffectsResolved;
                issue._journalEntries.Clear();
                foreach (var entry in data.Journal) issue._journalEntries.Add(entry.ToJournalLog());
                if (!ownership.IsLocalPeerOwner(giver)) return;
                CampaignEventDispatcher.Instance.OnIssueLogAdded(issue, false);
                if (data.Status != IssueBase.IssueUpdateDetails.None)
                    CampaignEventDispatcher.Instance.OnIssueUpdated(issue, data.Status, Hero.MainHero);
            }
        });
    }

    private void BattleFinalized(MessagePayload<MapEventFinalized> payload)
    {
        if (ModInformation.IsClient) return;
        GameThread.RunSafe(() =>
        {
            foreach (var quest in Campaign.Current.QuestManager.Quests.OfType<Quest>().ToArray())
                world.CleanupBattle(quest);
        });
    }

    private void Send(MessagePayload<ExtortionQuestChanged> payload)
    {
        if (ModInformation.IsClient) return;
        var quest = payload.What.Quest;
        if (!quest.IsOngoing || !ownership.TryGetOwnerControllerId(quest.QuestGiver, out _)) return;
        if (!objects.TryGetIdWithLogging(quest.QuestGiver, out var giverId)) return;
        if (!TryGetPartyId(quest._deserterMobileParty, out var deserterId) ||
            !TryGetPartyId(quest._defenderMobileParty, out var defenderId)) return;
        string ambushId = null;
        if (payload.What.StartAmbush &&
            !objects.TryGetIdWithLogging(quest._deserterMobileParty?.MapEvent, out ambushId)) return;

        network.SendAll(new NetworkExtortionQuestState(giverId, quest.StringId, (int)quest._currentState,
            deserterId, defenderId, quest._desertersRunAwayTimeoutTime,
            quest._deserterBattleFinalizedForTheFirstTime, quest._playerAwayFromSettlementNotificationSent,
            quest.JournalEntries.Select(entry => new ExtortionJournalEntry(entry)).ToArray(), payload.What.StartAmbush, ambushId));
    }

    private bool TryGetPartyId(MobileParty party, out string id)
    {
        id = null;
        return party == null || !party.IsActive || objects.TryGetIdWithLogging(party, out id);
    }

    private bool TryGetParty(string id, out MobileParty party)
    {
        party = null;
        return id == null || objects.TryGetObjectWithLogging(id, out party);
    }

    private void Receive(MessagePayload<NetworkExtortionQuestState> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objects.TryGetObjectWithLogging<Hero>(data.GiverId, out var giver)) return;
            if (!ownership.IsLocalPeerOwner(giver) || giver.Issue?.IssueQuest is not Quest quest ||
                !quest.IsOngoing || quest.StringId != data.QuestId) return;
            if (!Enum.IsDefined(typeof(QuestState), data.State) || data.Journal == null) return;
            if (!TryGetParty(data.DeserterPartyId, out var deserters) ||
                !TryGetParty(data.DefenderPartyId, out var defenders)) return;

            using (new AllowedThread())
            {
                var previousLogCount = quest.JournalEntries.Count;
                var previousState = quest._currentState;
                var previouslyWarned = quest._playerAwayFromSettlementNotificationSent;
                quest._deserterMobileParty = deserters;
                quest._defenderMobileParty = defenders;
                quest._currentState = (QuestState)data.State;
                quest._desertersRunAwayTimeoutTime = data.RunAwayDueTime;
                quest._deserterBattleFinalizedForTheFirstTime = data.BattleFinalized;
                quest._playerAwayFromSettlementNotificationSent = data.AwayWarningSent;
                quest._journalEntries.Clear();
                foreach (var entry in data.Journal) quest._journalEntries.Add(entry.ToJournalLog());
                if (quest._currentState == QuestState.DesertersAreDefeated)
                {
                    if (!quest.IsTracked(giver)) quest.AddTrackedObject(giver);
                    if (!quest.IsTracked(quest.QuestSettlement)) quest.AddTrackedObject(quest.QuestSettlement);
                }
                if (data.Journal.Length > previousLogCount)
                    CampaignEventDispatcher.Instance.OnQuestLogAdded(quest, false);
                if (previousState != quest._currentState && quest._currentState == QuestState.DesertersRunningAwayFromPlayer)
                    MBInformationManager.AddQuickInformation(quest.OnDesertersNoticedPlayerNotificationText, 0,
                        Hero.MainHero.CharacterObject);
                if (previousState != quest._currentState && quest._currentState == QuestState.DesertersAreDefeated)
                    MBInformationManager.AddQuickInformation(quest.OnPlayerDefeatedDesertersNotificationText, 0,
                        giver.CharacterObject);
                if (!previouslyWarned && data.AwayWarningSent)
                    MBInformationManager.AddQuickInformation(quest.OnPlayerLeftQuestSettlementNotificationText, 0,
                        giver.CharacterObject);
                if (data.StartAmbush) QueueAmbush(quest, data.AmbushMapEventId);
            }
        });
    }

    internal void QueueAmbush(Quest quest, string mapEventId)
    {
        if (mapEventId == null || !objects.TryGetObjectWithLogging<MapEvent>(mapEventId, out var battle)) return;
        initialization.RunAfterCommit(battle, () =>
        {
            if (!quest.IsOngoing || quest.QuestGiver.Issue?.IssueQuest != quest ||
                !ownership.IsLocalPeerOwner(quest.QuestGiver) || !battle.IsRaid ||
                quest._defenderMobileParty?.MapEvent != battle || quest._deserterMobileParty?.MapEvent != battle ||
                MobileParty.MainParty.MapEvent != battle) return;

            using (new AllowedThread())
            {
                // Adopt the existing raid without finishing it or replaying world creation.
                Campaign.Current.LocationEncounter = null;
                PlayerEncounter.Start();
                PlayerEncounter.Init();
                PlayerEncounter.Current.PlayerPartyInitialStrength = PartyBase.MainParty.CalculateCurrentStrength();
                var leader = ConversationHelper.GetConversationCharacterPartyLeader(quest._deserterMobileParty.Party);
                GameMenu.ActivateGameMenu("encounter_meeting");
                CampaignMapConversation.OpenConversation(
                    new ConversationCharacterData(CharacterObject.PlayerCharacter, PartyBase.MainParty, noHorse: true),
                    new ConversationCharacterData(leader, quest._deserterMobileParty.Party, noHorse: true));
            }
        });
    }
}
