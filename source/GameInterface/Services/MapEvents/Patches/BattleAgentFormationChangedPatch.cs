using Common.Messaging;
using GameInterface.Services.MapEvents.Messages;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>Reports membership changes after the formation setter completes, not just transfer orders.</summary>
[HarmonyPatch(typeof(Agent), nameof(Agent.Formation), MethodType.Setter)]
internal class BattleAgentFormationChangedPatch
{
    // Remember membership before the setter so repeated assignments do not produce traffic.
    [HarmonyPrefix]
    private static void Prefix(Agent __instance, out Formation __state)
    {
        __state = __instance.Formation;
    }

    // The replicator filters unregistered, remote and deployment-withheld agents.
    [HarmonyPostfix]
    private static void Postfix(Agent __instance, Formation __state)
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive) return;
        if (BattleSpawnGate.SuppressCapture || __instance.Formation == __state) return;

        int formationIndex = __instance.Formation == null ? -1 : (int)__instance.Formation.FormationIndex;
        MessageBroker.Instance.Publish(__instance, new BattleAgentFormationChanged(__instance, formationIndex));
    }
}
