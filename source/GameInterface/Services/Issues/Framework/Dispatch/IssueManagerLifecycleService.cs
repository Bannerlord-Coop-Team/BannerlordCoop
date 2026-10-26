using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Framework.Dispatch;

internal class IssueManagerLifecycleService : IIssueManagerLifecycleService
{
    private readonly IIssueOwnerResolver ownerResolver;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;

    public IssueManagerLifecycleService(
        IIssueOwnerResolver ownerResolver,
        IObjectManager objectManager,
        IPlayerManager playerManager)
    {
        this.ownerResolver = ownerResolver;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
    }

    public bool TryOpenOwnerScope(IssueBase issue, out IDisposable scope)
    {
        scope = null;
        if (!ownerResolver.TryResolveOwnerHero(issue, out var ownerHero))
        {
            return false;
        }

        scope = new MainHeroSubstitutionScope(ownerHero, ownerHero.PartyBelongedTo);
        return true;
    }

    public bool TryResolveMainHeroParty(out PartyBase party)
    {
        party = null;
        if (!playerManager.TryGetPlayer(Hero.MainHero, out var player) ||
            !objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var ownerParty))
        {
            return false;
        }

        party = ownerParty.Party;
        return true;
    }
}
