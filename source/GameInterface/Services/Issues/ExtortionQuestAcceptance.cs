using Common;
using Common.Util;
using GameInterface.Services.Issues.Data;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Generic.Dispatch;
using GameInterface.Services.ObjectManager;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues;

using Issue = ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssue;
using Quest = ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;

internal interface IExtortionQuestAcceptance : IRaceArbitratedAcceptMirrorStrategy<ExtortionQuestAcceptFields>,
    IAlternativeAcceptMirrorStrategy<ExtortionAlternativeAcceptFields>
{
}

internal sealed class ExtortionQuestAcceptance : IExtortionQuestAcceptance
{
    private readonly IObjectManager objectManager;
    private readonly IIssueOwnershipRegistry ownershipRegistry;

    public ExtortionQuestAcceptance(IObjectManager objectManager, IIssueOwnershipRegistry ownershipRegistry)
    {
        if (objectManager == null) throw new ArgumentNullException(nameof(objectManager));
        if (ownershipRegistry == null) throw new ArgumentNullException(nameof(ownershipRegistry));
        this.objectManager = objectManager;
        this.ownershipRegistry = ownershipRegistry;
    }

    public void ReplayQuestAccepted(Hero owner)
    {
        if (ModInformation.IsClient || owner?.Issue is not Issue issue || !issue.IsOngoingWithoutQuest) return;
        if (!issue.CheckPreconditions(owner, out _)) return;

        using (new IssueDispatchReplayGuard())
        {
            Campaign.Current.IssueManager.StartIssueQuest(owner);
            if (issue.IssueQuest is Quest quest) quest.OnQuestAccepted();
        }
    }

    public bool TryCaptureQuestFields(Hero owner, out ExtortionQuestAcceptFields fields)
    {
        fields = default;
        if (owner?.Issue?.IssueQuest is not Quest quest || !quest.IsOngoing) return false;
        if (!objectManager.TryGetIdWithLogging(Hero.MainHero, out var playerHeroId)) return false;
        if (!objectManager.TryGetIdWithLogging(quest._deserterMobileParty, out var deserterPartyId)) return false;

        fields = new ExtortionQuestAcceptFields(playerHeroId, deserterPartyId, quest.StringId,
            quest._questDifficultyMultiplier, quest.RewardGold, quest.QuestDueTime);
        return true;
    }

    public void MirrorQuestAccepted(Hero owner, ExtortionQuestAcceptFields fields)
    {
        if (ModInformation.IsServer || owner?.Issue is not Issue issue || !issue.IsOngoingWithoutQuest) return;
        if (!objectManager.TryGetObjectWithLogging<Hero>(fields.PlayerHeroId, out var playerHero) ||
            !objectManager.TryGetObjectWithLogging<MobileParty>(fields.DeserterPartyId, out var deserterParty))
            throw new InvalidOperationException("The accepted deserter quest's player or party is not registered.");

        using (new AllowedThread())
        {
            issue._issueDifficultyMultiplier = fields.Difficulty;
            issue._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
            issue.IsTriedToSolveBefore = true;
            issue.IssueDueTime = CampaignTime.Never;
            if (playerHero != Hero.MainHero) return;

            using (new ExtortionQuestMirrorScope(owner, deserterParty))
            {
                var quest = new Quest(fields.QuestId, owner, fields.Difficulty, fields.RewardGold, fields.DueTime);
                issue.IssueQuest = quest;
                quest.OnQuestAccepted();
            }
        }
    }

    public void ReplayAlternativeAccepted(Hero owner)
    {
        // The generic handler has already run the authoritative troop transfer and start.
        if (owner?.Issue is not Issue issue || !issue.IsSolvingWithAlternative)
            throw new InvalidOperationException("The deserter alternative solution has not started.");
        CampaignEventDispatcher.Instance.OnHeroGetsBusy(issue.AlternativeSolutionHero, HeroGetsBusyReasons.SolvesIssue);
    }

    public bool TryCaptureAlternativeFields(Hero owner, out ExtortionAlternativeAcceptFields fields)
    {
        fields = default;
        if (owner?.Issue is not Issue issue || !issue.IsSolvingWithAlternative) return false;
        fields = new ExtortionAlternativeAcceptFields(issue.IssueDifficultyMultiplier,
            AlternativeSolutionVanillaStateSync.Capture(issue));
        return true;
    }

    public void MirrorAlternativeAccepted(Hero owner, ExtortionAlternativeAcceptFields fields)
    {
        if (ModInformation.IsServer || owner?.Issue is not Issue issue || !issue.IsSolvingWithAlternative) return;
        using (new AllowedThread())
        {
            issue._issueDifficultyMultiplier = fields.Difficulty;
            // The generic mirror already added a start log before applying this difficulty.
            issue._journalEntries.Clear();
            AlternativeSolutionVanillaStateSync.Apply(issue, fields.State);
        }
    }

    public void RejectAcceptance(Hero owner)
    {
        if (ModInformation.IsClient || owner?.Issue is not Issue issue || issue.IsOngoingWithoutQuest) return;
        if (ownershipRegistry.TryGetOwnerControllerId(owner, out _)) return;

        using (new IssueFinalizeAuthorityGuard())
        {
            issue.CompleteIssueWithCancel();
        }
    }

    // The generic handler returns authoritative troops; the client only selected a private roster.
    void IAlternativeAcceptMirrorStrategy<ExtortionAlternativeAcceptFields>.RejectAcceptance(Hero owner) { }
}

internal sealed class ExtortionQuestMirrorScope : IDisposable
{
    [ThreadStatic] private static ExtortionQuestMirrorScope current;
    private readonly ExtortionQuestMirrorScope previous;
    private readonly Hero owner;
    private readonly MobileParty deserterParty;
    private readonly MobileParty defenderParty;

    public static bool IsActive => current != null;

    public ExtortionQuestMirrorScope(Hero owner, MobileParty deserterParty, MobileParty defenderParty = null)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (deserterParty == null) throw new ArgumentNullException(nameof(deserterParty));
        previous = current;
        this.owner = owner;
        this.deserterParty = deserterParty;
        this.defenderParty = defenderParty;
        current = this;
    }

    public static void Apply(Quest quest)
    {
        if (current == null || current.owner != quest.QuestGiver)
            throw new InvalidOperationException("Deserter quest creation requires the server's party identity.");
        quest._deserterMobileParty = current.deserterParty;
    }

    public static void ApplyDefender(Quest quest)
    {
        if (current == null || current.owner != quest.QuestGiver || current.defenderParty == null)
            throw new InvalidOperationException("Deserter ambush requires the server's defender party identity.");
        quest._defenderMobileParty = current.defenderParty;
    }

    public void Dispose() => current = previous;
}
