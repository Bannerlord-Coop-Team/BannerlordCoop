using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Smithing.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;

namespace GameInterface.Services.Smithing.Patches;

[HarmonyPatch(typeof(CraftingCampaignBehavior))]
internal class DoSmeltingPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<CraftingCampaignBehavior>();

    [HarmonyPatch(nameof(CraftingCampaignBehavior.DoSmelting))]
    [HarmonyPrefix]
    public static bool DoSmeltingPrefix(ref CraftingCampaignBehavior __instance, Hero currentCraftingHero, EquipmentElement equipmentElement)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        // Publish message with data
        var message = new DoSmelting(currentCraftingHero, equipmentElement);
        MessageBroker.Instance.Publish(__instance, message);

        // Skip original to override original client saving
        return false;
    }
}
