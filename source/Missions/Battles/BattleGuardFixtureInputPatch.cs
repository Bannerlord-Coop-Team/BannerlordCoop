#if DEBUG
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace Missions.Battles;

[HarmonyPatchCategory(MissionModule.BattleGuardFixtureInputPatchCategory)]
[HarmonyPatch(
    typeof(MissionMainAgentController),
    nameof(MissionMainAgentController.OnPreMissionTick),
    new[] { typeof(float) })]
internal static class BattleGuardFixtureInputPatch
{
    private static void Postfix()
    {
        // Native consumes player controls before mission OnMissionTick.
        Mission.Current?
            .GetMissionBehavior<CoopBattleController>()?
            .ApplyGuardFixturePlayerInput();
    }
}
#endif
