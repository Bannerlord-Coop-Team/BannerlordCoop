#if DEBUG
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace Missions.Battles;

[HarmonyPatchCategory(MissionModule.BattleGuardFixtureInputPatchCategory)]
[HarmonyPatch(typeof(MissionMainAgentController), "ControlTick")]
internal static class BattleGuardFixtureControlPatch
{
    private static bool Prefix()
    {
        return Mission.Current?
            .GetMissionBehavior<CoopBattleController>()?
            .IsGuardFixtureDrivingPlayerInput() != true;
    }
}

[HarmonyPatchCategory(MissionModule.BattleGuardFixtureInputPatchCategory)]
[HarmonyPatch(
    typeof(MissionMainAgentController),
    nameof(MissionMainAgentController.OnPreMissionTick),
    new[] { typeof(float) })]
internal static class BattleGuardFixtureInputPatch
{
    private static void Postfix()
    {
        // The fixture input must be installed before native consumes control flags.
        Mission.Current?
            .GetMissionBehavior<CoopBattleController>()?
            .ApplyGuardFixturePlayerInput();
    }
}

#endif
