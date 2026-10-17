using Common;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using System;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest;

namespace GameInterface.Services.Issues.Quests.LordsNeedsTutor;

internal sealed class LordsNeedsTutorQuestSolutionAcceptStrategy : IQuestSolutionAcceptStrategy
{
    private readonly IIssueOwnerResolver ownerResolver;
    public LordsNeedsTutorQuestSolutionAcceptStrategy(IIssueOwnerResolver ownerResolver) => this.ownerResolver = ownerResolver;

    public byte[] Capture(IssueBase issue) => issue.IssueQuest is Quest quest
        ? BitConverter.GetBytes(quest._randomForQuestReward) : Array.Empty<byte>();

    public void Apply(IssueBase issue, byte[] captured)
    {
        if (issue.IssueQuest is not Quest quest) return;
        if (captured.Length != sizeof(int)) throw new ArgumentException("Missing Tutor reward", nameof(captured));
        var reward = BitConverter.ToInt32(captured, 0);
        if (reward < 2 || reward > 4) throw new ArgumentException("Invalid Tutor reward", nameof(captured));
        quest._randomForQuestReward = reward;
        if (ModInformation.IsServer && quest._startQuestLog == null)
        {
            if (!ownerResolver.TryResolveOwnerHero(issue, out var owner)) throw new InvalidOperationException("Tutor quest has no owner");
            using (new MainHeroSubstitutionScope(owner, owner.PartyBelongedTo)) quest.QuestAcceptedConsequences();
        }
    }
}
