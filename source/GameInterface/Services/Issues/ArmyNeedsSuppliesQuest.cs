using System;
using System.Linq;
using Common.Util;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Generic.Dispatch;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues;

using Issue = ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssue;
using Quest = ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssueQuest;

internal interface IArmyNeedsSuppliesQuest : IRaceArbitratedAcceptMirrorStrategy<ArmyNeedsSuppliesAcceptance>
{
    bool IsLocalOwner(Quest quest);
    bool IsOwner(Quest quest, Hero hero);
    bool TryOpenOwnerScope(Quest quest, out IDisposable scope);
}

internal sealed class ArmyNeedsSuppliesQuest : IArmyNeedsSuppliesQuest
{
    private readonly IObjectManager objects;
    private readonly IPlayerManager players;
    private readonly IIssueOwnershipRegistry owners;
    private readonly IArmyNeedsSuppliesJournalOwners journalOwners;

    public ArmyNeedsSuppliesQuest(IObjectManager objects, IPlayerManager players, IIssueOwnershipRegistry owners,
        IArmyNeedsSuppliesJournalOwners journalOwners)
    {
        this.objects = objects;
        this.players = players;
        this.owners = owners;
        this.journalOwners = journalOwners;
    }

    public bool IsLocalOwner(Quest quest) => owners.IsLocalPeerOwner(quest.QuestGiver);

    public bool IsOwner(Quest quest, Hero hero) => hero != null &&
        owners.TryGetOwnerControllerId(quest.QuestGiver, out var controllerId) &&
        players.TryGetPlayer(controllerId, out var player) &&
        objects.TryGetIdWithLogging(hero, out var heroId) && player.HeroId == heroId;

    public bool TryOpenOwnerScope(Quest quest, out IDisposable scope)
    {
        scope = null;
        if (!owners.TryGetOwnerControllerId(quest.QuestGiver, out var controllerId) ||
            !players.TryGetPlayer(controllerId, out var player) ||
            !objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero) ||
            !objects.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)) return false;

        scope = new MainHeroSubstitutionScope(hero, party);
        return true;
    }

    public void ReplayQuestAccepted(Hero owner)
    {
        if (owner?.Issue is not Issue issue || !issue.IsOngoingWithoutQuest) return;
        if (!issue.IssueStayAliveConditions() ||
            !issue.CanPlayerTakeQuestConditions(owner, out _, out _, out _, out _)) return;

        using (new IssueDispatchReplayGuard())
        {
            Campaign.Current.IssueManager.StartIssueQuest(owner);
            if (issue.IssueQuest is Quest quest) quest.QuestAcceptedConsequences();
        }
    }

    public bool TryCaptureQuestFields(Hero owner, out ArmyNeedsSuppliesAcceptance fields)
    {
        fields = null;
        if (owner?.Issue?.IssueQuest is not Quest quest || quest._grainLog == null ||
            !objects.TryGetIdWithLogging(Hero.MainHero, out var heroId)) return false;
        var player = players.Players.FirstOrDefault(candidate => candidate.HeroId == heroId);
        if (player == null) return false;
        journalOwners.SetOwner(Campaign.Current.GetCampaignBehavior<JournalLogsCampaignBehavior>().GetRelatedLog(quest),
            player.ControllerId);

        fields = new ArmyNeedsSuppliesAcceptance
        {
            ControllerId = player.ControllerId,
            Grain = quest._requestedGrainAmount,
            Livestock = quest._requestedLiveStockAmount,
            Wine = quest._requestedWineAmount,
            DueTime = quest.QuestDueTime,
            Journal = new ArmyNeedsSuppliesJournal(quest),
        };
        return true;
    }

    public void MirrorQuestAccepted(Hero owner, ArmyNeedsSuppliesAcceptance fields)
    {
        if (owner?.Issue is not Issue issue || fields == null ||
            !players.TryGetPlayer(fields.ControllerId, out var player) ||
            !objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero) ||
            !objects.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party))
            throw new InvalidOperationException("Cannot resolve the army supply quest's accepting player.");
        if (!issue.IsOngoingWithoutQuest) return;

        owners.SetOwner(owner, fields.ControllerId);
        using (new AllowedThread())
        using (new MainHeroSubstitutionScope(hero, party))
        using (new IssueDispatchReplayGuard())
        using (new QuestSolutionStartAuthorityGuard())
        {
            Campaign.Current.IssueManager.StartIssueQuest(owner);
            if (issue.IssueQuest is not Quest quest) return;
            quest._requestedGrainAmount = fields.Grain;
            quest._requestedLiveStockAmount = fields.Livestock;
            quest._requestedWineAmount = fields.Wine;
            quest.QuestDueTime = fields.DueTime;

            // Observers retain the issue identity without adding another player's personal quest.
            fields.Journal.Apply(quest);
            if (owners.IsLocalPeerOwner(owner))
            {
                quest.StartQuest();
                journalOwners.SetOwner(Campaign.Current.GetCampaignBehavior<JournalLogsCampaignBehavior>().GetRelatedLog(quest),
                    fields.ControllerId);
            }
        }
    }

    public void RejectAcceptance(Hero owner)
    {
        // Acceptance is deferred until the server reply, so rejection has no local quest to undo.
    }
}
