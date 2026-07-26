using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Workshops.Messages;
using HarmonyLib;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Workshops.Patches;

[HarmonyPatch(typeof(WorkshopsCampaignBehavior))]
internal class WorkshopDataPatches
{
    [HarmonyPatch(nameof(WorkshopsCampaignBehavior.AddNewWorkshopData))]
    [HarmonyPrefix]
    public static bool AddNewWorkshopDataPrefix(ref WorkshopsCampaignBehavior __instance, Workshop workshop)
    {
        return ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed();
    }

    [HarmonyPatch(nameof(WorkshopsCampaignBehavior.AddNewWorkshopData))]
    [HarmonyPostfix]
    public static void AddNewWorkshopDataPostfix(ref WorkshopsCampaignBehavior __instance, Workshop workshop)
    {
        if (ModInformation.IsClient) return;

        MessageBroker.Instance.Publish(__instance, new NewWorkshopDataAdded(workshop));
    }

    [HarmonyPatch(nameof(WorkshopsCampaignBehavior.RemoveWorkshopData))]
    [HarmonyPrefix]
    public static bool RemoveWorkshopDataPrefix(ref WorkshopsCampaignBehavior __instance, Workshop workshop)
    {
        return ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed();
    }

    [HarmonyPatch(nameof(WorkshopsCampaignBehavior.RemoveWorkshopData))]
    [HarmonyPostfix]
    public static void RemoveWorkshopDataPostfix(ref WorkshopsCampaignBehavior __instance, Workshop workshop)
    {
        if (ModInformation.IsClient) return;

        MessageBroker.Instance.Publish(__instance, new WorkshopDataRemoved(workshop));
    }

    [HarmonyPatch(nameof(WorkshopsCampaignBehavior.AddOutputProgressForWarehouse))]
    [HarmonyPrefix]
    public static bool AddOutputProgressForWarehousePrefix(ref WorkshopsCampaignBehavior __instance, Workshop workshop, float progressToAdd)
    {
        return ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed();
    }

    [HarmonyPatch(nameof(WorkshopsCampaignBehavior.AddOutputProgressForWarehouse))]
    [HarmonyPostfix]
    public static void AddOutputProgressForWarehousePostfix(ref WorkshopsCampaignBehavior __instance, Workshop workshop, float progressToAdd)
    {
        if (ModInformation.IsClient) return;

        MessageBroker.Instance.Publish(__instance, new OutputProgressAddedForWarehouse(workshop, progressToAdd));
    }

    [HarmonyPatch(nameof(WorkshopsCampaignBehavior.AddOutputProgressForTown))]
    [HarmonyPrefix]
    public static bool AddOutputProgressForTownPrefix(ref WorkshopsCampaignBehavior __instance, Workshop workshop, float progressToAdd)
    {
        return ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed();
    }

    [HarmonyPatch(nameof(WorkshopsCampaignBehavior.AddOutputProgressForTown))]
    [HarmonyPostfix]
    public static void AddOutputProgressForTownPostfix(ref WorkshopsCampaignBehavior __instance, Workshop workshop, float progressToAdd)
    {
        if (ModInformation.IsClient) return;

        MessageBroker.Instance.Publish(__instance, new OutputProgressForTownAdded(workshop, progressToAdd));
    }
}

[HarmonyPatch(typeof(IWorkshopWarehouseCampaignBehavior))]
internal class WorkshopDataInterfacePatches
{
    public static MethodBase TargetMethod()
    {
        return typeof(WorkshopsCampaignBehavior)
            .GetInterfaceMap(typeof(IWorkshopWarehouseCampaignBehavior))
            .TargetMethods
            .First(m => m.Name.Contains("SetIsGettingInputsFromWarehouse"));
    }

    public static bool Prefix(ref WorkshopsCampaignBehavior __instance, Workshop workshop, bool isActive)
    {
        // Check for updating on server and other clients
        if (ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed()) return true;

        // Send message to server
        var message = new IsGettingInputsFromWarehouseSet(workshop, isActive);
        MessageBroker.Instance.Publish(__instance, message);

        // Can run locally for updating VMs before server sends message to apply the same value
        return true;
    }
}

[HarmonyPatch(typeof(IWorkshopWarehouseCampaignBehavior))]
internal class SetIsGettingInputsFromWarehousePatch
{
    public static MethodBase TargetMethod()
    {
        return typeof(WorkshopsCampaignBehavior)
            .GetInterfaceMap(typeof(IWorkshopWarehouseCampaignBehavior))
            .TargetMethods
            .First(m => m.Name.Contains("SetStockProductionInWarehouseRatio"));
    }

    public static bool Prefix(ref WorkshopsCampaignBehavior __instance, Workshop workshop, float ratio)
    {
        // Check for updating on server and other clients
        if (ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed()) return true;

        // Send message to server
        var message = new StockProductionInWarehouseRatioSet(workshop, ratio);
        MessageBroker.Instance.Publish(__instance, message);

        // Can run locally for updating VMs before server sends message to apply the same value
        return true;
    }
}
