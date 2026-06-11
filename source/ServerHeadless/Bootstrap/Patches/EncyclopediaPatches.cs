using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem.Encyclopedia;

namespace ServerHeadless.Bootstrap.Patches
{
    /// <summary>
    /// Entity EncyclopediaLink getters (Hero/Settlement/Clan/…) call
    /// <see cref="EncyclopediaManager.GetIdentifier"/>, which indexes a page dictionary populated by
    /// the (UI-side) encyclopedia behavior we don't run headless. Return a safe identifier so the
    /// link-building text paths used during conversation/menu setup don't fault.
    /// </summary>
    [HarmonyPatch(typeof(EncyclopediaManager))]
    internal class EncyclopediaPatches
    {
        [HarmonyPatch(nameof(EncyclopediaManager.GetIdentifier))]
        [HarmonyPrefix]
        static bool GetIdentifierPrefix(Type type, ref string __result)
        {
            __result = type.Name;
            return false;
        }
    }
}
