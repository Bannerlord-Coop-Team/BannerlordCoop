using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Smithing;
internal class CraftingCampaignBehaviorSync : IAutoSync
{
    public CraftingCampaignBehaviorSync(AutoSyncRegistry AutoSyncRegistry)
    {
        //// Fields
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(CraftingCampaignBehavior), nameof(CraftingCampaignBehavior._townOrderCount)));

        // Dictionaries (not yet able to be synced)
        //AutoSyncRegistry.AddField(AccessTools.Field(typeof(CraftingCampaignBehavior), nameof(CraftingCampaignBehavior._craftingOrders)));
        //AutoSyncRegistry.AddField(AccessTools.Field(typeof(CraftingCampaignBehavior), nameof(CraftingCampaignBehavior._heroCraftingRecords)));
        //AutoSyncRegistry.AddField(AccessTools.Field(typeof(CraftingCampaignBehavior), nameof(CraftingCampaignBehavior._openedPartsDictionary))); // Problematic, assumes only one player
        //AutoSyncRegistry.AddField(AccessTools.Field(typeof(CraftingCampaignBehavior), nameof(CraftingCampaignBehavior._openNewPartXpDictionary))); // Problematic, assumes only one player
    }
}
