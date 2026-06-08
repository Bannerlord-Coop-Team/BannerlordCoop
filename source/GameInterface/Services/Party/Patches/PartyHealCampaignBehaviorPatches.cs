using Common;
using Common.Messaging;
using GameInterface.Services.Party.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Party.Patches;

[HarmonyPatch(typeof(PartyHealCampaignBehavior))]
internal class PartyHealCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(PartyHealCampaignBehavior.OnHourlyTick))]
    [HarmonyPrefix]
    public static bool OnHourlyTick(ref PartyHealCampaignBehavior __instance)
    {
        if (ModInformation.IsClient) return false;

        // Instead of MainParty.Party, manage on the server for all players
        var message = new PartyHealHourlyTick(__instance);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(PartyHealCampaignBehavior.OnQuarterDailyPartyTick))]
    [HarmonyPrefix]
    public static bool OnQuarterDailyPartyTickPrefix(ref PartyHealCampaignBehavior __instance, MobileParty mobileParty)
    {
        if (ModInformation.IsClient) return false;

        // Avoid healing player parties
        var message = new PartyHealQuarterDailyTick(__instance, mobileParty);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }
}
