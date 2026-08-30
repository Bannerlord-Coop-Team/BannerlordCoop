using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.Heroes.HeirSelection.Interfaces;

public interface IHeirSelectionCampaignBehaviorInterface : IGameAbstraction
{
    void OnBeforePlayerCharacterChanged(Hero oldPlayerHero, Hero newPlayerHero);
    void OnPlayerCharacterChanged(Hero oldPlayerHero, Hero newPlayerHero, MobileParty newPlayerParty, bool isPartyChanged);
}

public class HeirSelectionCampaignBehaviorInterface : IHeirSelectionCampaignBehaviorInterface
{
    private readonly Dictionary<Hero, ItemRoster> playerHeroItemsThatWillBeInherited;

    private readonly Dictionary<Hero, ItemRoster> playerHeroEquipmentsThatWillBeInherited;

    public HeirSelectionCampaignBehaviorInterface()
    {
        playerHeroItemsThatWillBeInherited = new();
        playerHeroEquipmentsThatWillBeInherited = new();
    }

    public void OnBeforePlayerCharacterChanged(Hero oldPlayerHero, Hero newPlayerHero)
    {
        playerHeroItemsThatWillBeInherited[oldPlayerHero] = new();
        playerHeroEquipmentsThatWillBeInherited[oldPlayerHero] = new();

        foreach (ItemRosterElement itemRosterElement in MobileParty.MainParty.ItemRoster)
        {
            playerHeroItemsThatWillBeInherited[oldPlayerHero].Add(itemRosterElement);
        }
        for (int i = 0; i < 12; i++)
        {
            if (!oldPlayerHero.BattleEquipment[i].IsEmpty)
            {
                playerHeroEquipmentsThatWillBeInherited[oldPlayerHero].AddToCounts(oldPlayerHero.BattleEquipment[i], 1);
            }
            if (!oldPlayerHero.CivilianEquipment[i].IsEmpty)
            {
                playerHeroEquipmentsThatWillBeInherited[oldPlayerHero].AddToCounts(oldPlayerHero.CivilianEquipment[i], 1);
            }
        }
    }

    public void OnPlayerCharacterChanged(Hero oldPlayerHero, Hero newPlayerHero, MobileParty newPlayerParty, bool isPartyChanged)
    {
        if (!playerHeroItemsThatWillBeInherited.ContainsKey(oldPlayerHero)) return;
        if (!playerHeroEquipmentsThatWillBeInherited.ContainsKey(oldPlayerHero)) return;

        if (isPartyChanged)
        {
            newPlayerParty.ItemRoster.Add(playerHeroItemsThatWillBeInherited[oldPlayerHero]);
        }
        newPlayerParty.ItemRoster.Add(playerHeroEquipmentsThatWillBeInherited[oldPlayerHero]);

        playerHeroItemsThatWillBeInherited.Remove(oldPlayerHero);
        playerHeroEquipmentsThatWillBeInherited.Remove(oldPlayerHero);
    }
}