using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Configuration;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Time;
using GameInterface.Services.Time.Interfaces;
using HarmonyLib;
using SandBox.View.Map;
using Serilog;
using System.Collections.Generic;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(Campaign))]
internal class TimePatches
{
    private static CampaignTimeControlMode CurrentMode = CampaignTimeControlMode.Stop;

    internal static ITimeControlModeConverter ModeConverter { get; } = new TimeControlModeConverter();

    [HarmonyPatch("TimeControlMode")]
    [HarmonyPatch(MethodType.Setter)]
    private static bool Prefix(ref Campaign __instance, ref CampaignTimeControlMode value)
    {
        // Only player time input publishes a request: the time controls, hotkeys and map clicks allow this thread
        // in the AllowTimeControlFrom*Patches classes
        if (CallOriginalPolicy.IsOriginalAllowed() == false) return false;

        if (value != __instance._timeControlMode)
        {
            var controlMode = ModeConverter.Convert(value);
            MessageBroker.Instance.Publish(__instance, new TimeSpeedChangedAttempted(controlMode));
        }

        return false;
    }

    [HarmonyPatch("TimeControlMode")]
    [HarmonyPatch(MethodType.Getter)]
    private static void Postfix(ref CampaignTimeControlMode __result)
    {
        __result = CurrentMode;
    }

    public static void OverrideTimeControlMode(CampaignTimeControlMode value)
    {
        if (Campaign.Current == null) return;

        // _timeControlMode is getting set magically somewhere so we use our own value instead
        CurrentMode = value;
        Campaign.Current._timeControlMode = value;
    }

    internal static bool CanApplyTimeControl(TimeControlEnum controlMode)
    {
        if (ContainerProvider.TryResolve<ITimeControlInterface>(out var timeControlInterface) == false)
        {
            return true;
        }

        return timeControlInterface.CanSetTimeControl(controlMode);
    }

    internal static void PublishBlockedTimeControlAttempt(object source, TimeControlEnum controlMode)
    {
        MessageBroker.Instance.Publish(source, new TimeSpeedChangedAttempted(controlMode));
    }
}

[HarmonyPatch(typeof(Campaign), "TickMapTime")]
internal class CampaignTimePacingPatches
{
    [HarmonyPostfix]
    private static void TickMapTimePostfix(Campaign __instance, float realDt)
    {
        if (ModInformation.IsClient &&
            ContainerProvider.TryResolve<IMapTimeTrackerInterface>(out var mapTimeTrackerInterface))
        {
            mapTimeTrackerInterface.ApplyClientSimulationTime(__instance, realDt);
        }
    }
}

[HarmonyPatch(typeof(MapTimeControlVM))]
internal class AllowTimeControlFromControlsPatches
{
    [HarmonyPatch(nameof(MapTimeControlVM.ExecuteTimeControlChange))]
    [HarmonyPrefix]
    private static bool ExecuteTimeControlChangePrefix(ref MapTimeControlVM __instance, int selectedTimeSpeed)
    {
        using (new AllowedThread())
        {
            var controlMode = TimePatches.ModeConverter.Convert((CampaignTimeControlMode)selectedTimeSpeed);
            if (TimePatches.CanApplyTimeControl(controlMode) == false)
            {
                TimePatches.PublishBlockedTimeControlAttempt(__instance, controlMode);
                return false;
            }

            int num = selectedTimeSpeed;
            if (__instance._timeFlowState == 3 && num == 2)
            {
                num = 4;
            }
            else if (__instance._timeFlowState == 4 && num == 1)
            {
                num = 3;
            }
            else if (__instance._timeFlowState == 2 && num == 0)
            {
                num = 6;
            }
            if (num != __instance._timeFlowState)
            {
                __instance.TimeFlowState = num;
                __instance.SetTimeSpeed(selectedTimeSpeed);
            }

            return false;
        }
    }
}

[HarmonyPatch(typeof(Campaign))]
internal class CampaignSetTimeSpeedPatches
{
    [HarmonyPatch(nameof(Campaign.SetTimeSpeed))]
    [HarmonyPrefix]
    private static bool SetTimeSpeedPrefix(ref Campaign __instance, int speed)
    {
        var controlMode = TimePatches.ModeConverter.Convert((CampaignTimeControlMode)speed);
        if (TimePatches.CanApplyTimeControl(controlMode))
        {
            return true;
        }

        TimePatches.PublishBlockedTimeControlAttempt(__instance, controlMode);
        return false;
    }
}


[HarmonyPatch(typeof(MapScreen), "TaleWorlds.CampaignSystem.GameState.IMapStateHandler.BeforeTick")]
internal class AllowTimeControlFromHotKeysPatches
{
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var allow = AccessTools.Method(typeof(AllowedThread), nameof(AllowedThread.AllowThisThread));
        var revoke = AccessTools.Method(typeof(AllowedThread), nameof(AllowedThread.RevokeThisThread));

        var setTime = AccessTools.Method(typeof(Campaign), nameof(Campaign.SetTimeSpeed));
        var isWaitActive = AccessTools.PropertyGetter(typeof(GameMenu), nameof(GameMenu.IsWaitActive));
        var allowTimeControlInMenu = AccessTools.Method(typeof(AllowTimeControlFromHotKeysPatches), nameof(AllowTimeControlInMenu));

        foreach (var instr in instructions)
        {
            if (instr.Calls(setTime))
            {
                // wrap set time speed with allow
                yield return new CodeInstruction(OpCodes.Call, allow);
                yield return instr;
                yield return new CodeInstruction(OpCodes.Call, revoke);
            }
            else if (instr.Calls(isWaitActive))
            {
                // Co-op pauses when everyone is occupied, so menus must still let a player resume time.
                instr.opcode = OpCodes.Call;
                instr.operand = allowTimeControlInMenu;
                yield return instr;
            }
            else
            {
                yield return instr;
            }
        }
    }

    private static bool AllowTimeControlInMenu(GameMenu _) => true;
}

/// <summary>
/// Sends the time changes vanilla makes for a travel or parley click to the server as time requests, like the time controls do.
/// <see cref="MapScreen.HandleClickTimeChange"/> resumes or fast forwards, and <see cref="MapScreen.HandleMouse"/> drops a
/// single click's fast forward back to play once the double click window runs out.
/// </summary>
[HarmonyPatch(typeof(MapScreen))]
internal class AllowTimeControlFromMapClickPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<AllowTimeControlFromMapClickPatches>();

    [HarmonyPatch(nameof(MapScreen.HandleClickTimeChange))]
    [HarmonyPrefix]
    private static void Prefix(out AllowedThread __state)
    {
        __state = AllowMapClickTimeChange();
    }

    [HarmonyPatch(nameof(MapScreen.HandleClickTimeChange))]
    [HarmonyFinalizer]
    private static void Finalizer(AllowedThread __state)
    {
        __state?.Dispose();
    }

    [HarmonyPatch(nameof(MapScreen.HandleMouse))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> HandleMouseTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var setTimeControlMode = AccessTools.PropertySetter(typeof(Campaign), nameof(Campaign.TimeControlMode));
        var setFromMapClick = AccessTools.Method(typeof(AllowTimeControlFromMapClickPatches), nameof(SetTimeControlModeFromMapClick));

        int replacements = 0;
        foreach (var instr in instructions)
        {
            if (instr.Calls(setTimeControlMode))
            {
                instr.opcode = OpCodes.Call;
                instr.operand = setFromMapClick;
                replacements++;
            }

            yield return instr;
        }

        if (replacements == 0)
            Logger.Warning("{Patch} made no replacements; the target method may have changed", nameof(HandleMouseTranspiler));
    }

    private static void SetTimeControlModeFromMapClick(Campaign campaign, CampaignTimeControlMode mode)
    {
        using (AllowMapClickTimeChange())
        {
            campaign.TimeControlMode = mode;
        }
    }

    // Without the allowed thread the time setter drops the click's change
    private static AllowedThread AllowMapClickTimeChange()
    {
        var options = ModConfigProvider.ModOptions;
        // Map clicks only change time when the server turns on mapClickChangesTime
        if (!options.MapClickChangesTime) return null;
        // and only when clients are allowed to control time at all
        if (!options.AllowClientTimeControls) return null;
        return new AllowedThread();
    }
}

[HarmonyPatch(typeof(PlayerEncounter), nameof(PlayerEncounter.Finish))]
internal class PlayerEncouterFinishPatches
{
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var setTimeMode = AccessTools.PropertySetter(typeof(Campaign), nameof(Campaign.TimeControlMode));

        foreach (var instr in instructions)
        {
            if (instr.Calls(setTimeMode))
            {
                // Pop campaign
                yield return new CodeInstruction(OpCodes.Pop);
                // Pop setter value
                yield return new CodeInstruction(OpCodes.Pop);
            }
            else
            {
                yield return instr;
            }
        }
    }
}
