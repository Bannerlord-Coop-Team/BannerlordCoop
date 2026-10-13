using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Players.Interfaces;

public interface IPlayerPartyLookup : IGameAbstraction
{
    bool TryGetPlayerParty(Hero hero, out MobileParty playerParty);

    bool TryGetPlayerHero(MobileParty playerParty, out Hero playerHero);
}

public class PlayerPartyLookup : IPlayerPartyLookup
{
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;

    public PlayerPartyLookup(IObjectManager objectManager, IPlayerManager playerManager)
    {
        this.objectManager = objectManager;
        this.playerManager = playerManager;
    }

    public bool TryGetPlayerParty(Hero hero, out MobileParty playerParty)
    {
        playerParty = null;

        if (!objectManager.TryGetId(hero, out var heroId))
            return false;

        foreach (var player in playerManager.Players)
        {
            if (player.HeroId == heroId)
                return objectManager.TryGetObject(player.MobilePartyId, out playerParty);
        }

        return false;
    }

    public bool TryGetPlayerHero(MobileParty playerParty, out Hero playerHero)
    {
        playerHero = null;

        if (!objectManager.TryGetId(playerParty, out var mobilePartyId)) return false;

        foreach (var player in playerManager.Players)
        {
            if (player.MobilePartyId == mobilePartyId)
            {
                return objectManager.TryGetObject(player.HeroId, out playerHero);
            }   
        }

        return false;
    }
}
