using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using System;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest;

namespace GameInterface.Services.Issues.Quests.VillageNeedsTools;

internal interface IVillageNeedsToolsService
{
    IDisposable OpenOwnerScope(Quest quest);
}

internal class VillageNeedsToolsService : IVillageNeedsToolsService
{
    private readonly IIssueOwnerResolver ownerResolver;

    public VillageNeedsToolsService(IIssueOwnerResolver ownerResolver)
    {
        this.ownerResolver = ownerResolver;
    }

    public IDisposable OpenOwnerScope(Quest quest)
    {
        var issue = IssueManager.GetIssueOfQuest(quest);
        if (issue == null || !ownerResolver.TryResolveOwnerHero(issue, out var ownerHero)) return null;

        return new MainHeroSubstitutionScope(ownerHero, ownerHero.PartyBelongedTo);
    }
}
