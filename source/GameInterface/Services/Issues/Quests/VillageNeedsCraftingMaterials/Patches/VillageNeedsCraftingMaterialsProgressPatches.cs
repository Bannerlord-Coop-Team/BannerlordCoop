using Common;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using Quest = TaleWorlds.CampaignSystem.Issues.VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest;

namespace GameInterface.Services.Issues.Quests.VillageNeedsCraftingMaterials.Patches;

/// <summary>
/// Trades are applied by the server, so vanilla's inventory exchange event never reaches the owner's
/// quest. The owner's client recounts the materials whenever the server changes its party's items.
/// </summary>
[HarmonyPatch(typeof(ItemRoster))]
internal class VillageNeedsCraftingMaterialsProgressPatches
{
    [HarmonyPatch(nameof(ItemRoster.AddToCounts), new[] { typeof(EquipmentElement), typeof(int) })]
    [HarmonyPostfix]
    private static void AddToCountsPostfix(ItemRoster __instance, EquipmentElement rosterElement)
    {
        if (IsMainPartyRoster(__instance))
        {
            UpdateQuestLogs(rosterElement.Item);
        }
    }

    [HarmonyPatch(nameof(ItemRoster.Clear))]
    [HarmonyPostfix]
    private static void ClearPostfix(ItemRoster __instance)
    {
        if (IsMainPartyRoster(__instance))
        {
            UpdateQuestLogs(null);
        }
    }

    private static bool IsMainPartyRoster(ItemRoster roster)
    {
        if (ModInformation.IsServer)
        {
            return false;
        }

        var mainParty = Campaign.Current?.MainParty;
        return mainParty != null && roster == mainParty.ItemRoster;
    }

    // A null item recounts every crafting quest, used when the roster was cleared
    private static void UpdateQuestLogs(ItemObject item)
    {
        var quests = Campaign.Current.QuestManager.Quests
            .OfType<Quest>()
            .Where(quest => quest.IsOngoing && quest._playerAcceptedQuestLog != null && (item == null || quest._requestedItem == item))
            .ToList();

        foreach (var quest in quests)
        {
            quest.UpdateQuestLog();
        }
    }
}
