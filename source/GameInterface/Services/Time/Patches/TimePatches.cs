using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.Time;
using HarmonyLib;
using SandBox.View.Map;
using System.Collections.Generic;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(Campaign))]
internal class TimePatches
{
    private static CampaignTimeControlMode CurrentMode = CampaignTimeControlMode.Stop;

    private static readonly TimeControlModeConverter timeControlModeConverter = new();

    [HarmonyPatch("TimeControlMode")]
    [HarmonyPatch(MethodType.Setter)]
    private static bool Prefix(ref Campaign __instance, ref CampaignTimeControlMode value)
    {
        // We only want ExecuteTimeControlChange, which is called from the time control button clicks,
        // to publish the TimeSpeedChanged message.
        // To do this we skip this method if this thread is not "allowed"
        // We set this thread to "allowed" in AllowTimeControlFromControlsPatches
        if (CallOriginalPolicy.IsOriginalAllowed() == false) return false;

        if (value != __instance._timeControlMode)
        {
            var controlMode = timeControlModeConverter.Convert(value);
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

    public static void OverrideTimeControlMode(Campaign campaign, CampaignTimeControlMode value)
    {
        if (campaign == null) return;

        // _timeControlMode is getting set magically somewhere so we use our own value instead
        CurrentMode = value;
        campaign._timeControlMode = value;
    }

    internal static TimeControlEnum ConvertSelectedTimeSpeed(int selectedTimeSpeed)
    {
        return selectedTimeSpeed switch
        {
            0 => TimeControlEnum.Pause,
            1 => TimeControlEnum.Play_1x,
            2 => TimeControlEnum.Play_2x,
            _ => timeControlModeConverter.Convert((CampaignTimeControlMode)selectedTimeSpeed),
        };
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

[HarmonyPatch(typeof(MapTimeControlVM))]
internal class AllowTimeControlFromControlsPatches
{
    [HarmonyPatch(nameof(MapTimeControlVM.ExecuteTimeControlChange))]
    [HarmonyPrefix]
    private static bool ExecuteTimeControlChangePrefix(ref MapTimeControlVM __instance, int selectedTimeSpeed)
    {
        using (new AllowedThread())
        {
            var controlMode = TimePatches.ConvertSelectedTimeSpeed(selectedTimeSpeed);
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
        var controlMode = TimePatches.ConvertSelectedTimeSpeed(speed);
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

        foreach (var instr in instructions)
        {
            if (instr.Calls(setTime))
            {
                // wrap set time speed with allow
                yield return new CodeInstruction(OpCodes.Call, allow);
                yield return instr;
                yield return new CodeInstruction(OpCodes.Call, revoke);
            }
            else
            {
                yield return instr;
            }
        }
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
