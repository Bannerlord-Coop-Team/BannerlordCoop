using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Quests.EscortMerchantCaravan.Patches;

[HarmonyPatch(typeof(MobileParty), nameof(MobileParty.IsVisible), MethodType.Getter)]
internal class EscortVisibilityPatch
{
    [HarmonyPrefix]
    private static bool Prefix(MobileParty __instance, ref bool __result)
    {
        if (ModInformation.IsClient || EscortQuestEventPatches.CurrentQuest == null) return true;
        __result = false;
        if (!__instance.IsActive) return false;
        if (Campaign.Current.TrueSight)
        {
            __result = true;
            return false;
        }
        if (__instance.CurrentSettlement != null && __instance.LeaderHero?.ClanBanner == null &&
            !(__instance.MapEvent?.IsSiegeAssault == true && __instance.Party.Side == BattleSideEnum.Attacker)) return false;
        var ownerParty = MobileParty.MainParty;
        PartyBase.CalculateVisibilityAndInspected(ownerParty.Position.ToVec2(), __instance,
            out __result, out _, ownerParty.SeeingRange);
        return false;
    }
}
