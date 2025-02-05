using Common.Extensions;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Time;
using HarmonyLib;
using SandBox.View.Map;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        if (AllowedThread.IsThisThreadAllowed() == false) return false;

        if (value != __instance._timeControlMode)
        {
            var controlMode = timeControlModeConverter.Convert(value);
            MessageBroker.Instance.Publish(__instance, new AttemptedTimeSpeedChanged(controlMode));
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
}

[HarmonyPatch(typeof(MapTimeControlVM), "ExecuteTimeControlChange")]
internal class AllowTimeControlFromControlsPatches
{
    private static void Prefix()
    {
        AllowedThread.AllowThisThread();
    }

    private static void Postfix()
    {
        AllowedThread.RevokeThisThread();
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