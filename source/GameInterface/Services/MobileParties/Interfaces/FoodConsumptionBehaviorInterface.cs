using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Armies.Extensions;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.UI.Notifications.Messages;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Interfaces;

public interface IFoodConsumptionBehaviorInterface : IGameAbstraction
{
    void OnPartyAttachedParty(FoodConsumptionBehavior behavior, MobileParty mobileParty);
    void DailyTickParty(FoodConsumptionBehavior behavior, MobileParty mobileParty);
    void PartyConsumeFood(FoodConsumptionBehavior behavior, MobileParty mobileParty, bool starvingCheck = false);
    void CheckAnimalBreeding(FoodConsumptionBehavior behavior, MobileParty mobileParty);
}

public class FoodConsumptionBehaviorInterface : IFoodConsumptionBehaviorInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<FoodConsumptionBehaviorInterface>();

    public void OnPartyAttachedParty(FoodConsumptionBehavior behavior, MobileParty mobileParty)
    {
        GameThread.RunSafe(() =>
        {
            if (mobileParty.Army == null || !mobileParty.Army.IsPlayerArmy()) return;

            if (mobileParty.Party.IsStarving)
            {
                behavior.PartyConsumeFood(mobileParty, true);
                return;
            }
            if (mobileParty.Army.LeaderParty.Party.IsStarving)
            {
                behavior.PartyConsumeFood(mobileParty.Army.LeaderParty, true);
            }
            foreach (MobileParty attachedParty in mobileParty.Army.LeaderParty.AttachedParties)
            {
                if (attachedParty.Party.IsStarving && mobileParty != attachedParty)
                {
                    behavior.PartyConsumeFood(attachedParty, true);
                }
            }
        });
    }

    public void DailyTickParty(FoodConsumptionBehavior behavior, MobileParty mobileParty)
    {
        GameThread.RunSafe(() =>
        {
            behavior.CheckAnimalBreeding(mobileParty);
            if (Campaign.Current.Models.MobilePartyFoodConsumptionModel.DoesPartyConsumeFood(mobileParty))
            {
                // Check for a player party starving here instead of OnTick
                behavior.PartyConsumeFood(mobileParty, mobileParty.IsPlayerParty() && mobileParty.Party.IsStarving);
            }
        });
    }

    public void PartyConsumeFood(FoodConsumptionBehavior behavior, MobileParty mobileParty, bool starvingCheck = false)
    {
        GameThread.RunSafe(() =>
        {
            bool wasStarving = mobileParty.Party.IsStarving;
            float foodChange = mobileParty.FoodChange;
            float absoluteFoodChange = (foodChange < 0f) ? (-foodChange) : 0f;
            int remainingFoodPercentage = (mobileParty.Party.RemainingFoodPercentage < 0) ? 0 : mobileParty.Party.RemainingFoodPercentage;
            int percentageFoodChange = MathF.Round(absoluteFoodChange * 100f);
            remainingFoodPercentage -= percentageFoodChange;

            // Consume food and slaughter livestock in party if needed
            behavior.MakeFoodConsumption(mobileParty, ref remainingFoodPercentage);
            if (remainingFoodPercentage < 0 && mobileParty.ItemRoster.TotalFood > 0 && behavior.SlaughterLivestock(mobileParty, remainingFoodPercentage))
            {
                behavior.MakeFoodConsumption(mobileParty, ref remainingFoodPercentage);
                if (mobileParty.IsPlayerParty()) // Replace IsMainParty check
                {
                    // Notify players of slaughtered animals
                    MessageBroker.Instance.Publish(this, new NotifyAnimalsSlaughteredToEat(mobileParty));
                }
            }

            // Get food from army if starving (this part is identical to TaleWorlds' code)
            if (remainingFoodPercentage < 0 && mobileParty.Army != null && (mobileParty.AttachedTo == mobileParty.Army.LeaderParty || mobileParty.Army.LeaderParty == mobileParty))
            {
                Dictionary<Hero, float> dictionary = new Dictionary<Hero, float>();
                Hero leaderHero = mobileParty.LeaderHero;
                do
                {
                    MobileParty mobileParty2 = null;
                    float num4 = 1f;
                    MobileParty leaderParty = mobileParty.Army.LeaderParty;
                    if (leaderParty != mobileParty && !leaderParty.Party.IsStarving && leaderParty.ItemRoster.TotalFood > 0)
                    {
                        float num5 = (float)leaderParty.ItemRoster.TotalFood / MathF.Abs(leaderParty.FoodChange);
                        if (num5 > num4)
                        {
                            num4 = num5;
                            mobileParty2 = leaderParty;
                        }
                    }
                    foreach (MobileParty mobileParty3 in leaderParty.AttachedParties)
                    {
                        if (mobileParty3 != mobileParty && !mobileParty3.Party.IsStarving && mobileParty3.ItemRoster.TotalFood > 0)
                        {
                            float num6 = (float)mobileParty3.ItemRoster.TotalFood / MathF.Abs(mobileParty3.FoodChange);
                            if (num6 > num4)
                            {
                                num4 = num6;
                                mobileParty2 = mobileParty3;
                            }
                        }
                    }
                    ItemRosterElement itemRosterElement = default(ItemRosterElement);
                    if (mobileParty2 == null) break;

                    int num7 = 10000;
                    bool flag = false;
                    foreach (ItemRosterElement itemRosterElement2 in mobileParty2.ItemRoster)
                    {
                        if (itemRosterElement2.EquipmentElement.Item.IsFood && itemRosterElement2.EquipmentElement.Item.Value < num7)
                        {
                            itemRosterElement = itemRosterElement2;
                            num7 = itemRosterElement2.EquipmentElement.Item.Value;
                            flag = true;
                        }
                    }
                    if (!flag)
                    {
                        foreach (ItemRosterElement itemRosterElement3 in mobileParty2.ItemRoster)
                        {
                            if (itemRosterElement3.EquipmentElement.Item.HasHorseComponent && itemRosterElement3.EquipmentElement.Item.HorseComponent.IsLiveStock && itemRosterElement3.EquipmentElement.Item.Value < num7)
                            {
                                itemRosterElement = itemRosterElement3;
                                num7 = itemRosterElement3.EquipmentElement.Item.Value;
                                flag = true;
                            }
                        }

                        break;
                    }

                    mobileParty2.ItemRoster.AddToCounts(itemRosterElement.EquipmentElement, -1);
                    remainingFoodPercentage += 100;
                    if (itemRosterElement.EquipmentElement.Item.HasHorseComponent && itemRosterElement.EquipmentElement.Item.HorseComponent.IsLiveStock)
                    {
                        int meatCount = itemRosterElement.EquipmentElement.Item.HorseComponent.MeatCount;
                        mobileParty2.ItemRoster.AddToCounts(DefaultItems.Meat, meatCount - 1);
                    }
                    Hero leaderHero2 = mobileParty2.LeaderHero;
                    if (leaderHero != null && leaderHero2 != null)
                    {
                        float num8 = 0.2f;
                        GainKingdomInfluenceAction.ApplyForGivingFood(leaderHero2, leaderHero, num8);
                        float num9;
                        if (dictionary.TryGetValue(leaderHero2, out num9))
                        {
                            dictionary[leaderHero2] = num9 + num8;
                        }
                        else
                        {
                            dictionary.Add(leaderHero2, num8);
                        }
                    }
                }
                while (remainingFoodPercentage < 0);
                foreach (KeyValuePair<Hero, float> keyValuePair in dictionary)
                {
                    CampaignEventDispatcher.Instance.OnHeroSharedFoodWithAnother(keyValuePair.Key, leaderHero, keyValuePair.Value);
                }
            }

            // Check party morale
            mobileParty.Party.RemainingFoodPercentage = remainingFoodPercentage;
            bool isNowStarving = mobileParty.Party.IsStarving;
            if ((int)Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ToDays != (int)CampaignTime.Now.ToDays)
            {
                if (wasStarving && isNowStarving)
                {
                    int dailyStarvationMoralePenalty = Campaign.Current.Models.PartyMoraleModel.GetDailyStarvationMoralePenalty(mobileParty.Party);
                    mobileParty.RecentEventsMorale += (float)dailyStarvationMoralePenalty;
                    if (mobileParty.IsPlayerParty()) // Replace IsMainParty check
                    {
                        // Notify players of declining morale from starving party
                        MessageBroker.Instance.Publish(this, new NotifyDailyStarvationPenalty(mobileParty, -dailyStarvationMoralePenalty));

                        // Only vibrates game pad?
                        CampaignEventDispatcher.Instance.OnMainPartyStarving();

                        if ((int)CampaignTime.Now.ToDays % 3 == 0 && mobileParty.MemberRoster.TotalManCount > 1)
                        {
                            TraitLevelingHelper.OnPartyStarved(); // TODO
                        }
                    }
                }
                if (mobileParty.MemberRoster.TotalManCount > 1)
                {
                    SkillLevelingManager.OnFoodConsumed(mobileParty, isNowStarving);

                    // Replace IsMainParty check
                    if (!wasStarving && !isNowStarving && mobileParty.IsPlayerParty() && mobileParty.Morale >= 90f && mobileParty.MemberRoster.TotalRegulars >= 20 && (int)CampaignTime.Now.ToDays % 10 == 0)
                    {
                        TraitLevelingHelper.OnPartyTreatedWell(); // TODO
                    }
                }
            }
            CampaignEventDispatcher.Instance.OnPartyConsumedFood(mobileParty);
        });
    }

    public void CheckAnimalBreeding(FoodConsumptionBehavior behavior, MobileParty mobileParty)
    {
        GameThread.RunSafe(() =>
        {
            if (MBRandom.RandomFloat < DefaultPerks.Riding.Breeder.PrimaryBonus && !mobileParty.IsCurrentlyAtSea && mobileParty.HasPerk(DefaultPerks.Riding.Breeder, false) && (mobileParty.ItemRoster.NumberOfLivestockAnimals > 1 || mobileParty.ItemRoster.NumberOfPackAnimals > 1 || mobileParty.ItemRoster.NumberOfMounts > 1))
            {
                int numberOfAnimalsInParty = mobileParty.ItemRoster.NumberOfLivestockAnimals + mobileParty.ItemRoster.NumberOfPackAnimals + mobileParty.ItemRoster.NumberOfMounts;
                ItemRosterElement randomAnimal = mobileParty.ItemRoster.GetRandomElementWithPredicate((ItemRosterElement x) => x.EquipmentElement.Item.HasHorseComponent);
                int numberBred = MathF.Round(MathF.Max(1f, (float)numberOfAnimalsInParty / 50f));
                mobileParty.ItemRoster.AddToCounts(randomAnimal.EquipmentElement.Item, numberBred);
                if (mobileParty.IsPlayerParty())
                {
                    // Notify players of animals bred in their party
                    MessageBroker.Instance.Publish(this, new NotifyAnimalsBred(mobileParty, numberBred, randomAnimal));
                }
            }
        });
    }
}