using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Smithing;
internal class CraftingCampaignBehaviorSync : IDynamicSync
{
    public CraftingCampaignBehaviorSync(DynamicSyncRegistry dynamicSyncRegistry)
    {
        //// Fields
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(CraftingCampaignBehavior), nameof(CraftingCampaignBehavior._townOrderCount)));

        // Dictionaries (not yet able to be synced)
        //dynamicSyncRegistry.AddField(AccessTools.Field(typeof(CraftingCampaignBehavior), nameof(CraftingCampaignBehavior._craftingOrders)));
        //dynamicSyncRegistry.AddField(AccessTools.Field(typeof(CraftingCampaignBehavior), nameof(CraftingCampaignBehavior._heroCraftingRecords)));
        //dynamicSyncRegistry.AddField(AccessTools.Field(typeof(CraftingCampaignBehavior), nameof(CraftingCampaignBehavior._openedPartsDictionary))); // Problematic, assumes only one player
        //dynamicSyncRegistry.AddField(AccessTools.Field(typeof(CraftingCampaignBehavior), nameof(CraftingCampaignBehavior._openNewPartXpDictionary))); // Problematic, assumes only one player
    }
}
