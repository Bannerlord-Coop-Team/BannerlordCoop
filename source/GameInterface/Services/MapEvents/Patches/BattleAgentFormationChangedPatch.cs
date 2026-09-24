using Common.Messaging;
using GameInterface.Services.MapEvents.Messages;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(Agent), nameof(Agent.Formation), MethodType.Setter)]
internal class BattleAgentFormationChangedPatch
{
    [HarmonyPrefix]
    private static void Prefix(Agent __instance, out Formation __state)
    {
        __state = __instance.Formation;
    }

    [HarmonyPostfix]
    private static void Postfix(Agent __instance, Formation __state)
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive
            || BattleSpawnGate.SuppressCapture || __state == __instance.Formation) return;

        MessageBroker.Instance.Publish(__instance, new BattleAgentFormationChanged(__instance));
    }
}
