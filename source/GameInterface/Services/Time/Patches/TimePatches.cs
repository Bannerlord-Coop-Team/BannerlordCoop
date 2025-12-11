using Common.Messaging;
using Common.Util;
using GameInterface.Services.Heroes.Messages;
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
    private static readonly TimeControlModeConverter timeControlModeConverter = new();

    [HarmonyPatch("TimeControlMode")]
    [HarmonyPatch(MethodType.Setter)]
    private static bool Prefix(ref Campaign __instance, ref CampaignTimeControlMode value)
    {
        // Empêcher toute accélération; laisser STOP et PLAY passer normalement
        switch (value)
        {
            case CampaignTimeControlMode.StoppableFastForward:
            case CampaignTimeControlMode.UnstoppableFastForward:
            case CampaignTimeControlMode.UnstoppableFastForwardForPartyWaitTime:
            case CampaignTimeControlMode.Stop:
            case CampaignTimeControlMode.FastForwardStop:
                value = CampaignTimeControlMode.StoppablePlay;
                break;
            default:
                break;
        }

        // Laisser le setter original s'exécuter pour préserver les invariants internes
        return true;
    }

    public static void OverrideTimeControlMode(Campaign campaign, CampaignTimeControlMode value)
    {
        if (campaign == null) return;
        // Clamp via interface également
        var clamped = value switch
        {
            CampaignTimeControlMode.StoppableFastForward => CampaignTimeControlMode.StoppablePlay,
            CampaignTimeControlMode.UnstoppableFastForward => CampaignTimeControlMode.StoppablePlay,
            CampaignTimeControlMode.UnstoppableFastForwardForPartyWaitTime => CampaignTimeControlMode.StoppablePlay,
            _ => value,
        };
        campaign._timeControlMode = clamped;
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
