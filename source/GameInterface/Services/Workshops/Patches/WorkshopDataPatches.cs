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
        // Don't run on clients unless in an allowed thread
        if (ModInformation.IsClient) return CallOriginalPolicy.IsOriginalAllowed();

        // Update clients
        var message = new NewWorkshopDataAdded(workshop);
        MessageBroker.Instance.Publish(__instance, message);

        // Run on the server
        return true;
    }

    [HarmonyPatch(nameof(WorkshopsCampaignBehavior.RemoveWorkshopData))]
    [HarmonyPrefix]
    public static bool RemoveWorkshopDataPrefix(ref WorkshopsCampaignBehavior __instance, Workshop workshop)
    {
        // Don't run on clients unless in an allowed thread
        if (ModInformation.IsClient) return CallOriginalPolicy.IsOriginalAllowed();

        // Update clients
        var message = new WorkshopDataRemoved(workshop);
        MessageBroker.Instance.Publish(__instance, message);

        // Run on the server
        return true;
    }

    [HarmonyPatch(nameof(WorkshopsCampaignBehavior.AddOutputProgressForWarehouse))]
    [HarmonyPrefix]
    public static bool AddOutputProgressForWarehousePrefix(ref WorkshopsCampaignBehavior __instance, Workshop workshop, float progressToAdd)
    {
        // Don't run on clients unless in an allowed thread
        if (ModInformation.IsClient) return CallOriginalPolicy.IsOriginalAllowed();

        // Update clients
        var message = new OutputProgressAddedForWarehouse(workshop, progressToAdd);
        MessageBroker.Instance.Publish(__instance, message);

        // Run on the server
        return true;
    }

    [HarmonyPatch(nameof(WorkshopsCampaignBehavior.AddOutputProgressForTown))]
    [HarmonyPrefix]
    public static bool AddOutputProgressForTownPrefix(ref WorkshopsCampaignBehavior __instance, Workshop workshop, float progressToAdd)
    {
        // Don't run on clients unless in an allowed thread
        if (ModInformation.IsClient) return CallOriginalPolicy.IsOriginalAllowed();

        // Update clients
        var message = new OutputProgressForTownAdded(workshop, progressToAdd);
        MessageBroker.Instance.Publish(__instance, message);

        // Run on the server
        return true;
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