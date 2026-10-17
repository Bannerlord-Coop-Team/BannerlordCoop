using Common;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.CreationCapture;
using GameInterface.Services.Issues.Generic.Dispatch;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsToOffloadStolenGoods;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.TroopRosters.Data;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Interfaces;

using Issue = LordWantsRivalCapturedIssueBehavior.LordWantsRivalCapturedIssue;
using Quest = LordWantsRivalCapturedIssueBehavior.LordWantsRivalCapturedIssueQuest;

internal interface ILordWantsRivalCapturedQuestService
{
    void BroadcastCreation(Issue issue);
    void MirrorCreation(NetworkRivalCapturedIssueCreated data);
    void PrepareAcceptance(Hero giver);
    bool TryCaptureFrameworkQuestFields(Hero giver, out RivalCapturedAcceptFields fields);
    void ApplyFrameworkQuestAcceptance(Issue issue, RivalCapturedAcceptFields fields);
    void ReplayQuestAccepted(Hero giver);
    void RejectAcceptance(Hero giver);
    bool TryCaptureQuestFields(Hero giver, out RivalCapturedAcceptFields fields);
    void MirrorQuestAccepted(Hero giver, RivalCapturedAcceptFields fields);
    bool TryEnterOwnerScope(Quest quest, out IDisposable scope);
    bool IsOwnedBy(Quest quest, Hero hero);
    bool IsPresentWithGiver(string controllerId, Hero giver);
    void RequestChoice(Quest quest, RivalCapturedChoice choice);
    void ApplyChoice(Player player, RequestRivalCapturedChoice request);
    void RecordPendingCaptures(Player player, TroopRosterData prisoners);
    void ClearPendingCapture(Hero giver);
    void ClearTargetPendingCaptures(Hero target);
    void ClearPlayerPendingCaptures(string controllerId);
    void SendProgress(Quest quest);
    void MirrorProgress(NetworkRivalCapturedProgress data);
    void MirrorTraitProgress(NetworkRivalCapturedTraitProgress data);
    void RequestTraitChange(int honorXp);
    void ApplyTraitProgress(Player player, RequestRivalCapturedTraitProgress data);
    void RestoreLocalTraitProgress(Hero hero);
    void CancelPlayerQuests(string controllerId);
    void CheckCancellation(Quest quest, bool causedByPlayer);
    void OnBattleWon(MapEvent mapEvent);
    void RemoveOtherPlayersQuests(QuestManager manager);
}

internal sealed class LordWantsRivalCapturedQuestService : ILordWantsRivalCapturedQuestService,
    ICreationCaptureStrategy<Issue, Hero>
{
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IIssueGenerationRegistry generations;
    private readonly ConversationPartyTracker conversations;
    private readonly PendingRegistry<(Quest Quest, Hero Target, Player Owner, int Generation)> pendingCaptures = new();

    public LordWantsRivalCapturedQuestService(IObjectManager objectManager, INetwork network,
        IPlayerManager playerManager, IControllerIdProvider controllerIdProvider,
        IIssueOwnershipRegistry ownership, IIssueGenerationRegistry generations,
        ConversationPartyTracker conversations)
    {
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.controllerIdProvider = controllerIdProvider;
        this.ownership = ownership;
        this.generations = generations;
        this.conversations = conversations;
    }

    public bool TryCaptureFields(Issue issue, out Hero target)
    {
        target = issue?._targetHero;
        return target != null;
    }

    public Issue ConstructReplicated(Hero giver, Hero target) => new Issue(giver, target);

    public void BroadcastCreation(Issue issue)
    {
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var giverId) ||
            !objectManager.TryGetIdWithLogging(issue._targetHero, out var targetId)) return;

        network.SendAll(new NetworkRivalCapturedIssueCreated(giverId, targetId, issue.StringId,
            generations.Bump(issue.IssueOwner), issue.IssueCreationTime, issue.IssueDueTime));
    }

    public void MirrorCreation(NetworkRivalCapturedIssueCreated data)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(data.GiverId, out var giver) ||
            !objectManager.TryGetObjectWithLogging<Hero>(data.TargetId, out var target)) return;
        if (generations.TryGetGeneration(giver, out var generation) && generation >= data.Generation) return;
        if (giver.Issue != null) return;

        var issue = new CreationCaptureRunner<Issue, Hero>(this).ConstructAndRegisterReplicated(giver, target);
        issue.StringId = data.IssueId;
        issue.IssueCreationTime = data.CreationTime;
        issue.IssueDueTime = data.DueTime;
        generations.SetGeneration(giver, data.Generation);
    }

    public void ReplayQuestAccepted(Hero giver)
    {
        if (giver?.Issue is not Issue issue || !issue.IsOngoingWithoutQuest ||
            !issue.CheckPreconditions(giver, out _) || !TryGetCurrentPlayer(out _) ||
            !generations.TryGetGeneration(giver, out _) ||
            !GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress.TryGet(Hero.MainHero, out var progress) ||
            !progress.HasProperty(DefaultTraits.Honor)) return;

        using (new IssueDispatchReplayGuard())
        {
            if (!Campaign.Current.IssueManager.StartIssueQuest(giver)) return;
            if (issue.IssueQuest is Quest quest && quest.IsOngoing &&
                !Campaign.Current.QuestManager.Quests.Contains(quest))
                quest.QuestAcceptedConsequences();
        }
    }

    public void RejectAcceptance(Hero giver)
    {
        if (giver?.Issue?.IssueQuest is not Quest quest || ownership.TryGetOwnerControllerId(giver, out _)) return;

        // A rejected optimistic client accept must discard only its local quest so the
        // still-available issue can be retried after the generation catches up.
        if (ModInformation.IsClient)
        {
            var issue = giver.Issue;
            issue.IssueQuest = null;
            issue._issueState = IssueBase.IssueState.Ongoing;
            issue.IssueDueTime = CampaignTime.DaysFromNow(1f);
            issue.IsTriedToSolveBefore = false;
            Campaign.Current.QuestManager.RemoveAllTrackedObjectsForQuest(quest);
            Campaign.Current.QuestManager.OnQuestFinalized(quest);
            return;
        }

        using (new IssueFinalizeAuthorityGuard())
        {
            if (quest.IsOngoing) quest.CompleteQuestWithCancel();
            else giver.Issue.CompleteIssueWithCancel();
        }
    }

    public bool TryCaptureQuestFields(Hero giver, out RivalCapturedAcceptFields fields)
    {
        fields = default;
        if (giver?.Issue?.IssueQuest is not Quest quest || !quest.IsOngoing ||
            !Campaign.Current.QuestManager.Quests.Contains(quest) ||
            !TryGetCurrentPlayer(out var player) || !generations.TryGetGeneration(giver, out var generation)) return false;

        fields = new RivalCapturedAcceptFields(player.ControllerId, generation, quest.QuestDueTime, CaptureState(quest));
        return true;
    }

    public void MirrorQuestAccepted(Hero giver, RivalCapturedAcceptFields fields)
    {
        if (giver.Issue is not Issue issue ||
            !generations.TryGetGeneration(giver, out var generation) || generation != fields.Generation) return;

        if (fields.ControllerId != controllerIdProvider.ControllerId)
        {
            issue._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
            issue.IsTriedToSolveBefore = true;
            issue.IssueDueTime = CampaignTime.Never;
            return;
        }

        using (new AllowedThread())
        using (new IssueDispatchReplayGuard())
        using (new QuestSolutionStartAuthorityGuard())
        {
            if (issue.IsOngoingWithoutQuest && !Campaign.Current.IssueManager.StartIssueQuest(giver)) return;
            if (issue.IssueQuest is not Quest quest) return;
            quest.ChangeQuestDueTime(fields.DueTime);
            if (quest.IsOngoing && !Campaign.Current.QuestManager.Quests.Contains(quest))
                quest.QuestAcceptedConsequences();
            ApplyState(quest, fields.State);
        }
    }

    public bool TryEnterOwnerScope(Quest quest, out IDisposable scope)
    {
        scope = null;
        if (!ownership.TryGetOwnerControllerId(quest.QuestGiver, out var controllerId) ||
            !playerManager.TryGetPlayer(controllerId, out var player)) return false;
        return TryEnterOwnerScope(player, out scope);
    }

    private bool TryEnterOwnerScope(Player player, out IDisposable scope)
    {
        scope = null;
        if (!objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero) ||
            !objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)) return false;

        scope = new OwnerScope(hero, party, xp => SendTraitProgress(player, xp));
        return true;
    }

    private sealed class OwnerScope : IDisposable
    {
        private readonly MainHeroSubstitutionScope heroScope;
        private readonly IssueFinalizeAuthorityGuard authority;
        private readonly PropertyOwner<PropertyObject> previousTraitProgress;
        private readonly PropertyOwner<PropertyObject> ownerTraitProgress;
        private readonly int previousHonorXp;
        private readonly Action<int> sendTraitProgress;

        public OwnerScope(Hero hero, MobileParty party, Action<int> sendTraitProgress)
        {
            this.sendTraitProgress = sendTraitProgress;
            previousTraitProgress = Campaign.Current.PlayerTraitDeveloper;
            // Reuse the saved per-player issue XP store, including progress from stolen-goods quests.
            var progress = GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress;
            if (!progress.TryGet(hero, out ownerTraitProgress))
            {
                ownerTraitProgress = new PropertyOwner<PropertyObject>();
                progress.Set(hero, ownerTraitProgress);
            }
            previousHonorXp = ownerTraitProgress.GetPropertyValue(DefaultTraits.Honor);
            heroScope = new MainHeroSubstitutionScope(hero, party);
            authority = new IssueFinalizeAuthorityGuard();
            Campaign.Current.PlayerTraitDeveloper = ownerTraitProgress;
        }

        public void Dispose()
        {
            try
            {
                var honorXp = ownerTraitProgress.GetPropertyValue(DefaultTraits.Honor);
                ownerTraitProgress._attributes[DefaultTraits.Honor] = honorXp;
                if (honorXp != previousHonorXp) sendTraitProgress(honorXp);
            }
            finally
            {
                Campaign.Current.PlayerTraitDeveloper = previousTraitProgress;
                heroScope.Dispose();
                authority.Dispose();
            }
        }
    }

    private bool TryGetCurrentPlayer(out Player player)
    {
        player = null;
        if (Hero.MainHero == null || !objectManager.TryGetId(Hero.MainHero, out var heroId)) return false;
        player = playerManager.Players.FirstOrDefault(candidate => candidate.HeroId == heroId);
        return player != null;
    }

    public bool IsOwnedBy(Quest quest, Hero hero)
    {
        return objectManager.TryGetId(hero, out var heroId) &&
            ownership.TryGetOwnerControllerId(quest.QuestGiver, out var controllerId) &&
            playerManager.TryGetPlayer(controllerId, out var player) && player.HeroId == heroId;
    }

    public bool IsPresentWithGiver(string controllerId, Hero giver)
    {
        if (!playerManager.TryGetPlayer(controllerId, out var player) ||
            !objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)) return false;
        if (giver.CurrentSettlement != null && party.CurrentSettlement == giver.CurrentSettlement) return true;

        return giver.PartyBelongedTo != null && playerManager.TryGetPeer(controllerId, out var peer) &&
            conversations.TryGetEngagement(peer, out var engagement) &&
            objectManager.TryGetId(giver.PartyBelongedTo.Party, out var giverPartyId) &&
            objectManager.TryGetId(party.Party, out var playerPartyId) &&
            engagement.PartyId == giverPartyId && engagement.EngagerPartyId == playerPartyId;
    }

    public void RequestChoice(Quest quest, RivalCapturedChoice choice)
    {
        if (!quest.IsOngoing || !ownership.IsLocalPeerOwner(quest.QuestGiver) ||
            !objectManager.TryGetIdWithLogging(quest.QuestGiver, out var giverId) ||
            !generations.TryGetGeneration(quest.QuestGiver, out var generation)) return;

        network.SendAll(new RequestRivalCapturedChoice(giverId, generation, choice));
    }

    public void ApplyChoice(Player player, RequestRivalCapturedChoice request)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(request.GiverId, out var giver) ||
            !ownership.TryGetOwnerControllerId(giver, out var controllerId) || controllerId != player.ControllerId ||
            !generations.TryGetGeneration(giver, out var generation) || generation != request.Generation ||
            giver.Issue?.IssueQuest is not Quest quest || !quest.IsOngoing ||
            !TryEnterOwnerScope(quest, out var scope)) return;

        using (scope)
        {
            if (request.Choice == RivalCapturedChoice.HearCounterOffer && !quest._firstCounterOfferMade &&
                !quest._targetHero.IsPrisoner)
            {
                if (!TryConsumePendingCapture(quest, player, generation)) return;
                TakePrisonerAction.Apply(PartyBase.MainParty, quest._targetHero);
            }

            if (!quest._targetHero.IsPrisoner || quest._targetHero.PartyBelongedToAsPrisoner != PartyBase.MainParty ||
                !PartyBase.MainParty.PrisonRoster.Contains(quest._targetHero.CharacterObject)) return;

            ClearPendingCapture(giver);
            switch (request.Choice)
            {
                case RivalCapturedChoice.HearCounterOffer:
                    quest._firstCounterOfferMade = true;
                    SendProgress(quest);
                    break;
                case RivalCapturedChoice.AcceptCounterOffer:
                    quest.QuestFailCounterOfferAccepted();
                    break;
                case RivalCapturedChoice.DeliverToGiver:
                    if (!IsPresentWithGiver(controllerId, giver)) return;
                    Deliver(quest, giver.PartyBelongedTo?.Party ?? giver.CurrentSettlement?.Party);
                    break;
                case RivalCapturedChoice.DeliverToAgent:
                    var settlement = MobileParty.MainParty.CurrentSettlement;
                    if (settlement?.IsFortification != true || settlement.OwnerClan != giver.Clan) return;
                    Deliver(quest, settlement.Party);
                    break;
            }
        }
    }

    public void RecordPendingCaptures(Player player, TroopRosterData prisoners)
    {
        if (ModInformation.IsClient) return;
        ClearPlayerPendingCaptures(player.ControllerId);
        if (prisoners.Data == null) return;

        foreach (var quest in Campaign.Current.QuestManager.Quests.OfType<Quest>())
        {
            if (!quest.IsOngoing || !ownership.TryGetOwnerControllerId(quest.QuestGiver, out var owner) ||
                owner != player.ControllerId || !generations.TryGetGeneration(quest.QuestGiver, out var generation) ||
                !objectManager.TryGetHandleWithLogging(quest._targetHero.CharacterObject, out var targetHandle) ||
                !prisoners.Data.Any(troop => troop.CharacterId == targetHandle && troop.Number > 0)) continue;

            // The server has allocated this captive; the dialogue outlives the MapEvent.
            pendingCaptures.Set(quest.QuestGiver, (quest, quest._targetHero, player, generation));
        }
    }

    private bool TryConsumePendingCapture(Quest quest, Player player, int generation)
    {
        if (!pendingCaptures.TryGet(quest.QuestGiver, out var pending) || pending.Quest != quest ||
            pending.Target != quest._targetHero || pending.Generation != generation ||
            pending.Owner.ControllerId != player.ControllerId || pending.Owner.HeroId != player.HeroId ||
            pending.Owner.MobilePartyId != player.MobilePartyId) return false;

        ClearPendingCapture(quest.QuestGiver);
        return true;
    }

    public void ClearPendingCapture(Hero giver) => pendingCaptures.Clear(giver);

    public void ClearTargetPendingCaptures(Hero target)
    {
        foreach (var pending in pendingCaptures.Snapshot())
        {
            if (pending.Value.Target == target) pendingCaptures.Clear(pending.Key);
        }
    }

    public void ClearPlayerPendingCaptures(string controllerId)
    {
        foreach (var pending in pendingCaptures.Snapshot())
        {
            if (pending.Value.Owner.ControllerId == controllerId) pendingCaptures.Clear(pending.Key);
        }
    }

    private void Deliver(Quest quest, PartyBase receiver)
    {
        if (receiver == null || receiver == PartyBase.MainParty) return;

        GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, quest.RewardGold);
        ChangeRelationAction.ApplyPlayerRelation(quest._targetHero, -10);
        quest.RelationshipChangeWithQuestGiver = 15;
        GainRenownAction.Apply(Hero.MainHero, 6f);
        quest.AddLog(quest.PlayerDeliveredPrisonerQuestSuccessLogText);
        quest.CompleteQuestWithSuccess();
        TransferPrisonerAction.Apply(quest._targetHero.CharacterObject, PartyBase.MainParty, receiver);
    }

    private RivalCapturedQuestState CaptureState(Quest quest)
    {
        return new RivalCapturedQuestState(quest._firstCounterOfferMade, quest.RelationshipChangeWithQuestGiver,
            quest.JournalEntries.Select(log => new RivalCapturedLog(log)).ToArray());
    }

    public void SendProgress(Quest quest)
    {
        if (!ownership.TryGetOwnerControllerId(quest.QuestGiver, out var controllerId) ||
            !playerManager.TryGetPeer(controllerId, out var peer) ||
            !objectManager.TryGetIdWithLogging(quest.QuestGiver, out var giverId) ||
            !generations.TryGetGeneration(quest.QuestGiver, out var generation)) return;

        network.Send(peer, new NetworkRivalCapturedProgress(giverId, generation, CaptureState(quest)));
    }

    public void MirrorProgress(NetworkRivalCapturedProgress data)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(data.GiverId, out var giver) ||
            !ownership.IsLocalPeerOwner(giver) ||
            !generations.TryGetGeneration(giver, out var generation) || generation != data.Generation ||
            giver.Issue?.IssueQuest is not Quest quest || !quest.IsOngoing) return;

        ApplyState(quest, data.State);
    }

    private void SendTraitProgress(Player player, int honorXp)
    {
        if (playerManager.TryGetPeer(player.ControllerId, out var peer))
            network.Send(peer, new NetworkRivalCapturedTraitProgress(player.HeroId, honorXp));
    }

    public void MirrorTraitProgress(NetworkRivalCapturedTraitProgress data)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(data.HeroId, out var hero) || hero != Hero.MainHero) return;
        Campaign.Current.PlayerTraitDeveloper.SetPropertyValue(DefaultTraits.Honor, data.HonorXp);
        StoreHonorProgress(hero, data.HonorXp);
    }

    public void PrepareAcceptance(Hero giver)
    {
        if (ModInformation.IsServer || giver?.Issue is not Issue || !giver.Issue.IsOngoingWithoutQuest) return;
        RequestTraitProgress(giver, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor), true);
    }

    public bool TryCaptureFrameworkQuestFields(Hero giver, out RivalCapturedAcceptFields fields)
    {
        fields = default;
        if (giver?.Issue is not Issue issue || issue.IssueQuest is not Quest quest || !quest.IsOngoing ||
            !TryGetCurrentPlayer(out var player) || !generations.TryGetGeneration(giver, out var generation))
            return false;

        fields = new RivalCapturedAcceptFields(player.ControllerId, generation, quest.QuestDueTime, CaptureState(quest));
        return true;
    }

    public void ApplyFrameworkQuestAcceptance(Issue issue, RivalCapturedAcceptFields fields)
    {
        if (issue == null || !generations.TryGetGeneration(issue.IssueOwner, out var generation))
            throw new InvalidOperationException("The rival-captured acceptance has no issue generation");

        if (generation != fields.Generation && ModInformation.IsServer)
            throw new InvalidOperationException("The rival-captured acceptance belongs to a stale issue generation");

        if (generation != fields.Generation)
            generations.SetGeneration(issue.IssueOwner, fields.Generation);

        if (ModInformation.IsServer)
        {
            if (issue.IssueQuest is Quest quest)
            {
                if (quest.IsOngoing && !Campaign.Current.QuestManager.Quests.Contains(quest))
                {
                    using (new QuestSolutionStartAuthorityGuard())
                        quest.QuestAcceptedConsequences();
                }

                quest.ChangeQuestDueTime(fields.DueTime);
                ApplyState(quest, fields.State);
            }
            return;
        }

        if (fields.ControllerId == controllerIdProvider.ControllerId)
            return;

        issue._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
        issue.IsTriedToSolveBefore = true;
        issue.IssueDueTime = CampaignTime.Never;
    }

    public void RequestTraitChange(int honorXp)
    {
        if (GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress.TryGet(Hero.MainHero, out var progress) &&
            progress.HasProperty(DefaultTraits.Honor))
            RequestTraitProgress(null, honorXp, false);
    }

    private void RequestTraitProgress(Hero giver, int honorXp, bool isBaseline)
    {
        if (!objectManager.TryGetIdWithLogging(Hero.MainHero, out var heroId)) return;
        string giverId = null;
        var generation = 0;
        if (isBaseline && (!objectManager.TryGetIdWithLogging(giver, out giverId) ||
            !generations.TryGetGeneration(giver, out generation))) return;
        StoreHonorProgress(Hero.MainHero, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor));
        network.SendAll(new RequestRivalCapturedTraitProgress(giverId, generation, honorXp, isBaseline, heroId));
    }

    public void ApplyTraitProgress(Player player, RequestRivalCapturedTraitProgress data)
    {
        if (data.HeroId != player.HeroId ||
            !objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero)) return;

        var registry = GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress;
        if (data.IsBaseline)
        {
            if (objectManager.TryGetObjectWithLogging<Hero>(data.GiverId, out var giver) && giver.Issue is Issue issue &&
                generations.TryGetGeneration(giver, out var generation) && generation == data.Generation &&
                issue.IsOngoingWithoutQuest && IsPresentWithGiver(player.ControllerId, giver))
            {
                if (registry.TryGet(hero, out var progress) && progress.HasProperty(DefaultTraits.Honor))
                    SendTraitProgress(player, progress.GetPropertyValue(DefaultTraits.Honor));
                else StoreHonorProgress(hero, data.HonorXp);
            }
            return;
        }

        // A personal XP change can already be in flight when its quest ends.
        if (!registry.TryGet(hero, out var knownProgress) || !knownProgress.HasProperty(DefaultTraits.Honor) ||
            !TryEnterOwnerScope(player, out var scope)) return;
        using (scope) TraitLevelingHelper.AddTraitXp(DefaultTraits.Honor, data.HonorXp);
    }

    private void StoreHonorProgress(Hero hero, int honorXp)
    {
        var registry = GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress;
        if (!registry.TryGet(hero, out var progress))
        {
            progress = new PropertyOwner<PropertyObject>();
            registry.Set(hero, progress);
        }
        // Keep an explicit zero so a stale acceptance baseline cannot replace known progress.
        progress._attributes[DefaultTraits.Honor] = honorXp;
    }

    public void RestoreLocalTraitProgress(Hero hero)
    {
        if (hero != null && hero == Hero.MainHero &&
            GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress.TryGet(hero, out var progress) &&
            progress.GetProperties().Contains(DefaultTraits.Honor))
            Campaign.Current.PlayerTraitDeveloper.SetPropertyValue(DefaultTraits.Honor, progress.GetPropertyValue(DefaultTraits.Honor));
    }

    public void CancelPlayerQuests(string controllerId)
    {
        ClearPlayerPendingCaptures(controllerId);
        if (Campaign.Current?.QuestManager == null) return;

        // Cancellation has no player reward and must also clean up a registration whose hero is gone.
        using (new IssueFinalizeAuthorityGuard())
        {
            foreach (var quest in Campaign.Current.QuestManager.Quests.OfType<Quest>().ToArray())
            {
                if (quest.IsOngoing && ownership.TryGetOwnerControllerId(quest.QuestGiver, out var owner) && owner == controllerId)
                    quest.CompleteQuestWithCancel(new TextObject("{=bYdhYidf}The quest was canceled because your clan leader, who made the original agreement, is no longer head of the clan.\""));
            }
        }
    }

    private void ApplyState(Quest quest, RivalCapturedQuestState state)
    {
        quest._firstCounterOfferMade = state.FirstCounterOfferMade;
        quest.RelationshipChangeWithQuestGiver = state.GiverRelationChange;
        quest._journalEntries.Clear();
        foreach (var log in state.Logs ?? Array.Empty<RivalCapturedLog>())
            quest._journalEntries.Add(new JournalLog(log.Time, log.Text));
        CampaignEventDispatcher.Instance.OnQuestLogAdded(quest, true);
    }

    public void CheckCancellation(Quest quest, bool causedByPlayer)
    {
        if (quest.QuestGiver.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction))
        {
            if (causedByPlayer) quest.CompleteQuestWithFail(quest.WarDeclaredQuestLog);
            else quest.CompleteQuestWithCancel(quest.WarDeclaredQuestLog);
        }
        else if (quest.QuestGiver.MapFaction == quest._targetHero.MapFaction)
            quest.CompleteQuestWithCancel(quest.TargetHeroAndQuestGiverInSameFaction);
        else if (quest.QuestGiver.MapFaction != Hero.MainHero.MapFaction)
            quest.CompleteQuestWithCancel(quest.PlayerAndQuestGiverNotInSameFaction);
    }

    public void OnBattleWon(MapEvent mapEvent)
    {
        if (!mapEvent.HasWinner) return;

        foreach (var quest in Campaign.Current.QuestManager.Quests.OfType<Quest>().ToArray())
        {
            if (!quest.IsOngoing || !TryEnterOwnerScope(quest, out var scope)) continue;
            using (scope)
            {
                if (!mapEvent.PartiesOnSide(mapEvent.WinningSide).Any(party => party.Party == PartyBase.MainParty) ||
                    !mapEvent.PartiesOnSide(mapEvent.GetOtherSide(mapEvent.WinningSide)).Any(party => party.Party.Owner == quest._targetHero) ||
                    quest._targetHero.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction)) continue;

                ChangeRelationAction.ApplyPlayerRelation(quest._targetHero.MapFaction.Leader, -10);
                DeclareWarAction.ApplyByPlayerHostility(Hero.MainHero.MapFaction, quest._targetHero.MapFaction);
            }
        }
    }

    public void RemoveOtherPlayersQuests(QuestManager manager)
    {
        foreach (var quest in manager.Quests.OfType<Quest>().ToArray())
        {
            if (ownership.IsLocalPeerOwner(quest.QuestGiver)) continue;
            manager.RemoveAllTrackedObjectsForQuest(quest);
            manager._quests.Remove(quest);
            if (quest.QuestGiver.Issue?.IssueQuest == quest) quest.QuestGiver.Issue.IssueQuest = null;
        }
    }
}
