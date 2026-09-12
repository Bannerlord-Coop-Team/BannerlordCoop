using GameInterface.Services.MobileParties.Extensions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Banners.Interfaces;

public interface IBannerCampaignBehaviorInterface : IGameAbstraction
{
    void OnCollectLootItems(BannerCampaignBehavior behavior, PartyBase winnerParty, ItemRoster gainedLoots);
}

public class BannerCampaignBehaviorInterface : IBannerCampaignBehaviorInterface
{
    public void OnCollectLootItems(BannerCampaignBehavior behavior, PartyBase winnerParty, ItemRoster gainedLoots)
    {
        // Replace MainParty with IsPlayerParty()
        if (!winnerParty.MobileParty.IsPlayerParty()) return;

        // Use party's MapEvent instead of MainParty.MobileParty.MapEvent
        MapEvent mapEvent = winnerParty.MobileParty.MapEvent;
        ItemObject bannerRewardForWinningMapEvent = Campaign.Current.Models.BattleRewardModel.GetBannerRewardForWinningMapEvent(mapEvent);
        if (bannerRewardForWinningMapEvent != null)
        {
            gainedLoots.AddToCounts(bannerRewardForWinningMapEvent, 1);
        }
        Hero hero = null;
        MBReadOnlyList<MapEventParty> defeatedParties = mapEvent.PartiesOnSide(mapEvent.DefeatedSide);
        if (defeatedParties.Exists((MapEventParty x) => x.Party.IsMobile && x.Party.MobileParty.Army != null))
        {
            foreach (MapEventParty defeatedParty in defeatedParties)
            {
                if (defeatedParty.Party.IsMobile && defeatedParty.Party.MobileParty.Army != null && !defeatedParty.Party.MobileParty.Army.ArmyOwner.BannerItem.IsInvalid() && behavior.CanBannerBeLootedFromHero(defeatedParty.Party.MobileParty.Army.ArmyOwner))
                {
                    hero = defeatedParty.Party.MobileParty.Army.ArmyOwner;
                    break;
                }
            }
        }
        if (hero == null)
        {
            MapEventParty randomElementWithPredicate = defeatedParties.GetRandomElementWithPredicate((MapEventParty x) => x.Party.LeaderHero != null && !x.Party.LeaderHero.BannerItem.IsInvalid() && behavior.CanBannerBeLootedFromHero(x.Party.LeaderHero));
            hero = randomElementWithPredicate?.Party.LeaderHero;
        }
        if (hero != null)
        {
            float bannerLootChanceFromDefeatedHero = Campaign.Current.Models.BattleRewardModel.GetBannerLootChanceFromDefeatedHero(hero);
            if (MBRandom.RandomFloat <= bannerLootChanceFromDefeatedHero)
            {
                behavior.LogBannerLootForHero(hero, ((BannerComponent)hero.BannerItem.Item.ItemComponent).BannerLevel);
                gainedLoots.AddToCounts(hero.BannerItem.Item, 1);
                hero.BannerItem = new EquipmentElement(null, null, null, false);
            }
        }
    }
}