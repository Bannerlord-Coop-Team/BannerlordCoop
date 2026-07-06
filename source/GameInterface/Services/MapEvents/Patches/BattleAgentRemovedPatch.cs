using Common.Messaging;
using GameInterface.Services.MapEvents.Messages;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Captures authoritative deaths at the callback that carries the attacker and native death presentation.
/// </summary>
[HarmonyPatch(typeof(Mission), "OnAgentRemoved",
    new[] { typeof(Agent), typeof(Agent), typeof(AgentState), typeof(KillingBlow) })]
internal class BattleAgentRemovedPatch
{
    [HarmonyPostfix]
    private static void Postfix(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow killingBlow)
    {
        if (!BattleSpawnConfig.Enabled) return;
        if (!BattleSpawnGate.IsCoopBattleActive) return;
        if (BattleSpawnGate.IsReplicatedDeath(affectedAgent)) return;
        if (agentState != AgentState.Killed && agentState != AgentState.Unconscious) return;

        MessageBroker.Instance.Publish(affectedAgent,
            new BattleAgentDied(affectedAgent, affectorAgent, agentState == AgentState.Unconscious, killingBlow));
    }
}
