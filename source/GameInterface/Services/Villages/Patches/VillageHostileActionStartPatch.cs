using Common;
using GameInterface.Policies;
using GameInterface.Services.Villages.Data;
using GameInterface.Services.Villages.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(VillageHostileActionCampaignBehavior), "StartHostileAction")]
internal class VillageHostileActionStartPatch
{
    [HarmonyPrefix]
    private static bool Prefix(VillageHostileActionCampaignBehavior.HostileActionType hostileActionType)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsServer) return true;

        if (!ContainerProvider.TryResolve<IVillageHostileActionInterface>(out var hostileActionInterface))
            return false;

        if (!TryConvert(hostileActionType, out var action))
            return false;

        hostileActionInterface.RequestHostileAction(action);
        return false;
    }

    private static bool TryConvert(
        VillageHostileActionCampaignBehavior.HostileActionType hostileActionType,
        out VillageHostileAction action)
    {
        switch (hostileActionType)
        {
            case VillageHostileActionCampaignBehavior.HostileActionType.Raid:
                action = VillageHostileAction.Raid;
                return true;
            case VillageHostileActionCampaignBehavior.HostileActionType.ForceTroop:
                action = VillageHostileAction.ForceVolunteers;
                return true;
            case VillageHostileActionCampaignBehavior.HostileActionType.ForceSupply:
                action = VillageHostileAction.ForceSupplies;
                return true;
            default:
                action = VillageHostileAction.Raid;
                return false;
        }
    }
}
