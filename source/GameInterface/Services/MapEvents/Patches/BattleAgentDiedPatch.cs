using Common.Messaging;
using GameInterface.Services.MapEvents.Messages;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Captures agent deaths in a coop field battle (postfix on the <see cref="Agent.Die"/> chokepoint) and
/// publishes <see cref="BattleAgentDied"/> so the Missions battle controller can replicate the death to
/// peers (kill their puppet) and notify the server for map-event casualty accounting. Fires for every death;
/// the controller's authority check decides whether this node owns it. Gated by
/// <see cref="BattleSpawnConfig.Enabled"/> and only in an active coop battle (see <see cref="BattleSpawnGate"/>).
/// </summary>
// Agent.Die is overloaded — pin the (Blow, KillInfo) signature so PatchAll can't become ambiguous.
[HarmonyPatch(typeof(Agent), nameof(Agent.Die), new[] { typeof(Blow), typeof(Agent.KillInfo) })]
internal class BattleAgentDiedPatch
{
    [HarmonyPostfix]
    private static void Postfix(Agent __instance)
    {
        if (!BattleSpawnConfig.Enabled) return;
        if (!BattleSpawnGate.IsCoopBattleActive) return;
        if (__instance == null) return;

        bool wounded = __instance.State == AgentState.Unconscious;
        MessageBroker.Instance.Publish(__instance, new BattleAgentDied(__instance, wounded));
    }
}
