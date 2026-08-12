using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using Common;
using GameInterface.Services.Clans.Patches;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Patches;
/// <summary>
/// Skips the <see cref="MobileParty.PartyTradeGold"/> setter for lord parties, since their
/// trade gold is derived from <see cref="Hero.Gold"/> which is already synced by autosync
/// </summary>
[HarmonyPatch(typeof(MobileParty))]
internal class PartyTradeGoldPatch
{
    [HarmonyPatch(nameof(MobileParty.PartyTradeGold), MethodType.Setter)]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    static bool PartyTradeGoldSetPrefix(MobileParty __instance, int value)
    {
        if (__instance.IsLordParty && __instance.LeaderHero != null)
        {
            __instance.LeaderHero.Gold = MathF.Max(value, 0);
            return false;
        }
        return true;
    }

    [HarmonyPatch(nameof(MobileParty.PartyTradeGold), MethodType.Setter)]
    [HarmonyPostfix]
    static void PartyTradeGoldSetPostfix(MobileParty __instance)
    {
        if (ModInformation.IsClient && __instance?.IsCaravan == true)
            ClanPartyItemVMPatches.RefreshCaravanIncome(__instance);
    }
}
