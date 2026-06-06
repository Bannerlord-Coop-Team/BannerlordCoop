using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Inventory.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using static TaleWorlds.Core.Equipment;

namespace GameInterface.Services.Inventory.Patches;

[HarmonyPatch(typeof(InventoryLogic.PartyEquipment))]
internal class InventoryLogicPartyEquipmentPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<InventoryLogic.PartyEquipment>();

    [HarmonyPatch(nameof(InventoryLogic.PartyEquipment.ResetEquipment))]
    [HarmonyPostfix]
    public static void ResetEquipmentPostfix(ref InventoryLogic.PartyEquipment __instance)
    {
        var message = new EquipmentReset(__instance.CharacterEquipments);
        MessageBroker.Instance.Publish(__instance, message);
    }
}

[HarmonyPatch(typeof(SPInventoryVM))]
internal class SPInventoryVMPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<SPInventoryVM>();

    [HarmonyPatch(nameof(SPInventoryVM.UpdateEquipment))]
    [HarmonyPostfix]
    public static void UpdateEquipmentPostfix(ref SPInventoryVM __instance, Equipment equipment, SPItemVM itemVM, EquipmentIndex itemType)
    {
        Hero hero = __instance._currentCharacter.HeroObject;
        EquipmentType equipmentType = equipment._equipmentType;
        EquipmentElement equipmentElement = (itemVM == null) ? default(EquipmentElement) : itemVM.ItemRosterElement.EquipmentElement;

        var message = new EquipmentUpdated(hero, equipmentType, equipmentElement, itemType);
        MessageBroker.Instance.Publish(__instance, message);
    }
}