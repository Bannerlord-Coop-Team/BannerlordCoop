using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.Inventory.Interfaces;

public interface IDefaultItemDiscardModelInterface : IGameAbstraction
{
    /// <summary>
    /// Replace vanilla implementation to include a playerParty argument.
    /// Vanilla just uses the MainParty which doesn't work with multiple players.
    /// </summary>
    int GetXpBonusForDiscardingItem(MobileParty playerParty, ItemObject item, int amount = 1);
}

public class DefaultItemDiscardModelInterface : IDefaultItemDiscardModelInterface
{
    public int GetXpBonusForDiscardingItem(MobileParty playerParty, ItemObject item, int amount = 1)
    {
        if (!PlayerCanDonateItem(playerParty, item)) return 0;

        int xpBonus;
        switch (item.Tier)
        {
            case ItemObject.ItemTiers.Tier1:
                xpBonus = 75;
                break;
            case ItemObject.ItemTiers.Tier2:
                xpBonus = 150;
                break;
            case ItemObject.ItemTiers.Tier3:
                xpBonus = 250;
                break;
            case ItemObject.ItemTiers.Tier4:
            case ItemObject.ItemTiers.Tier5:
            case ItemObject.ItemTiers.Tier6:
                xpBonus = 300;
                break;
            default:
                xpBonus = 35;
                break;
        }
        return xpBonus * amount;
    }

    // Use playerParty to check for perks instead of MobileParty.MainParty
    private bool PlayerCanDonateItem(MobileParty playerParty, ItemObject item)
    {
        bool result = false;
        if (item.HasWeaponComponent)
        {
            result = playerParty.HasPerk(DefaultPerks.Steward.GivingHands, false);
        }
        else if (item.HasArmorComponent)
        {
            result = playerParty.HasPerk(DefaultPerks.Steward.PaidInPromise, true);
        }
        return result;
    }
}
