#if DEBUG
using GameInterface;
using GameInterface.Services.MapEvents.NavalScenarios;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using NavalDLC.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace Missions.Naval;

/// <summary>
/// DEBUG: every party of a naval scenario staged with ignore_limits=1 may field 8 ships, the base vanilla's IgnoreDeploymentLimits
/// uses and the side's cap, with no perk bonus, on every client that splits its side, so two players can take all 8.
/// </summary>
[HarmonyPatch(typeof(NavalDLCShipDeploymentModel), nameof(NavalDLCShipDeploymentModel.GetShipDeploymentLimit))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class NavalScenarioShipLimitPatch
{
    // The base NavalDLCShipDeploymentModel.GetShipDeploymentLimit uses with IgnoreDeploymentLimits on, and the side's cap.
    private const int IgnoredLimit = 8;

    // Raises the limit of a party in the scenario's battle only, so no other battle's split changes.
    [HarmonyPostfix]
    private static void Postfix(MobileParty party, ref int __result)
    {
        if (!ContainerProvider.TryResolve<INavalScenarioState>(out var scenarios)) return;
        if (scenarios.Current is not { IgnoreShipLimits: true } scenario) return;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return;
        if (!objectManager.TryGetId(party?.MapEvent, out string mapEventId)) return;
        if (mapEventId != scenario.MapEventId) return;

        __result = IgnoredLimit;
    }
}
#endif
