using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Actions.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Actions.Patches;

[HarmonyPatch(typeof(ChangeProductionTypeOfWorkshopAction))]
internal class ChangeProductionTypeOfWorkshopActionPatches
{
    [HarmonyPatch(nameof(ChangeProductionTypeOfWorkshopAction.Apply))]
    [HarmonyPrefix]
    public static bool ApplyPrefix(Workshop workshop, WorkshopType newWorkshopType, bool ignoreCost = false)
    {
        if (ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed()) return true;

        var message = new ProductionTypeOfWorkshopChanged(workshop, newWorkshopType, ignoreCost);
        MessageBroker.Instance.Publish(null, message);

        return false;
    }
}
