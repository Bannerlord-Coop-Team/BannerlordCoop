using GameInterface.Services.MapEvents;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.GauntletUI.Mission.Singleplayer;

namespace GameInterface.Services.UI.Patches;

/// <summary>
/// Supplies the authoritative attacker and killing blow while the kill feed handles a replicated death.
/// </summary>
[HarmonyPatch(typeof(MissionGauntletKillNotificationSingleplayerUIHandler),
    nameof(MissionGauntletKillNotificationSingleplayerUIHandler.OnAgentRemoved),
    new[] { typeof(Agent), typeof(Agent), typeof(AgentState), typeof(KillingBlow) })]
internal class ReplicatedDeathKillFeedPatch
{
    [HarmonyPrefix]
    internal static void Prefix(Agent affectedAgent, ref Agent affectorAgent, ref KillingBlow killingBlow)
    {
        if (!BattleSpawnConfig.Enabled) return;
        if (!BattleSpawnGate.IsCoopBattleActive) return;
        if (!BattleSpawnGate.TryGetReplicatedDeath(affectedAgent, out var replicatedAffector, out var replicatedKillingBlow)) return;

        if (replicatedAffector != null)
            affectorAgent = replicatedAffector;
        if (replicatedKillingBlow.IsValid)
            killingBlow = replicatedKillingBlow;
    }
}
