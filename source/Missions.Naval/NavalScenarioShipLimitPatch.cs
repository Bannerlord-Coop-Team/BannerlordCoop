#if DEBUG
using GameInterface;
using GameInterface.Services.MapEvents.NavalScenarios;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using NavalDLC.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace Missions.Naval;

/// <summary>
/// DEBUG: every party of a naval scenario staged with ignore_limits=1 fields vanilla's most ships, as vanilla's
/// IgnoreDeploymentLimits gives, on every client that splits its side, so two players can take all of the side's 8 ships.
/// </summary>
[HarmonyPatch(typeof(NavalDLCShipDeploymentModel), nameof(NavalDLCShipDeploymentModel.GetShipDeploymentLimit))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class NavalScenarioShipLimitPatch
{
    // NavalDLCShipDeploymentModel.GetShipDeploymentLimit with IgnoreDeploymentLimits on.
    private const int IgnoredLimit = 8;

    // Raises the limit of a party in the scenario's battle only, so no other battle's split changes.
    [HarmonyPostfix]
    private static void Postfix(MobileParty party, ref int __result)
    {
        if (!ContainerProvider.TryResolve<INavalScenarioState>(out var scenarios)
            || scenarios.Current is not { IgnoreShipLimits: true } scenario) return;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)
            || !objectManager.TryGetId(party?.MapEvent, out string mapEventId) || mapEventId != scenario.MapEventId) return;

        __result = IgnoredLimit;
    }
}
#endif
