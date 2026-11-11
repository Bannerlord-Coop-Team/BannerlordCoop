using Common;
using Common.Messaging;
using GameInterface.Services.MapEvents.Messages;
using HarmonyLib;
using ManagedCallbacks;
using TaleWorlds.DotNet;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Publishes <see cref="BattleAgentRetreatChanged"/> from the only two writers of <see cref="Agent.IsRunningAway"/>:
/// the native retreat callback and <see cref="Agent.StopRetreating"/>.
/// </summary>
[HarmonyPatch]
internal class BattleAgentRetreatPatches
{
    // Patches the generated dispatcher, not Agent.SetAgentAIPerformingRetreatBehavior: that method inlines into a dispatcher
    // JITted before PatchAll, which a battle played before joining does, and then a patch on it never fires.
    [HarmonyPatch(typeof(CoreCallbacksGenerated), nameof(CoreCallbacksGenerated.Agent_SetAgentAIPerformingRetreatBehavior))]
    [HarmonyPrefix]
    private static void NativeRetreatPrefix(int thisPointer, bool isAgentAIPerformingRetreatBehavior, out Agent __state)
    {
        __state = IsInCoopBattle() ? DotNetObject.GetManagedObjectWithId(thisPointer) as Agent : null;
        if (__state?.IsRunningAway == isAgentAIPerformingRetreatBehavior) __state = null;
    }

    // Publishes after the write, because the callback only writes when the agent has a mission on a non-client.
    [HarmonyPatch(typeof(CoreCallbacksGenerated), nameof(CoreCallbacksGenerated.Agent_SetAgentAIPerformingRetreatBehavior))]
    [HarmonyPostfix]
    private static void NativeRetreatPostfix(bool isAgentAIPerformingRetreatBehavior, Agent __state)
    {
        if (__state?.IsRunningAway == isAgentAIPerformingRetreatBehavior)
            Publish(__state, isAgentAIPerformingRetreatBehavior);
    }

    // StopRetreating always clears the flag, so a flag set now is a change.
    [HarmonyPatch(typeof(Agent), nameof(Agent.StopRetreating))]
    [HarmonyPrefix]
    private static void StopRetreatingPrefix(Agent __instance)
    {
        if (IsInCoopBattle() && __instance.IsRunningAway) Publish(__instance, false);
    }

    // Whether a coop battle mission is running here; ordinary missions keep vanilla's retreat untouched.
    private static bool IsInCoopBattle() => BattleSpawnConfig.Enabled && BattleSpawnGate.IsCoopBattleActive;

    // Marshals because agents tick on TWParallel workers, where CommonAIComponent.OnTickParallel calls StopRetreating.
    private static void Publish(Agent agent, bool isRunningAway)
    {
        GameThread.RunSafe(
            () => MessageBroker.Instance.Publish(agent, new BattleAgentRetreatChanged(agent, isRunningAway)),
            context: nameof(BattleAgentRetreatPatches));
    }
}
