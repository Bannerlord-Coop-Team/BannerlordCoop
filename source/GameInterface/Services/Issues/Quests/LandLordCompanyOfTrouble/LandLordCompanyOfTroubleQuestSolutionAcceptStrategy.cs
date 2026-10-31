using Common;
using Common.Logging;
using GameInterface.Services.Issues.Framework.Interface;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest;

namespace GameInterface.Services.Issues.Quests.LandLordCompanyOfTrouble;

internal class LandLordCompanyOfTroubleQuestSolutionAcceptStrategy : IQuestSolutionAcceptStrategy
{
    private static readonly ILogger Logger = LogManager.GetLogger<LandLordCompanyOfTroubleQuestSolutionAcceptStrategy>();

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

        if (!ownerResolver.TryResolveOwnerHero(issue, out var ownerHero))
        {
            Logger.Error("{issue} was accepted but has no owning player to give the mercenaries to", issue.StringId);
            return;
        }

        ownerHero.PartyBelongedTo.MemberRoster.AddToCounts(quest._troubleCharacterObject, quest._companyTroopCount);
    }
}
