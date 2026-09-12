using Common.Messaging;
using GameInterface.Services.MapEvents.Messages;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Captures agents routing out of a coop battle (postfix on the <see cref="Mission.OnAgentRemoved"/>
/// removal funnel, filtered to the routed state) and publishes <see cref="BattleAgentRouted"/> so the
/// Missions battle controller can replicate the despawn to peers. Deaths take the separate
/// <see cref="BattleAgentDiedPatch"/> path; a rout is not a casualty, so no roster message is sent.
/// </summary>
[HarmonyPatch(typeof(Mission), nameof(Mission.OnAgentRemoved))]
internal class BattleAgentRoutedPatch
{
    [HarmonyPostfix]
    private static void Postfix(Agent affectedAgent, AgentState agentState)
    {
        if (!BattleSpawnConfig.Enabled) return;
        if (!BattleSpawnGate.IsCoopBattleActive) return;
        if (agentState != AgentState.Routed || affectedAgent == null) return;

        MessageBroker.Instance.Publish(affectedAgent, new BattleAgentRouted(affectedAgent));
    }
}
