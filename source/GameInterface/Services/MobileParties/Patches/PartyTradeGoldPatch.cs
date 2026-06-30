using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Conversation.Tags;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Patches;

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
}