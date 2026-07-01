using Common.Messaging;
using GameInterface.Services.MapEvents.Messages;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// In a coop field battle, route damage dealt to a PUPPET (an agent owned by another client) to that agent's
/// owner instead of applying it locally. Each client only simulates the troops it owns; everyone else's are
/// inert puppets (<see cref="AgentControllerType.None"/>). Without this, a local troop kills enemy puppets
/// locally — deaths the owner never hears about — and the two clients' battles diverge. Here a damaging blow
/// on a puppet is SUPPRESSED locally and published as <see cref="BattlePuppetHit"/>; the Missions controller
/// forwards it to the owner, which applies it to the real agent and broadcasts any resulting death. Own agents
/// (AI/Player controller) take the blow normally.
/// <para>
/// Puppets are inert (never attack), so every blow originates from a locally-controlled agent — only the
/// VICTIM's ownership matters here.
/// </para>
/// </summary>
[HarmonyPatch(typeof(Agent), nameof(Agent.RegisterBlow))]
internal class BattleBlowInterceptPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Agent __instance, Blow blow, ref AttackCollisionData collisionData)
    {
        if (!BattleSpawnConfig.Enabled) return true;
        if (!BattleSpawnGate.IsCoopBattleActive) return true;

        // A puppet is an agent this client does not own: own troops are AI-controlled and the own hero is
        // Player; every replicated puppet is spawned with AgentControllerType.None. Non-puppet victims take
        // the blow locally as usual.
        if (__instance == null || !__instance.IsHuman || __instance.Controller != AgentControllerType.None)
            return true;

        // Suppress locally and route the WHOLE blow (+ collision data) to the puppet's owner, which re-applies
        // it through Agent.RegisterBlow so the engine resolves real damage/ragdoll/death. blow.OwnerId is the
        // attacker's LOCAL index here — resolve the agent so the owner can re-map it to its own local index.
        if (blow.InflictedDamage > 0)
        {
            var attacker = Mission.Current?.FindAgentWithIndex(blow.OwnerId);
            MessageBroker.Instance.Publish(__instance, new BattlePuppetHit(__instance, attacker, blow, collisionData));
        }
        return false;
    }
}
