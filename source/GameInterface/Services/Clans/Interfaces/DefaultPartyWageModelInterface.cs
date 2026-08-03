using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.MobileParties.Extensions;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Clans.Interfaces;

public interface IDefaultPartyWageModelInterface : IGameAbstraction
{
    /// <summary>
    /// Re-implement vanilla's GetTotalWage
    /// The perk Aid Corps doesn't do anything in vanilla
    /// MainParty/PlayerClan usage replaced to work for coop
    /// </summary>
    ExplainedNumber GetTotalWage(DefaultPartyWageModel __instance, MobileParty mobileParty, TroopRoster troopRoster, bool includeDescriptions = false);
}

public class DefaultPartyWageModelInterface : IDefaultPartyWageModelInterface
{
    public ExplainedNumber GetTotalWage(DefaultPartyWageModel __instance, MobileParty mobileParty, TroopRoster troopRoster, bool includeDescriptions = false)
    {
        int totalWage = 0;
        int infantryWage = 0;
        int cavalryWage = 0;
        int archersWage = 0;
        int eliteArchersWage = 0;
        int banditsWage = 0;
        int caravanGuardsWage = 0;
        int mercenariesWage = 0;
        for (int i = 0; i < troopRoster.Count; i++)
        {
            var elementCopyAtIndex = troopRoster.GetElementCopyAtIndex(i);
            var character = elementCopyAtIndex.Character;

            if (character.IsHero)
            {
                var hero = character.HeroObject;

                // Replace to check for any player hero.
                // Non-leader player heroes shouldn't have wages as the money wouldn't be transferred to them.
                // In future multiple players in a party and/or clan could be paid separately with a different clan expense.
                if (hero.IsClanLeader
                    || hero.IsPlayerHero()
                    || (mobileParty.IsPlayerParty() && hero.Clan.IsPlayerClan() && hero.Occupation == Occupation.Lord)) continue;

                if (mobileParty.LeaderHero != null && mobileParty.LeaderHero.GetPerkValue(DefaultPerks.Steward.PaidInPromise))
                {
                    totalWage += MathF.Round((float)character.TroopWage * (1f + DefaultPerks.Steward.PaidInPromise.PrimaryBonus));
                }
                else
                {
                    totalWage += character.TroopWage;
                }
            }
            else
            {
                // Actually apply Aid Corps bonus. Vanilla's implementation doesn't do anything
                bool hasAidCorps = mobileParty.HasPerk(DefaultPerks.Steward.AidCorps, false);
                int numberOfTroopsToPayWages = elementCopyAtIndex.Number - (hasAidCorps ? elementCopyAtIndex.WoundedNumber : 0);

                int totalWageForTroopType = character.TroopWage * numberOfTroopsToPayWages;
                totalWage += totalWageForTroopType;

                // Calculate wages for individual troop types
                if (character.Culture.IsBandit) banditsWage += totalWageForTroopType;
                if (character.IsInfantry) infantryWage += totalWageForTroopType;
                if (character.IsMounted) cavalryWage += totalWageForTroopType;
                if (character.Occupation == Occupation.CaravanGuard) caravanGuardsWage += totalWageForTroopType;
                if (character.Occupation == Occupation.Mercenary) mercenariesWage += totalWageForTroopType;
                if (character.IsRanged) archersWage += totalWageForTroopType;
                if (character.IsRanged && character.Tier >= 4) eliteArchersWage += totalWageForTroopType;
            }
        }

        if (mobileParty.LeaderHero != null && mobileParty.LeaderHero.GetPerkValue(DefaultPerks.Roguery.DeepPockets))
        {
            totalWage -= banditsWage;
            var banditBonus = new ExplainedNumber((float)banditsWage, false, null);
            PerkHelper.AddPerkBonusForCharacter(DefaultPerks.Roguery.DeepPockets, mobileParty.LeaderHero.CharacterObject, false, ref banditBonus, false);
            totalWage += (int)banditBonus.ResultNumber;
        }
        if (eliteArchersWage > 0)
        {
            totalWage -= eliteArchersWage;
            var eliteArcherBonus = new ExplainedNumber((float)eliteArchersWage, false, null);
            PerkHelper.AddPerkBonusForParty(DefaultPerks.Crossbow.PickedShots, mobileParty, true, ref eliteArcherBonus, mobileParty.IsCurrentlyAtSea);
            totalWage += (int)eliteArcherBonus.ResultNumber;
        }

        var result = new ExplainedNumber((float)totalWage, includeDescriptions, null);
        result.LimitMin(0f);

        var buildingEffects = new ExplainedNumber(1f, false, null);
        HandleGarrisonParty(__instance, mobileParty, infantryWage, archersWage, cavalryWage, ref result, ref buildingEffects);

        AddPerkFactor(mobileParty, DefaultPerks.Trade.SwordForBarter, caravanGuardsWage, true, ref result);
        AddPerkFactor(mobileParty, DefaultPerks.Steward.Contractors, mercenariesWage, false, ref result);
        AddPerkFactor(mobileParty, DefaultPerks.Trade.MercenaryConnections, mercenariesWage, true, ref result);

        var militaryCoronaeValue = (mobileParty.LeaderHero != null && mobileParty.LeaderHero.Clan.Kingdom != null && !mobileParty.LeaderHero.Clan.IsUnderMercenaryService && mobileParty.LeaderHero.Clan.Kingdom.ActivePolicies.Contains(DefaultPolicies.MilitaryCoronae)) ? 0.1f : 0f;
        result.AddFactor(militaryCoronaeValue, DefaultPolicies.MilitaryCoronae.Name);
        result.AddFactor(buildingEffects.ResultNumber - 1f, __instance._buildingEffects);

        AddOtherFactors(__instance, mobileParty, ref result);

        return result;
    }

    private void HandleGarrisonParty(
        DefaultPartyWageModel __instance,
        MobileParty mobileParty,
        int infantryWage,
        int archersWage,
        int cavalryWage,
        ref ExplainedNumber result,
        ref ExplainedNumber buildingEffects)
    {
        if (!mobileParty.IsGarrison) return;

        Settlement currentSettlement = mobileParty.CurrentSettlement;
        if ((currentSettlement?.Town) != null)
        {
            if (mobileParty.CurrentSettlement.IsFortification)
            {
                PerkHelper.AddPerkBonusForTown(DefaultPerks.OneHanded.MilitaryTradition, mobileParty.CurrentSettlement.Town, ref result);
                PerkHelper.AddPerkBonusForTown(DefaultPerks.TwoHanded.Berserker, mobileParty.CurrentSettlement.Town, ref result);
                PerkHelper.AddPerkBonusForTown(DefaultPerks.Steward.DrillSergant, mobileParty.CurrentSettlement.Town, ref result);
                float troopRatio = (float)infantryWage / result.BaseNumber;
                __instance.CalculatePartialGarrisonWageReduction(troopRatio, mobileParty, DefaultPerks.Polearm.StandardBearer, ref result, true);
                float troopRatio2 = (float)archersWage / result.BaseNumber;
                __instance.CalculatePartialGarrisonWageReduction(troopRatio2, mobileParty, DefaultPerks.Crossbow.PeasantLeader, ref result, true);
                float troopRatio3 = (float)cavalryWage / result.BaseNumber;
                __instance.CalculatePartialGarrisonWageReduction(troopRatio3, mobileParty, DefaultPerks.Riding.CavalryTactics, ref result, true);
            }
            if (mobileParty.CurrentSettlement.IsCastle)
            {
                PerkHelper.AddPerkBonusForTown(DefaultPerks.Bow.HunterClan, mobileParty.CurrentSettlement.Town, ref result);
                PerkHelper.AddPerkBonusForTown(DefaultPerks.Steward.StiffUpperLip, mobileParty.CurrentSettlement.Town, ref result);
            }
            if (mobileParty.CurrentSettlement.Owner.Culture.HasFeat(DefaultCulturalFeats.EmpireGarrisonWageFeat))
            {
                result.AddFactor(DefaultCulturalFeats.EmpireGarrisonWageFeat.EffectBonus, __instance._cultureText);
            }
            mobileParty.CurrentSettlement.Town.AddEffectOfBuildings(BuildingEffectEnum.GarrisonWageReduction, ref buildingEffects);
        }
    }

    private void AddPerkFactor(MobileParty mobileParty, PerkObject perk, int numTroops, bool checkSecondaryBonus, ref ExplainedNumber result)
    {
        if (mobileParty.HasPerk(perk, checkSecondaryBonus))
        {
            float factor = (float)numTroops / result.BaseNumber;
            if (factor > 0f)
            {
                float resultingFactor = (checkSecondaryBonus ? perk.SecondaryBonus : perk.PrimaryBonus) * factor;
                result.AddFactor(resultingFactor, perk.Name);
            }
        }
    }

    private void AddOtherFactors(DefaultPartyWageModel __instance, MobileParty mobileParty, ref ExplainedNumber result)
    {
        if (PartyBaseHelper.HasFeat(mobileParty.Party, DefaultCulturalFeats.AseraiIncreasedWageFeat))
        {
            result.AddFactor(DefaultCulturalFeats.AseraiIncreasedWageFeat.EffectBonus, __instance._cultureText);
        }
        if (!mobileParty.IsCurrentlyAtSea && mobileParty.HasPerk(DefaultPerks.Steward.Frugal, false))
        {
            result.AddFactor(DefaultPerks.Steward.Frugal.PrimaryBonus, DefaultPerks.Steward.Frugal.Name);
        }
        if (mobileParty.Army != null)
        {
            PerkHelper.AddPerkBonusForParty(DefaultPerks.Steward.EfficientCampaigner, mobileParty, false, ref result, mobileParty.IsCurrentlyAtSea);
        }
        if (mobileParty.SiegeEvent != null && mobileParty.SiegeEvent.BesiegerCamp.HasInvolvedPartyForEventType(mobileParty.Party, MapEvent.BattleTypes.Siege) && mobileParty.HasPerk(DefaultPerks.Steward.MasterOfWarcraft, false))
        {
            result.AddFactor(DefaultPerks.Steward.MasterOfWarcraft.PrimaryBonus, DefaultPerks.Steward.MasterOfWarcraft.Name);
        }
        if (mobileParty.EffectiveQuartermaster != null)
        {
            PerkHelper.AddEpicPerkBonusForCharacter(DefaultPerks.Steward.PriceOfLoyalty, mobileParty.EffectiveQuartermaster.CharacterObject, DefaultSkills.Steward, true, ref result, Campaign.Current.Models.CharacterDevelopmentModel.MaxSkillRequiredForEpicPerkBonus, false);
        }
        if (mobileParty.CurrentSettlement != null && mobileParty.LeaderHero != null && mobileParty.LeaderHero.GetPerkValue(DefaultPerks.Trade.ContentTrades))
        {
            result.AddFactor(DefaultPerks.Trade.ContentTrades.SecondaryBonus, DefaultPerks.Trade.ContentTrades.Name);
        }
    }
}