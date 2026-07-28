#if DEBUG
using HarmonyLib;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace Missions.Battles;

[HarmonyPatchCategory(MissionModule.BattleGuardFixtureInputPatchCategory)]
[HarmonyPatch(typeof(MissionMainAgentController), "ControlTick")]
internal static class BattleGuardFixtureControlPatch
{
    private static bool Prefix()
    {
        CoopBattleController controller = Mission.Current?
            .GetMissionBehavior<CoopBattleController>();
        if (controller?.IsGuardFixtureDrivingPlayerInput() != true)
            return true;

        // Movement flags alone bypass the native player-direction cache.
        return controller.PrepareGuardFixtureNativePlayerInput();
    }
}

[HarmonyPatchCategory(MissionModule.BattleGuardFixtureInputPatchCategory)]
[HarmonyPatch(
    typeof(InputContext),
    nameof(InputContext.IsGameKeyDown),
    new[] { typeof(int) })]
internal static class BattleGuardFixtureBlockInputPatch
{
    private const int DefendGameKey = 10;

    private static void Postfix(int gameKey, ref bool __result)
    {
        if (gameKey != DefendGameKey)
            return;

        if (Mission.Current?
            .GetMissionBehavior<CoopBattleController>()?
            .IsGuardFixtureHoldingNativePlayerBlock() == true)
        {
            __result = true;
        }
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
