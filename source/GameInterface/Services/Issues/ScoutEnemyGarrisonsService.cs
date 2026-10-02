using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Dispatch;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues;

using Issue = ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue;
using Quest = ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsQuest;

internal interface IScoutEnemyGarrisonsService
{
    void PublishIssue(Issue issue, bool created);
    void ApplyIssue(NetworkScoutEnemyGarrisonsIssue data);
    void Accept(Hero giver);
    bool CaptureAcceptance(Hero giver, out ScoutEnemyGarrisonsAccept data);
    void MirrorAcceptance(Hero giver, ScoutEnemyGarrisonsAccept data);
    IDisposable OpenAuthority(Quest quest, bool requireCurrentParty = true);
    void PublishProgress(Quest quest);
    void ApplyProgress(NetworkScoutEnemyGarrisonsProgress data);
    bool HasPersonalQuest(Hero hero);
    void RejectAcceptance(string giverId);
    void CancelReplacedHeroQuests(Hero heir);
}

internal sealed class ScoutEnemyGarrisonsService : IScoutEnemyGarrisonsService
{
    private readonly IObjectManager objects;
    private readonly INetwork network;
    private readonly IIssueGenerationRegistry generations;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IPlayerManager players;
    private readonly IScoutEnemyGarrisonsQuestState state;

    public ScoutEnemyGarrisonsService(IObjectManager objects, INetwork network,
        IIssueGenerationRegistry generations, IIssueOwnershipRegistry ownership,
        IPlayerManager players, IScoutEnemyGarrisonsQuestState state)
    {
        this.objects = objects;
        this.network = network;
        this.generations = generations;
        this.ownership = ownership;
        this.players = players;
        this.state = state;
    }

    public void PublishIssue(Issue issue, bool created)
    {
        if (!ModInformation.IsServer || !objects.TryGetIdWithLogging(issue.IssueOwner, out var giverId)) return;
        if (!TryCaptureTargets(issue, out var ids)) return;
        if (created) generations.Bump(issue.IssueOwner);
        if (!generations.TryGetGeneration(issue.IssueOwner, out var generation)) return;
        network.SendAll(new NetworkScoutEnemyGarrisonsIssue(giverId, generation, ids, issue.IssueDueTime, issue.StringId));
    }

    public void ApplyIssue(NetworkScoutEnemyGarrisonsIssue data)
    {
        if (string.IsNullOrEmpty(data.IssueId)) return;
        if (!objects.TryGetObjectWithLogging<Hero>(data.GiverId, out var giver)) return;
        if (!TryResolveTargets(data.TargetIds, out var targets)) return;
        if (generations.TryGetGeneration(giver, out var current) && current > data.Generation) return;
        if (giver.Issue != null && (giver.Issue is not Issue || current != data.Generation)) return;

        using (new AllowedThread())
        {
            if (giver.Issue == null)
            {
                PotentialIssueData.StartIssueDelegate factory = (in PotentialIssueData _, Hero owner) => new Issue(owner, targets);
                var potential = new PotentialIssueData(factory, typeof(Issue), IssueBase.IssueFrequency.VeryCommon);
                Campaign.Current.IssueManager.CreateNewIssue(in potential, giver);
            }
            if (giver.Issue is not Issue issue || !issue.IsOngoingWithoutQuest) return;
            issue.StringId = data.IssueId;
            SetTargets(issue, targets);
            issue.IssueDueTime = data.DueTime;
            generations.SetGeneration(giver, data.Generation);
        }
    }

    public void Accept(Hero giver)
    {
        if (giver.Issue is not Issue issue || !issue.IsOngoingWithoutQuest || !issue.CheckPreconditions(giver, out _)) return;
        if (!TryCaptureTargets(issue, out _)) return;
        if (!objects.TryGetIdWithLogging(Hero.MainHero, out var heroId)) return;
        var player = players.Players.SingleOrDefault(p => p.HeroId == heroId);
        if (player == null || !objects.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)) return;

        using (new IssueDispatchReplayGuard())
        {
            if (!Campaign.Current.IssueManager.StartIssueQuest(giver) || issue.IssueQuest is not Quest quest) return;
            state.Remember(quest, player.ControllerId, Hero.MainHero, party);
            ownership.SetOwner(giver, player.ControllerId);
            quest.QuestAcceptedConsequences();
        }
    }

    public bool CaptureAcceptance(Hero giver, out ScoutEnemyGarrisonsAccept data)
    {
        data = default;
        if (giver.Issue is not Issue issue || issue.IssueQuest is not Quest quest || !quest.IsOngoing) return false;
        if (!state.TryGet(quest, out var owner) || !TryCaptureTargets(issue, out var ids)) return false;
        if (!objects.TryGetIdWithLogging(owner.Hero, out var heroId) ||
            !objects.TryGetIdWithLogging(owner.Party, out var partyId)) return false;
        data = new ScoutEnemyGarrisonsAccept(owner.ControllerId, heroId, partyId, ids, quest.QuestDueTime, issue.StringId);
        return true;
    }

    public void MirrorAcceptance(Hero giver, ScoutEnemyGarrisonsAccept data)
    {
        var accepted = false;
        try
        {
            if (giver.Issue is not Issue issue || !issue.IsOngoingWithoutQuest || issue.StringId != data.IssueId) return;
            if (!objects.TryGetObjectWithLogging<Hero>(data.HeroId, out var hero) ||
                !objects.TryGetObjectWithLogging<MobileParty>(data.PartyId, out var party) ||
                !TryResolveTargets(data.TargetIds, out var targets)) return;

            using (new AllowedThread())
            using (new QuestSolutionStartAuthorityGuard())
            using (new IssueDispatchReplayGuard())
            using (new MainHeroSubstitutionScope(hero, party))
            {
                SetTargets(issue, targets);
                if (!Campaign.Current.IssueManager.StartIssueQuest(giver) || issue.IssueQuest is not Quest quest) return;
                state.Remember(quest, data.ControllerId, hero, party);
                ownership.SetOwner(giver, data.ControllerId);
                quest.ChangeQuestDueTime(data.DueTime);
                quest.QuestAcceptedConsequences();
                if (state.IsVisible(quest)) quest.AddDialogs();
                accepted = state.IsVisible(quest);
            }
        }
        finally
        {
            FinishAcceptance(giver, accepted);
        }
    }

    public IDisposable OpenAuthority(Quest quest, bool requireCurrentParty = true)
    {
        if (!state.TryGet(quest, out var owner) || owner.Hero == null) return null;
        if (players.TryGetPlayer(owner.ControllerId, out var player) &&
            objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero) && hero == owner.Hero)
        {
            if (objects.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)) owner.Party = party;
            else if (requireCurrentParty) return null;
        }
        else if (requireCurrentParty) return null;
        return owner.Party == null ? null : new AuthorityScope(owner.Hero, owner.Party);
    }

    public void RejectAcceptance(string giverId)
    {
        if (objects.TryGetObjectWithLogging<Hero>(giverId, out var giver)) FinishAcceptance(giver, false);
    }

    private void FinishAcceptance(Hero giver, bool accepted)
    {
        var pending = state.PendingAcceptance;
        if (pending?.IssueOwner != giver) return;
        state.PendingAcceptance = null;
        var conversation = Campaign.Current.ConversationManager;
        if (!conversation.IsConversationInProgress || conversation.OneToOneConversationHero != giver ||
            conversation.ActiveToken != conversation.GetStateIndex("issue_classic_quest_start")) return;
        if (accepted && giver.Issue == pending) conversation.DoOptionContinue();
        else conversation.EndConversation();
    }

    public void CancelReplacedHeroQuests(Hero heir)
    {
        if (!ModInformation.IsServer || !objects.TryGetIdWithLogging(heir, out var heroId)) return;
        var player = players.Players.SingleOrDefault(player => player.HeroId == heroId);
        if (player == null) return;
        foreach (var quest in Campaign.Current.QuestManager._quests.OfType<Quest>().ToArray())
        {
            if (quest.IsOngoing && state.TryGet(quest, out var owner) &&
                owner.ControllerId == player.ControllerId && owner.Hero != heir)
                quest.CompleteQuestWithCancel(new TextObject("{=bYdhYidf}The quest was canceled because your clan leader, who made the original agreement, is no longer head of the clan.\""));
        }
    }

    public void PublishProgress(Quest quest)
    {
        if (!ModInformation.IsServer || !state.TryGet(quest, out var owner)) return;
        if (!objects.TryGetIdWithLogging(quest.QuestGiver, out var giverId) ||
            !generations.TryGetGeneration(quest.QuestGiver, out var generation)) return;
        state.Capture(quest);
        network.SendAll(new NetworkScoutEnemyGarrisonsProgress(giverId, generation, quest.StringId,
            new[] { quest._questSettlement1.CurrentScoutProgress, quest._questSettlement2.CurrentScoutProgress,
                quest._questSettlement3.CurrentScoutProgress }, owner.NeutralTargets, quest._scoutedSettlementCount,
            quest.JournalEntries.Select(log => new ScoutEnemyGarrisonsLog(log)).ToArray(), quest.RelationshipChangeWithQuestGiver));
    }

    public void ApplyProgress(NetworkScoutEnemyGarrisonsProgress data)
    {
        if (data.Hours?.Length != 3 || data.Logs == null) return;
        if (!objects.TryGetObjectWithLogging<Hero>(data.GiverId, out var giver) ||
            !generations.TryGetGeneration(giver, out var generation) || generation != data.Generation ||
            giver.Issue?.IssueQuest is not Quest quest || quest.StringId != data.QuestId || !quest.IsOngoing ||
            !state.TryGet(quest, out var owner)) return;

        using (new AllowedThread())
        {
            var targets = new[] { quest._questSettlement1, quest._questSettlement2, quest._questSettlement3 };
            for (var i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                if (data.Hours[i] == 1 && target.CurrentScoutProgress != 1 && state.IsVisible(quest))
                {
                    var text = new TextObject("{=qfjRGjM4}Your scouts started to gather information about {SETTLEMENT}.");
                    text.SetTextVariable("SETTLEMENT", target.Settlement.Name);
                    MBInformationManager.AddQuickInformation(text);
                }
                target.CurrentScoutProgress = data.Hours[i];
                if (target.IsScoutingCompleted() && quest.IsTracked(target.Settlement)) quest.RemoveTrackedObject(target.Settlement);
            }
            owner.NeutralTargets = data.NeutralTargets;
            state.Restore(quest);
            quest._scoutedSettlementCount = data.ScoutedCount;
            quest.RelationshipChangeWithQuestGiver = data.RelationChange;
            for (var i = 0; i < data.Logs.Length; i++)
            {
                var log = data.Logs[i];
                if (i < quest.JournalEntries.Count)
                {
                    quest.JournalEntries[i].UpdateCurrentProgress(log.Progress);
                    continue;
                }
                quest._journalEntries.Add(new JournalLog(log.Time, log.Text, log.Task, log.Progress, log.Range, (LogType)log.Type));
                CampaignEventDispatcher.Instance.OnQuestLogAdded(quest, false);
            }
        }
    }

    public bool HasPersonalQuest(Hero hero) => Campaign.Current.QuestManager._quests.OfType<Quest>()
        .Any(quest => quest.IsOngoing && state.TryGet(quest, out var owner) && owner.Hero == hero);

    private bool TryCaptureTargets(Issue issue, out string[] ids)
    {
        ids = new string[3];
        return objects.TryGetIdWithLogging(issue._settlement1, out ids[0]) &&
            objects.TryGetIdWithLogging(issue._settlement2, out ids[1]) &&
            objects.TryGetIdWithLogging(issue._settlement3, out ids[2]);
    }

    private bool TryResolveTargets(string[] ids, out List<Settlement> targets)
    {
        targets = new List<Settlement>();
        if (ids?.Length != 3) return false;
        foreach (var id in ids)
        {
            if (!objects.TryGetObjectWithLogging<Settlement>(id, out var settlement)) return false;
            targets.Add(settlement);
        }
        return true;
    }

    private void SetTargets(Issue issue, List<Settlement> targets)
    {
        issue._settlement1 = targets[0];
        issue._settlement2 = targets[1];
        issue._settlement3 = targets[2];
    }

    private sealed class AuthorityScope : IDisposable
    {
        private readonly MainHeroSubstitutionScope player;
        private readonly IssueFinalizeAuthorityGuard finalization;

        public AuthorityScope(Hero hero, MobileParty party)
        {
            player = new MainHeroSubstitutionScope(hero, party);
            finalization = new IssueFinalizeAuthorityGuard();
        }

        public void Dispose()
        {
            finalization.Dispose();
            player.Dispose();
        }
    }
}
