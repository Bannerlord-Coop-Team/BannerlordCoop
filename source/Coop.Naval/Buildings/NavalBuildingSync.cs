using GameInterface.AutoSync;
using HarmonyLib;
using NavalDLC.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace Coop.Naval.Buildings;

internal class NavalBuildingSync : IAutoSync
{
    public NavalBuildingSync(AutoSyncRegistry autoSyncRegistry)
    {
        autoSyncRegistry.AddTargetMethod(typeof(Building), AccessTools.Method(typeof(NavalNimbleSurgeCampaignBehaviour), nameof(NavalNimbleSurgeCampaignBehaviour.OnSettlementEntered)));
    }
}
