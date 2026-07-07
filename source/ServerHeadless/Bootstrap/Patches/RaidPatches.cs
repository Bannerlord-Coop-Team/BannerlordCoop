using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace ServerHeadless.Bootstrap.Patches
{
    /// <summary>
    /// The raid model's common-loot table is lazily built from item lookups that can resolve to null
    /// on an arbitrary headless-loaded world, NRE-ing when a raid occurs. Preserve every vanilla item
    /// that resolves and skip only missing item ids.
    /// </summary>
    [HarmonyPatch(typeof(DefaultRaidModel), nameof(DefaultRaidModel.GetCommonLootItemScores))]
    internal class RaidPatches
    {
        private static readonly string[] CommonLootItemIds =
        {
            "hides",
            "hardwood",
            "tools",
            "grain",
            "linen",
            "sheep",
            "mule",
            "pottery"
        };

        static bool Prefix(ref MBReadOnlyList<(ItemObject, float)> __result)
        {
            __result = BuildCommonLootItemScores(ResolveCommonLootItem, LogMissingCommonLootItem);
            return false;
        }

        internal static MBReadOnlyList<(ItemObject, float)> BuildCommonLootItemScores(
            Func<string, ItemObject> resolveItem,
            Action<string> logMissingItem)
        {
            var loot = new MBList<(ItemObject, float)>();

            foreach (var itemId in CommonLootItemIds)
            {
                var item = resolveItem(itemId);
                if (item == null)
                {
                    logMissingItem(itemId);
                    continue;
                }

                loot.Add((item, 100f / (item.Value + 1)));
            }

            return loot;
        }

        private static ItemObject ResolveCommonLootItem(string itemId)
        {
            return MBObjectManager.Instance?.GetObject<ItemObject>(itemId)!;
        }

        private static void LogMissingCommonLootItem(string itemId)
        {
            Console.WriteLine($"[ServerHeadless] Skipping unresolved raid common-loot item '{itemId}'.");
        }
    }
}
