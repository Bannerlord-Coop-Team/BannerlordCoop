using HarmonyLib;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Patches;

/// <summary>
/// Identifies voices made by vanilla's battle-order presentation paths.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(MissionModule.AgentVoicePatchCategory)]
internal static class OrderVoiceContextPatch
{
    [ThreadStatic]
    private static int contextDepth;

    internal static bool IsActive => contextDepth > 0;

    [HarmonyPatch(typeof(OrderController), nameof(OrderController.PlayOrderGestures))]
    [HarmonyPrefix]
    private static void PlayOrderGesturesPrefix() => Enter();

    [HarmonyPatch(typeof(OrderController), nameof(OrderController.PlayOrderGestures))]
    [HarmonyFinalizer]
    private static void PlayOrderGesturesFinalizer() => Exit();

    [HarmonyPatch(typeof(OrderController), nameof(OrderController.PlayFormationSelectedGesture))]
    [HarmonyPrefix]
    private static void PlayFormationSelectedGesturePrefix() => Enter();

    [HarmonyPatch(typeof(OrderController), nameof(OrderController.PlayFormationSelectedGesture))]
    [HarmonyFinalizer]
    private static void PlayFormationSelectedGestureFinalizer() => Exit();

    [HarmonyPatch(typeof(OrderController), nameof(OrderController.SelectAllFormations),
        new[] { typeof(Agent), typeof(bool) })]
    [HarmonyPrefix]
    private static void SelectAllFormationsPrefix() => Enter();

    [HarmonyPatch(typeof(OrderController), nameof(OrderController.SelectAllFormations),
        new[] { typeof(Agent), typeof(bool) })]
    [HarmonyFinalizer]
    private static void SelectAllFormationsFinalizer() => Exit();

    internal static void Enter()
    {
        contextDepth++;
    }

    internal static void Exit()
    {
        contextDepth--;
    }
}
