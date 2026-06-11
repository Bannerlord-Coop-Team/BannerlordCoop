using HarmonyLib;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ServerHeadless.Bootstrap.Patches
{
    /// <summary>
    /// The raid model's common-loot table is lazily built from item lookups that can resolve to null
    /// on an arbitrary headless-loaded world, NRE-ing when a raid occurs. Report an empty loot table.
    /// </summary>
    [HarmonyPatch(typeof(DefaultRaidModel), nameof(DefaultRaidModel.GetCommonLootItemScores))]
    internal class RaidPatches
    {
        static bool Prefix(ref MBReadOnlyList<(ItemObject, float)> __result)
        {
            __result = new MBList<(ItemObject, float)>();
            return false;
        }
    }
}
