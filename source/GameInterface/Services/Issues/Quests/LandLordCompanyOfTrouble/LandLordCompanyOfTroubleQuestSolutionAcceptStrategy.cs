using Common;
using GameInterface.Services.Issues.Framework.Interface;
using System;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest;

namespace GameInterface.Services.Issues.Quests.LandLordCompanyOfTrouble;

internal class LandLordCompanyOfTroubleQuestSolutionAcceptStrategy : IQuestSolutionAcceptStrategy
{
    private readonly IIssueOwnerResolver ownerResolver;

    public LandLordCompanyOfTroubleQuestSolutionAcceptStrategy(IIssueOwnerResolver ownerResolver)
    {
        this.ownerResolver = ownerResolver;
    }

    public byte[] Capture(IssueBase issue) => Array.Empty<byte>();

    public void Apply(IssueBase issue, byte[] captured)
    {
        if (ModInformation.IsClient || !(issue.IssueQuest is Quest quest))
        {
            return;
        }

        if (!ownerResolver.TryResolveOwnerHero(issue, out var ownerHero) || ownerHero.PartyBelongedTo == null)
        {
            throw new InvalidOperationException($"{issue.StringId} was accepted without an owning party to give the mercenaries to");
        }

        ownerHero.PartyBelongedTo.MemberRoster.AddToCounts(quest._troubleCharacterObject, quest._companyTroopCount);
    }
}
