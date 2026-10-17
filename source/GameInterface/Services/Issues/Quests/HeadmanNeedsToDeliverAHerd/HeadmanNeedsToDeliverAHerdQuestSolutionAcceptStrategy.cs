using Common;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using System;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsToDeliverAHerd;

internal class HeadmanNeedsToDeliverAHerdQuestSolutionAcceptStrategy : IQuestSolutionAcceptStrategy
{
    private readonly IIssueOwnerResolver owners;
    private readonly IHeadmanHerdCleanup cleanup;

    public HeadmanNeedsToDeliverAHerdQuestSolutionAcceptStrategy(IIssueOwnerResolver owners, IHeadmanHerdCleanup cleanup)
    {
        this.owners = owners;
        this.cleanup = cleanup;
    }

    public byte[] Capture(IssueBase issue) => Array.Empty<byte>();

    public void Apply(IssueBase issue, byte[] captured)
    {
        if (ModInformation.IsClient || !(issue.IssueQuest is Quest quest)) return;
        if (!owners.TryResolveOwnerHero(issue, out var owner))
            throw new InvalidOperationException("The accepted herd has no owning player");

        using (new MainHeroSubstitutionScope(owner, owner.PartyBelongedTo))
        {
            cleanup.Remember(issue, owner.PartyBelongedTo);
            quest.AddHerdAndShepherdsToMainParty();
        }
    }
}
