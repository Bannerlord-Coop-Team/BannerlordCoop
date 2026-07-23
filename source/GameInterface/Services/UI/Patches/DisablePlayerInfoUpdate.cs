using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.UI.Patches;

[HarmonyPatch(typeof(MapInfoVM))]
internal class DisablePlayerInfoUpdate
{
    /// <summary>
    /// This disables DefaultClanFinanceModel.CalculateClanGoldChange which creates a large amount of stancelinks
    /// </summary>
    /// <param name="__instance"></param>
    /// <param name="updateForced"></param>
    /// <returns></returns>
    [HarmonyPatch(nameof(MapInfoVM.UpdatePlayerInfo))]
    [HarmonyPrefix]
    static bool Prefix(MapInfoVM __instance, bool updateForced)
    {
        __instance._goldInfo.HasWarning = false;
        if (__instance._goldInfo.IntValue != Hero.MainHero.Gold || updateForced)
        {
            __instance._goldInfo.IntValue = Hero.MainHero.Gold;
            __instance._goldInfo.Value = CampaignUIHelper.GetAbbreviatedValueTextFromValue(__instance._goldInfo.IntValue);
        }
        __instance._influenceInfo.HasWarning = (Hero.MainHero.Clan.Influence < -100f);
        if (__instance._influenceInfo.IntValue != (int)Hero.MainHero.Clan.Influence || updateForced)
        {
            __instance._influenceInfo.IntValue = (int)Hero.MainHero.Clan.Influence;
            __instance._influenceInfo.Value = CampaignUIHelper.GetAbbreviatedValueTextFromValue(__instance._influenceInfo.IntValue);
        }
        float num = MathF.Round(MobileParty.MainParty.Morale, 1);
        __instance._moraleInfo.HasWarning = (MobileParty.MainParty.Morale < (float)Campaign.Current.Models.PartyDesertionModel.GetMoraleThresholdForTroopDesertion());
        if (__instance._moraleInfo.FloatValue != num || updateForced)
        {
            __instance._moraleInfo.Value = num.ToString();
            __instance._moraleInfo.FloatValue = num;
            MBTextManager.SetTextVariable("BASE_EFFECT", num.ToString("0.0"), false);
        }
        int numDaysForFoodToLast = MobileParty.MainParty.GetNumDaysForFoodToLast();
        __instance._foodInfo.HasWarning = (numDaysForFoodToLast < 1);
        __instance._foodInfo.IntValue = (int)((MobileParty.MainParty.Food > 0f) ? MobileParty.MainParty.Food : 0f);
        __instance._foodInfo.Value = __instance._foodInfo.IntValue.ToString();
        __instance._troopsInfo.HasWarning = (PartyBase.MainParty.PartySizeLimit < PartyBase.MainParty.NumberOfAllMembers || PartyBase.MainParty.PrisonerSizeLimit < PartyBase.MainParty.NumberOfPrisoners);
        __instance._troopsInfo.IntValue = PartyBase.MainParty.MemberRoster.TotalManCount;
        __instance._troopsInfo.Value = CampaignUIHelper.GetPartyNameplateText(PartyBase.MainParty);
        int num2 = (int)MathF.Clamp((float)(Hero.MainHero.HitPoints * 100 / CharacterObject.PlayerCharacter.MaxHitPoints()), 1f, 100f);
        __instance._hitPointsInfo.HasWarning = Hero.MainHero.IsWounded;
        if (__instance._hitPointsInfo.IntValue != num2 || updateForced)
        {
            __instance._hitPointsInfo.IntValue = num2;
            GameTexts.SetVariable("NUMBER", __instance._hitPointsInfo.IntValue);
            __instance._hitPointsInfo.Value = GameTexts.FindText("str_NUMBER_percent", null).ToString();
        }
        Army army = MobileParty.MainParty.Army;
        MobileParty mobileParty = ((army != null) ? army.LeaderParty : null) ?? MobileParty.MainParty;
        float num3 = (mobileParty.IsActive && mobileParty.CurrentNavigationFace.IsValid()) ? mobileParty.Speed : 0f;
        if (__instance._speedInfo.FloatValue != num3 || updateForced)
        {
            __instance._speedInfo.FloatValue = num3;
            __instance._speedInfo.Value = CampaignUIHelper.FloatToString(num3);
        }
        float seeingRange = MobileParty.MainParty.SeeingRange;
        if (__instance._viewDistanceInfo.FloatValue != seeingRange || updateForced)
        {
            __instance._viewDistanceInfo.FloatValue = seeingRange;
            __instance._viewDistanceInfo.Value = CampaignUIHelper.FloatToString(seeingRange);
        }
        int totalWage = MobileParty.MainParty.TotalWage;
        if (__instance._troopWageInfo.IntValue != totalWage || updateForced)
        {
            __instance._troopWageInfo.IntValue = totalWage;
            __instance._troopWageInfo.Value = totalWage.ToString();
        }

        return false;
    }
}
