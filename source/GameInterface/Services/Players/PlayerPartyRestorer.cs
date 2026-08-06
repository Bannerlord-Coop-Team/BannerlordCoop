using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Players;

public interface IPlayerPartyRestorer
{
    void Restore(Player player);
    void Restore(Hero hero, MobileParty party);
}

internal class PlayerPartyRestorer : IPlayerPartyRestorer
{
    private readonly IObjectManager objectManager;

    public PlayerPartyRestorer(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public void Restore(Player player)
    {
        if (!objectManager.TryGetObjectWithLogging(player.HeroId, out Hero hero)) return;
        if (!objectManager.TryGetObjectWithLogging(player.MobilePartyId, out MobileParty party)) return;

        Restore(hero, party);
    }

    public void Restore(Hero hero, MobileParty party)
    {
        // Transferred root references can be missing from clan, roster, and party-component state.
        if (!hero.Clan.Heroes.Contains(hero))
            hero.Clan.OnLordAdded(hero);

        if (party.MemberRoster.GetTroopCount(hero.CharacterObject) == 0)
            party.MemberRoster.AddToCounts(hero.CharacterObject, 1, insertAtFront: true);

        if (party.LeaderHero != hero)
            party.ChangePartyLeader(hero);
    }
}
