using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Smithing.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CraftingSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Smithing.Patches;

[HarmonyPatch(typeof(CraftingCampaignBehavior))]
internal class CreateCraftedWeaponInternalPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<CraftingCampaignBehavior>();

    [HarmonyPatch(nameof(CraftingCampaignBehavior.CreateCraftedWeaponInternal))]
    [HarmonyPrefix]
    public static bool CreateCraftedWeaponInternalPrefix(CraftingCampaignBehavior __instance, ref ItemObject __result, bool isFreeMode, Hero crafterHero, WeaponDesign weaponDesign, ItemModifier weaponModifier = null)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        // Locally create string id. Without a string id, the result popup will not render new weapons.
        // This isn't sent to the server, won't matter after the item is crafted and won't persist across save games.
        // If the server uses this string id, two clients crafting at the same time can cause mismatched ids/crafted item counts.
        // Probable old cause of issue reported that gave crafted items to other players when two clients crafted at the same time.
        string nextCraftedItemId = $"ClientVisual_{__instance.GetNextCraftedItemId()}";
        ItemObject craftedItemObject;
        using (new AllowedThread())
        {
            craftedItemObject = (GameStateManager.Current.ActiveState as CraftingState).CraftingLogic.GetCurrentCraftedItemObject(true, nextCraftedItemId);
            ItemObject.InitAsPlayerCraftedItem(ref craftedItemObject);

            ItemObject registeredObject = MBObjectManager.Instance.RegisterObject<ItemObject>(craftedItemObject);
        }
        Crafting craftingLogic = (GameStateManager.Current.ActiveState as CraftingState).CraftingLogic;

        // Need to return the ItemObject for client's CraftingVM
        __result = craftedItemObject;

        // Patched separately for sending to server
        __instance.AddResearchPoints(weaponDesign.Template, Campaign.Current.Models.SmithingModel.GetPartResearchGainForSmithingItem(craftedItemObject, crafterHero, isFreeMode));

        // Publish message with data. Local ClientVisual not sent.
        // Actual item created on server and all clients in CraftingCampaignBehaviorCraftingHandler.
        var message = new CreatedCraftedWeaponInternal(isFreeMode, crafterHero, craftedItemObject.Name, craftedItemObject.Culture, weaponDesign, weaponModifier, Hero.MainHero, craftingLogic);
        MessageBroker.Instance.Publish(__instance, message);

        // Skip original to override original client saving
        return false;
    }

    [HarmonyPatch(nameof(CraftingCampaignBehavior.CreateCraftedWeaponInCraftingOrderMode))]
    [HarmonyPrefix]
    public static bool CreateCraftedWeaponInCraftingOrderModePrefix(CraftingCampaignBehavior __instance, ref ItemObject __result, Hero crafterHero, CraftingOrder craftingOrder, WeaponDesign weaponDesign)
    {
        ItemObject itemObject = __instance.CreateCraftedWeaponInternal(false, crafterHero, weaponDesign, null);
        float xpAmount = craftingOrder.GetOrderExperience(itemObject, __instance._currentItemModifier) + (float)Campaign.Current.Models.SmithingModel.GetSkillXpForSmithingInCraftingOrderMode(itemObject);

        var message = new AddSkillXpFromCrafting(crafterHero, xpAmount);
        MessageBroker.Instance.Publish(__instance, message);

        __result = itemObject;
        return false;
    }

    [HarmonyPatch(nameof(CraftingCampaignBehavior.CreateCraftedWeaponInFreeBuildMode))]
    [HarmonyPrefix]
    public static bool CreateCraftedWeaponInFreeBuildModePrefix(CraftingCampaignBehavior __instance, ref ItemObject __result, Hero hero, WeaponDesign weaponDesign, ItemModifier weaponModifier = null)
    {
        ItemObject itemObject = __instance.CreateCraftedWeaponInternal(true, hero, weaponDesign, weaponModifier);
        int skillXpForSmithingInFreeBuildMode = Campaign.Current.Models.SmithingModel.GetSkillXpForSmithingInFreeBuildMode(itemObject);

        var message = new AddSkillXpFromCrafting(hero, (float)skillXpForSmithingInFreeBuildMode);
        MessageBroker.Instance.Publish(__instance, message);
        
        __instance.AddItemToHistory(itemObject);

        __result = itemObject;
        return false;
    }
}