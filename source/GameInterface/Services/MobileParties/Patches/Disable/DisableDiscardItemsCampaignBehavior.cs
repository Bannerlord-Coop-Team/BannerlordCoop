using Common;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches.Disable;

[HarmonyPatch(typeof(DiscardItemsCampaignBehavior))]
internal class DisableDiscardItemsCampaignBehavior
{
    [HarmonyPatch(nameof(DiscardItemsCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}

[HarmonyPatch(typeof(DiscardItemsCampaignBehavior))]
internal class DiscardItemsCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(DiscardItemsCampaignBehavior.HandlePartyInventory))]
    [HarmonyPrefix]
    public static bool HandlePartyInventoryPrefix(DiscardItemsCampaignBehavior __instance, PartyBase party)
    {
        return party.MobileParty != null && !party.MobileParty.IsPlayerParty();
    }
}