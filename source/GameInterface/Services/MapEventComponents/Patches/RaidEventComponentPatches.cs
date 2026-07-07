using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEventComponents.Messages;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEventComponents.Patches;

[HarmonyPatch(typeof(RaidEventComponent))]
internal class RaidEventComponentPatches
{
    [HarmonyPatch(nameof(RaidEventComponent.Update))]
    [HarmonyPrefix]
    private static void UpdatePrefix(RaidEventComponent __instance, out RaidUpdateState __state)
    {
        __state = new RaidUpdateState(
            __instance._lootedItemCount,
            CaptureRewards(__instance._raidProductionRewards),
            CaptureItems(__instance.AttackerSide?.LeaderParty?.ItemRoster));
    }

    [HarmonyPatch(nameof(RaidEventComponent.Update))]
    [HarmonyPostfix]
    private static void UpdatePostfix(RaidEventComponent __instance, RaidUpdateState __state)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (ModInformation.IsClient) return;

        if (__instance._lootedItemCount != __state.LootedItemCount ||
            RewardsChanged(__state.RaidProductionRewards, __instance._raidProductionRewards))
        {
            MessageBroker.Instance.Publish(__instance, new RaidProductionRewardsUpdated(__instance));
        }

        var leaderParty = __instance.AttackerSide?.LeaderParty;
        var lootedItems = GetAddedItems(__state.PartyItemCounts, leaderParty?.ItemRoster);
        if (leaderParty?.MobileParty != null && lootedItems.Count > 0)
            MessageBroker.Instance.Publish(__instance, new RaidLootedItemsUpdated(leaderParty.MobileParty, lootedItems));
    }

    private static Dictionary<ItemObject, int> CaptureItems(ItemRoster roster)
    {
        var result = new Dictionary<ItemObject, int>();
        if (roster == null)
            return result;

        foreach (var element in roster)
        {
            var item = element.EquipmentElement.Item;
            if (item == null)
                continue;

            result[item] = element.Amount;
        }

        return result;
    }

    private static Dictionary<ItemObject, float> CaptureRewards(Dictionary<ItemObject, float> rewards)
    {
        return rewards == null
            ? new Dictionary<ItemObject, float>()
            : new Dictionary<ItemObject, float>(rewards);
    }

    private static ItemRoster GetAddedItems(Dictionary<ItemObject, int> before, ItemRoster after)
    {
        var result = new ItemRoster();
        if (after == null)
            return result;

        foreach (var element in after)
        {
            var item = element.EquipmentElement.Item;
            if (item == null)
                continue;

            before.TryGetValue(item, out var previousAmount);
            var added = element.Amount - previousAmount;
            if (added <= 0)
                continue;

            result.AddToCounts(element.EquipmentElement, added);
        }

        return result;
    }

    private static bool RewardsChanged(Dictionary<ItemObject, float> before, Dictionary<ItemObject, float> after)
    {
        if (after == null)
            return before.Count != 0;

        if (before.Count != after.Count)
            return true;

        foreach (var reward in after)
        {
            if (!before.TryGetValue(reward.Key, out var previous))
                return true;

            if (previous != reward.Value)
                return true;
        }

        return false;
    }

    private sealed class RaidUpdateState
    {
        public int LootedItemCount { get; }
        public Dictionary<ItemObject, float> RaidProductionRewards { get; }
        public Dictionary<ItemObject, int> PartyItemCounts { get; }

        public RaidUpdateState(
            int lootedItemCount,
            Dictionary<ItemObject, float> raidProductionRewards,
            Dictionary<ItemObject, int> partyItemCounts)
        {
            LootedItemCount = lootedItemCount;
            RaidProductionRewards = raidProductionRewards;
            PartyItemCounts = partyItemCounts;
        }
    }
}