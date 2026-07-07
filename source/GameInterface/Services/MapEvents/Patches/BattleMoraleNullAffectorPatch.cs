using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers.Logic;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Guards the native battle-morale interaction against a null affector during a coop battle.
/// <para>
/// Peers reflect a replicated death on their puppet, which can fire <see cref="Mission.OnAgentRemoved"/> with
/// a <c>null</c> affector when the local killer is missing. The vanilla
/// <c>AgentMoraleInteractionLogic.OnAgentRemoved</c> then calls
/// <c>SandboxBattleMoraleModel.CalculateMaxMoraleChangeDueToAgentIncapacitated</c>, which dereferences
/// <c>affectorAgent.Formation</c> WITHOUT a null check (every other affector access in that method is
/// guarded) → <see cref="System.NullReferenceException"/>. Vanilla never reaches this state because real
/// combat deaths always carry an affector.
/// </para>
/// <para>
/// Live-mission morale is the host's authority in a coop battle, so a death with no local affector has no
/// morale shock to attribute here. Skip the interaction in that case. Gated to coop battles
/// (see <see cref="BattleSpawnGate"/>) so single-player and non-battle behaviour is untouched.
/// </para>
/// </summary>
// OnAgentRemoved can be overloaded across the MissionBehavior hierarchy — pin the arg types so PatchAll
// can't become ambiguous.
[HarmonyPatch(typeof(AgentMoraleInteractionLogic), nameof(AgentMoraleInteractionLogic.OnAgentRemoved),
    new[] { typeof(Agent), typeof(Agent), typeof(AgentState), typeof(KillingBlow) })]
internal class BattleMoraleNullAffectorPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Agent affectorAgent)
    {
        // No local affector during a coop battle means skip the morale interaction (and the unguarded
        // affectorAgent.Formation deref). True original otherwise.
        return !(affectorAgent == null && BattleSpawnGate.IsCoopBattleActive);
    }
}
