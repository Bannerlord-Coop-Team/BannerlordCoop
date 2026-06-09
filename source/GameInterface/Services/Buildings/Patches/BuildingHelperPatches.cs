using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Buildings.Messages;
using HarmonyLib;
using Helpers;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Buildings.Patches;

[HarmonyPatch(typeof(BuildingHelper))]
internal class BuildingHelperPatches
{
    [HarmonyPatch(nameof(BuildingHelper.ChangeDefaultBuilding))]
    [HarmonyPrefix]
    public static bool ChangeDefaultBuildingPrefix(Building newDefault, Town town)
    {
        if (CallOriginalPolicy.IsOriginalAllowed() || ModInformation.IsServer) return true;

        var message = new DefaultBuildingChanged(newDefault, town);
        MessageBroker.Instance.Publish(null, message);

        return false;
    }

    [HarmonyPatch(nameof(BuildingHelper.ChangeCurrentBuildingQueue))]
    [HarmonyPrefix]
    public static bool ChangeCurrentBuildingQueuePrefix(List<Building> buildings, Town town)
    {
        if (CallOriginalPolicy.IsOriginalAllowed() || ModInformation.IsServer) return true;

        var message = new CurrentBuildingQueueChanged(buildings, town);
        MessageBroker.Instance.Publish(null, message);

        return false;
    }

    [HarmonyPatch(nameof(BuildingHelper.BoostBuildingProcessWithGold))]
    [HarmonyPrefix]
    public static bool BoostBuildingProcessWithGoldPrefix(int gold, Town town)
    {
        if (CallOriginalPolicy.IsOriginalAllowed() || ModInformation.IsServer) return true;

        var message = new BuildingProcessBoostedWithGold(gold, town, Hero.MainHero);
        MessageBroker.Instance.Publish(null, message);

        // Assign to update client's VM
        town.BoostBuildingProcess = gold;

        return false;
    }
}
