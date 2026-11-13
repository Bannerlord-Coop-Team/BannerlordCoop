using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Framework.Dispatch;

internal class IssueManagerLifecycleService : IIssueManagerLifecycleService
{
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;

    public IssueManagerLifecycleService(
        IObjectManager objectManager,
        IPlayerManager playerManager)
    {
        this.objectManager = objectManager;
        this.playerManager = playerManager;
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
