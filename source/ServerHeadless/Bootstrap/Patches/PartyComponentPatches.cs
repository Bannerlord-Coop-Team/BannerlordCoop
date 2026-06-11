using HarmonyLib;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace ServerHeadless.Bootstrap.Patches
{
    /// <summary>
    /// Party-component AfterLoad/initialize hooks assume fully-resolved world data (settlement/town
    /// links, etc.). Headless we skip the native module-data load, so some of those links aren't
    /// established yet; guard the initializers against the resulting nulls.
    /// </summary>
    [HarmonyPatch]
    internal class PartyComponentPatches
    {
        // GarrisonPartyComponent.OnInitialize does `Settlement.Town.GarrisonPartyComponent = this`.
        [HarmonyPatch(typeof(GarrisonPartyComponent), "OnInitialize")]
        [HarmonyPrefix]
        static bool GarrisonOnInitializePrefix(GarrisonPartyComponent __instance)
            => __instance.Settlement?.Town != null;
    }
}
