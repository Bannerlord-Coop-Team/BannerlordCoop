using System;
using System.Collections.Generic;
using System.Reflection;
using Autofac;
using E2E.Tests.Environment.Instance;
using GameInterface;
using GameInterface.Services.MapEvents;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace E2E.Tests.Environment.MockEngine;

/// <summary>
/// Installs Harmony prefixes that redirect the engine calls the coop battle flow makes — <see cref="Mission"/>
/// and <see cref="Agent"/> members — onto a per-client <see cref="MockMission"/> mirror, so spawn/damage/death
/// can run headless. Uses a dedicated <see cref="Harmony"/> instance and unpatches on <see cref="Dispose"/>
/// (shared-static Harmony made lifetime patching order-flaky). Create one per test with <c>using</c>.
/// <para>
/// The active client is the one inside <c>instance.Call(...)</c>: that scope sets the active container, which
/// the <c>Mission.Current</c> shim maps to its <see cref="MockMission"/>. Agent members resolve via the global
/// <see cref="AgentMirror"/> side-table, so they work regardless of which client owns the agent.
/// </para>
/// </summary>
public sealed class MissionEngineFixture : IDisposable
{
    private readonly Harmony harmony = new("e2e.mockengine");
    private static readonly Dictionary<ILifetimeScope, MockMission> ByContainer = new();

    public MissionEngineFixture()
    {
        // Mission statics / members
        Prefix(typeof(Mission), "get_Current", nameof(Mission_get_Current));
        Prefix(typeof(Mission), "get_CurrentTime", nameof(Mission_get_CurrentTime));
        Prefix(typeof(Mission), nameof(Mission.SpawnAgent), nameof(Mission_SpawnAgent));
        Prefix(typeof(Mission), "get_MainAgent", nameof(Mission_get_MainAgent));
        Prefix(typeof(Mission), "set_MainAgent", nameof(Mission_set_MainAgent));
        Prefix(typeof(Mission), nameof(Mission.FindAgentWithIndex), nameof(Mission_FindAgentWithIndex));
        // Per-side teams — the reinforcement spawn resolves the side's team to field a new party into.
        Prefix(typeof(Mission), "get_AttackerTeam", nameof(Mission_get_AttackerTeam));
        Prefix(typeof(Mission), "get_DefenderTeam", nameof(Mission_get_DefenderTeam));
        Prefix(typeof(Mission), "get_PlayerEnemyTeam", nameof(Mission_get_PlayerEnemyTeam));
        // The non-host retreat despawn filters the retreater's troops by the player team's side.
        Prefix(typeof(Mission), "get_PlayerTeam", nameof(Mission_get_PlayerTeam));
        Prefix(typeof(Team), "get_Side", nameof(Team_get_Side));
        // GetMissionBehavior<T> walks the mission's behavior list, which a skip-ctor shell doesn't have (NRE).
        // The spawn-capture and deployment paths probe for DeploymentMissionController — answer "none" for mock
        // missions. Reference-type instantiations share one method body, so patching this one covers them all.
        harmony.Patch(
            AccessTools.Method(typeof(Mission), nameof(Mission.GetMissionBehavior)).MakeGenericMethod(typeof(DeploymentMissionController)),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(MissionEngineFixture), nameof(Mission_GetMissionBehavior))));

        // Agent members
        Prefix(typeof(Agent), "get_Controller", nameof(Agent_get_Controller));
        Prefix(typeof(Agent), "set_Controller", nameof(Agent_set_Controller));
        Prefix(typeof(Agent), "get_Health", nameof(Agent_get_Health));
        Prefix(typeof(Agent), "set_Health", nameof(Agent_set_Health));
        Prefix(typeof(Agent), "get_Index", nameof(Agent_get_Index));
        Prefix(typeof(Agent), "get_Character", nameof(Agent_get_Character));
        Prefix(typeof(Agent), "get_Team", nameof(Agent_get_Team));
        Prefix(typeof(Agent), "get_Position", nameof(Agent_get_Position));
        Prefix(typeof(Agent), "get_Name", nameof(Agent_get_Name));
        Prefix(typeof(Agent), nameof(Agent.IsActive), nameof(Agent_IsActive));
        // Puppet classification (LocationPvpBlockPatch): human/mount/rider resolve via the mirror.
        Prefix(typeof(Agent), "get_IsHuman", nameof(Agent_get_IsHuman));
        Prefix(typeof(Agent), "get_IsMount", nameof(Agent_get_IsMount));
        Prefix(typeof(Agent), "get_RiderAgent", nameof(Agent_get_RiderAgent));

        // RegisterBlow is overloaded — pin the (Blow, in AttackCollisionData) signature.
        harmony.Patch(
            AccessTools.Method(typeof(Agent), nameof(Agent.RegisterBlow), new[] { typeof(Blow), typeof(AttackCollisionData).MakeByRefType() }),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(MissionEngineFixture), nameof(Agent_RegisterBlow))));

        // Team / Formation — host-migration adoption (ConvertPuppetToHostAi + the Charge order).
        Prefix(typeof(Agent), "get_Formation", nameof(Agent_get_Formation));
        Prefix(typeof(Agent), "set_Formation", nameof(Agent_set_Formation));
        Prefix(typeof(Agent), nameof(Agent.SetIsAIPaused), nameof(Agent_SetIsAIPaused));
        Prefix(typeof(Agent), nameof(Agent.SetAlarmState), nameof(Agent_SetAlarmState));
        Prefix(typeof(Agent), nameof(Agent.ResetEnemyCaches), nameof(Agent_ResetEnemyCaches));
        Prefix(typeof(Agent), nameof(Agent.FadeIn), nameof(Agent_FadeIn));
        // Retreat/adoption paths: own-party classification reads the origin, the adoption clears a mount's
        // interpolation target, and the retreat despawn fades the withdrawing troops out.
        Prefix(typeof(Agent), "get_Origin", nameof(Agent_get_Origin));
        Prefix(typeof(Agent), "get_MountAgent", nameof(Agent_get_MountAgent));
        Prefix(typeof(Agent), nameof(Agent.FadeOut), nameof(Agent_FadeOut));
        // Mount identity (#1750): registration, routing and death sync read the rider/mount relationship;
        // the movement sync's SyncMountState also assigns MountAgent and reads HasMount.
        Prefix(typeof(Agent), "get_HasMount", nameof(Agent_get_HasMount));
        Prefix(typeof(Agent), "set_MountAgent", nameof(Agent_set_MountAgent));
        // MakeDead is still used by non-battle despawn paths — pin the full (bool, ActionIndexCache, int)
        // signature (the int is a defaulted param callers don't see).
        harmony.Patch(
            AccessTools.Method(typeof(Agent), nameof(Agent.MakeDead), new[] { typeof(bool), typeof(ActionIndexCache), typeof(int) }),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(MissionEngineFixture), nameof(Agent_MakeDead))));
        // Callers pass ActionIndexCache.act_none, whose TYPE INITIALIZER creates dozens of named actions, each
        // resolving its index through MBAnimation.GetActionCodeWithName — a native engine call that throws
        // headless and would poison the type for the whole process (a failed cctor is cached). Answer
        // "unresolved" (-1) instead so the cctor completes.
        Prefix(typeof(MBAnimation), nameof(MBAnimation.GetActionCodeWithName), nameof(MBAnimation_GetActionCodeWithName));
        // Standalone mount movement: a masterless horse's own AgentMountData capture/apply reads and writes
        // the movement natives, and the apply path's staleness guard compares agent.Mission to Mission.Current.
        Prefix(typeof(Agent), "get_Mission", nameof(Agent_get_Mission));
        Prefix(typeof(Agent), "get_LookDirection", nameof(Agent_get_LookDirection));
        Prefix(typeof(Agent), "set_LookDirection", nameof(Agent_set_LookDirection));
        Prefix(typeof(Agent), nameof(Agent.GetMovementDirection), nameof(Agent_GetMovementDirection));
        Prefix(typeof(Agent), nameof(Agent.SetMovementDirection), nameof(Agent_SetMovementDirection));
        Prefix(typeof(Agent), "get_MovementInputVector", nameof(Agent_get_MovementInputVector));
        Prefix(typeof(Agent), "set_MovementInputVector", nameof(Agent_set_MovementInputVector));
        // AgentMountData also snapshots action channel 1; report "no action" so capture works headless (the
        // apply side's GetActionNameWithCode already returns null headless and skips SetActionChannel).
        Prefix(typeof(Agent), nameof(Agent.GetCurrentAction), nameof(Agent_GetCurrentAction));
        Prefix(typeof(Agent), nameof(Agent.GetCurrentAnimationFlag), nameof(Agent_GetCurrentAnimationFlag));
        Prefix(typeof(Agent), nameof(Agent.GetCurrentActionProgress), nameof(Agent_GetCurrentActionProgress));
        Prefix(typeof(Team), nameof(Team.GetFormation), nameof(Team_GetFormation));
        Prefix(typeof(Formation), nameof(Formation.SetControlledByAI), nameof(Formation_SetControlledByAI));
        Prefix(typeof(Formation), nameof(Formation.SetMovementOrder), nameof(Formation_SetMovementOrder));
        // The adoption/reclaim bookkeeping keys formations in a HashSet, but the native GetHashCode/Equals
        // overrides read engine state a skip-ctor shell doesn't have (NRE) — which silently aborted every
        // MULTI-agent adoption after the first agent's conversion headless (single-agent tests never noticed:
        // the throw came after that agent was already converted). Mocked shells hash and compare by identity.
        harmony.Patch(
            AccessTools.DeclaredMethod(typeof(Formation), nameof(GetHashCode))
                ?? throw new MissingMethodException(typeof(Formation).FullName, nameof(GetHashCode)),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(MissionEngineFixture), nameof(Formation_GetHashCode))));
        var formationEquals = AccessTools.DeclaredMethod(typeof(Formation), nameof(Equals), new[] { typeof(object) });
        if (formationEquals != null)
        {
            harmony.Patch(formationEquals,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(MissionEngineFixture), nameof(Formation_Equals))));
        }
        // NOTE: the coop adoption path's final step — Formation.SetMovementOrder(MovementOrder.MovementOrderCharge)
        // — can't run headless: the MovementOrder type initializer builds Timers from Mission.CurrentTime and
        // reads WorldPosition statics (engine-populated natives), so it NREs, and the static constants are
        // initonly so they can't be faked after init. That call is therefore swallowed by the coop RunSafe; the
        // migration test verifies everything UP TO it (AI conversion, formation, AI-control, authority).
    }

    /// <summary>Create + register a mock mission for a client. Call inside that client's scope.</summary>
    public MockMission CreateMission(EnvironmentInstance instance)
    {
        var mock = new MockMission();
        ByContainer[instance.Container] = mock;
        return mock;
    }

    private void Prefix(Type type, string method, string patch)
    {
        var target = AccessTools.Method(type, method) ?? throw new MissingMethodException(type.FullName, method);
        harmony.Patch(target, prefix: new HarmonyMethod(AccessTools.Method(typeof(MissionEngineFixture), patch)));
    }

    private static bool TryActiveMock(out MockMission mock)
    {
        mock = null;
        return ContainerProvider.TryGetContainer(out var container) && ByContainer.TryGetValue(container, out mock);
    }

    // ---- Mission shims ----
    private static bool Mission_get_Current(ref Mission __result)
    {
        if (!TryActiveMock(out var mock)) return true;
        __result = mock.Shell;
        return false;
    }

    // The MovementOrder ctor reads Mission.Current.CurrentTime (to seed a Timer); supply a value for mock shells
    // so building MovementOrder.MovementOrderCharge during host-migration adoption doesn't NRE headless.
    private static bool Mission_get_CurrentTime(Mission __instance, ref float __result)
    {
        if (!MockMission.ForShell(__instance, out _)) return true;
        __result = 0f;
        return false;
    }

    private static bool Mission_SpawnAgent(Mission __instance, AgentBuildData agentBuildData, ref Agent __result)
    {
        if (!MockMission.ForShell(__instance, out var mock)) return true;
        __result = mock.SpawnAgent(agentBuildData);
        return false;
    }

    private static bool Mission_GetMissionBehavior(Mission __instance, ref DeploymentMissionController __result)
    {
        if (!MockMission.ForShell(__instance, out _)) return true;
        __result = null;
        return false;
    }

    private static bool Mission_get_MainAgent(Mission __instance, ref Agent __result)
    {
        if (!MockMission.ForShell(__instance, out var mock)) return true;
        __result = mock.MainAgent;
        return false;
    }

    private static bool Mission_set_MainAgent(Mission __instance, Agent value)
    {
        if (!MockMission.ForShell(__instance, out var mock)) return true;
        mock.MainAgent = value;
        return false;
    }

    private static bool Mission_FindAgentWithIndex(Mission __instance, int agentId, ref Agent __result)
    {
        if (!MockMission.ForShell(__instance, out var mock)) return true;
        __result = mock.FindAgentWithIndex(agentId);
        return false;
    }

    private static bool Mission_get_AttackerTeam(Mission __instance, ref Team __result)
    {
        if (!MockMission.ForShell(__instance, out var mock)) return true;
        __result = mock.AttackerTeam.Shell;
        return false;
    }

    private static bool Mission_get_DefenderTeam(Mission __instance, ref Team __result)
    {
        if (!MockMission.ForShell(__instance, out var mock)) return true;
        __result = mock.DefenderTeam.Shell;
        return false;
    }

    // ResolveTeam only falls back to PlayerEnemyTeam for BattleSideEnum.None; map it to a real side team so the
    // shim never returns null.
    private static bool Mission_get_PlayerEnemyTeam(Mission __instance, ref Team __result)
    {
        if (!MockMission.ForShell(__instance, out var mock)) return true;
        __result = mock.AttackerTeam.Shell;
        return false;
    }

    private static bool Mission_get_PlayerTeam(Mission __instance, ref Team __result)
    {
        if (!MockMission.ForShell(__instance, out var mock)) return true;
        __result = mock.PlayerTeam?.Shell;
        return false;
    }

    private static bool Team_get_Side(Team __instance, ref BattleSideEnum __result)
    {
        if (!MockTeam.ForShell(__instance, out var team)) return true;
        __result = team.Side;
        return false;
    }

    // ---- Agent shims ----
    private static bool Agent_get_Controller(Agent __instance, ref AgentControllerType __result)
    {
        if (!AgentMirror.TryGet(__instance, out var m)) return true;
        __result = m.Controller;
        return false;
    }

    private static bool Agent_set_Controller(Agent __instance, AgentControllerType value)
    {
        if (!AgentMirror.TryGet(__instance, out var m)) return true;
        m.Controller = value;
        return false;
    }

    private static bool Agent_get_Health(Agent __instance, ref float __result)
    {
        if (!AgentMirror.TryGet(__instance, out var m)) return true;
        __result = m.Health;
        return false;
    }

    private static bool Agent_set_Health(Agent __instance, float value)
    {
        if (!AgentMirror.TryGet(__instance, out var m)) return true;
        m.Health = value;
        return false;
    }

    private static bool Agent_get_Index(Agent __instance, ref int __result)
    {
        if (!AgentMirror.TryGet(__instance, out var m)) return true;
        __result = m.Index;
        return false;
    }

    private static bool Agent_get_Character(Agent __instance, ref BasicCharacterObject __result)
    {
        if (!AgentMirror.TryGet(__instance, out var m)) return true;
        __result = m.Character;
        return false;
    }

    private static bool Agent_get_Team(Agent __instance, ref Team __result)
    {
        if (!AgentMirror.TryGet(__instance, out var m)) return true;
        __result = m.Team;
        return false;
    }

    private static bool Agent_get_Position(Agent __instance, ref Vec3 __result)
    {
        if (!AgentMirror.TryGet(__instance, out var m)) return true;
        __result = m.Position;
        return false;
    }

    private static bool Agent_IsActive(Agent __instance, ref bool __result)
    {
        if (!AgentMirror.TryGet(__instance, out var m)) return true;
        __result = m.IsActive;
        return false;
    }

    private static bool Agent_get_Name(Agent __instance, ref string __result)
    {
        if (!AgentMirror.TryGet(__instance, out var m)) return true;
        __result = m.Character?.StringId ?? "mock-agent";
        return false;
    }

    private static bool Agent_get_IsHuman(Agent __instance, ref bool __result)
    {
        if (!AgentMirror.TryGet(__instance, out var m)) return true;
        __result = m.IsHuman;
        return false;
    }

    private static bool Agent_get_IsMount(Agent __instance, ref bool __result)
    {
        if (!AgentMirror.TryGet(__instance, out var m)) return true;
        __result = m.IsMount;
        return false;
    }

    private static bool Agent_get_RiderAgent(Agent __instance, ref Agent __result)
    {
        if (!AgentMirror.TryGet(__instance, out var m)) return true;
        __result = m.RiderAgent;
        return false;
    }

    private static bool Agent_RegisterBlow(Agent __instance, Blow blow)
    {
        if (!AgentMirror.TryGet(__instance, out var victim)) return true;

        // Model Mission.OnAgentHit's missile lookup: for a missile blow it indexes Mission._missilesDictionary
        // by blow.WeaponRecord.AffectorWeaponSlotOrMissileIndex and throws KeyNotFound when that projectile is
        // not on this client (the live crash). A coop owner must clear the missile flag before re-applying a
        // routed blow — this reproduces / guards that bug class headlessly.
        if (blow.IsMissile && TryActiveMock(out var mock) && !mock.HasMissile(blow.WeaponRecord.AffectorWeaponSlotOrMissileIndex))
            throw new KeyNotFoundException(
                $"Missile index {blow.WeaponRecord.AffectorWeaponSlotOrMissileIndex} not in the mock mission's missile set (models Mission.OnAgentHit)");

        victim.Health -= blow.InflictedDamage;
        if (victim.Health < 1f)
        {
            victim.Health = 0f;
            victim.IsActive = false;
            if (BattleSpawnGate.TryGetReplicatedDeathState(__instance, out var agentState))
                victim.WasKilled = agentState == AgentState.Killed;
            if (BattleSpawnGate.TryGetReplicatedDeath(__instance, out _, out var killingBlow) && killingBlow.IsValid)
                victim.DeathAction = killingBlow.DeathAction;
            if (victim.MountAgent != null && AgentMirror.TryGet(victim.MountAgent, out var horse) && horse.RiderAgent == __instance)
                horse.RiderAgent = null;
            victim.MountAgent = null;
        }
        return false;
    }

    private static bool Agent_get_Formation(Agent __instance, ref Formation __result)
    {
        if (!AgentMirror.TryGet(__instance, out var m)) return true;
        __result = m.Formation;
        return false;
    }

    private static bool Agent_set_Formation(Agent __instance, Formation value)
    {
        if (!AgentMirror.TryGet(__instance, out var m)) return true;
        m.Formation = value;
        if (value != null && MockFormation.ForShell(value, out var mf)) mf.AddUnit(__instance);
        return false;
    }

    private static bool Agent_SetIsAIPaused(Agent __instance)
    {
        // No AI loop headless — accept the call so ConvertPuppetToHostAi doesn't deref the native agent.
        return !AgentMirror.TryGet(__instance, out _);
    }

    private static bool Agent_SetAlarmState(Agent __instance, ref bool __result)
    {
        // No AI loop headless — accept ConvertPuppetToHostAi's AI wake-up without dereffing the native agent.
        if (!AgentMirror.TryGet(__instance, out _)) return true;
        __result = true;
        return false;
    }

    private static bool Agent_ResetEnemyCaches(Agent __instance)
    {
        return !AgentMirror.TryGet(__instance, out _);
    }

    private static bool Agent_FadeIn(Agent __instance)
    {
        // No visual loop headless — accept the call so freshly spawned (reinforcement) agents don't deref natives.
        return !AgentMirror.TryGet(__instance, out _);
    }

    private static bool Agent_get_Origin(Agent __instance, ref IAgentOriginBase __result)
    {
        if (!AgentMirror.TryGet(__instance, out var m)) return true;
        __result = m.Origin;
        return false;
    }

    private static bool Agent_get_MountAgent(Agent __instance, ref Agent __result)
    {
        if (!AgentMirror.TryGet(__instance, out var m)) return true;
        __result = m.MountAgent;
        return false;
    }

    private static bool Agent_FadeOut(Agent __instance, bool hideMount)
    {
        // A faded-out agent is gone: mirror it as inactive so despawn paths (retreat withdrawal) are assertable.
        if (!AgentMirror.TryGet(__instance, out var m)) return true;
        m.IsActive = false;
        // Native FadeOut(_, hideMount: true) fades the agent's mount along with it — model the cascade so the
        // registered-horse-spared-vs-unregistered-horse-cascaded death paths are assertable.
        if (hideMount && m.MountAgent != null && AgentMirror.TryGet(m.MountAgent, out var horse))
            horse.IsActive = false;
        return false;
    }

    private static bool Agent_get_HasMount(Agent __instance, ref bool __result)
    {
        if (!AgentMirror.TryGet(__instance, out var m)) return true;
        __result = m.MountAgent != null;
        return false;
    }

    // Keeps the horse's RiderAgent back-reference in step, like the engine does on mount/dismount.
    private static bool Agent_set_MountAgent(Agent __instance, Agent value)
    {
        if (!AgentMirror.TryGet(__instance, out var m)) return true;
        if (m.MountAgent != null && AgentMirror.TryGet(m.MountAgent, out var oldHorse) && oldHorse.RiderAgent == __instance)
            oldHorse.RiderAgent = null;
        m.MountAgent = value;
        if (value != null && AgentMirror.TryGet(value, out var newHorse))
            newHorse.RiderAgent = __instance;
        return false;
    }

    private static bool Agent_MakeDead(Agent __instance, bool isKilled, ActionIndexCache actionIndex)
    {
        // A killed agent is gone: mirror it dead+inactive so death-broadcast paths are assertable.
        if (!AgentMirror.TryGet(__instance, out var m)) return true;
        m.Health = 0f;
        m.IsActive = false;
        m.WasKilled = isKilled;
        m.DeathAction = actionIndex.Index;
        return false;
    }

    private static bool MBAnimation_GetActionCodeWithName(ref int __result)
    {
        __result = -1;
        return false;
    }

    private static bool Agent_get_Mission(Agent __instance, ref Mission __result)
    {
        if (!AgentMirror.TryGet(__instance, out var m)) return true;
        __result = m.Mission;
        return false;
    }

    private static bool Agent_get_LookDirection(Agent __instance, ref Vec3 __result)
    {
        if (!AgentMirror.TryGet(__instance, out var m)) return true;
        __result = m.LookDirection;
        return false;
    }

    private static bool Agent_set_LookDirection(Agent __instance, Vec3 value)
    {
        if (!AgentMirror.TryGet(__instance, out var m)) return true;
        m.LookDirection = value;
        return false;
    }

    private static bool Agent_GetMovementDirection(Agent __instance, ref Vec2 __result)
    {
        if (!AgentMirror.TryGet(__instance, out var m)) return true;
        __result = m.MovementDirection;
        return false;
    }

    private static bool Agent_SetMovementDirection(Agent __instance, Vec2 __0)
    {
        if (!AgentMirror.TryGet(__instance, out var m)) return true;
        m.MovementDirection = __0;
        return false;
    }

    private static bool Agent_get_MovementInputVector(Agent __instance, ref Vec2 __result)
    {
        if (!AgentMirror.TryGet(__instance, out var m)) return true;
        __result = m.InputVector;
        return false;
    }

    private static bool Agent_set_MovementInputVector(Agent __instance, Vec2 value)
    {
        if (!AgentMirror.TryGet(__instance, out var m)) return true;
        m.InputVector = value;
        return false;
    }

    private static bool Agent_GetCurrentAction(Agent __instance, ref ActionIndexCache __result)
    {
        if (!AgentMirror.TryGet(__instance, out _)) return true;
        __result = ActionIndexCache.act_none; // safe: the MBAnimation shim above lets the cctor complete
        return false;
    }

    private static bool Agent_GetCurrentAnimationFlag(Agent __instance, ref AnimFlags __result)
    {
        if (!AgentMirror.TryGet(__instance, out _)) return true;
        __result = 0;
        return false;
    }

    private static bool Agent_GetCurrentActionProgress(Agent __instance, ref float __result)
    {
        if (!AgentMirror.TryGet(__instance, out _)) return true;
        __result = 0f;
        return false;
    }

    private static bool Team_GetFormation(Team __instance, FormationClass __0, ref Formation __result)
    {
        if (!MockTeam.ForShell(__instance, out var team)) return true;
        __result = team.GetFormation(__0).Shell;
        return false;
    }

    private static bool Formation_SetControlledByAI(Formation __instance, bool __0)
    {
        if (!MockFormation.ForShell(__instance, out var f)) return true;
        f.IsAIControlled = __0;
        return false;
    }

    private static bool Formation_SetMovementOrder(Formation __instance, MovementOrder __0)
    {
        if (!MockFormation.ForShell(__instance, out var f)) return true;
        f.MovementOrderSet = true;
        f.Order = __0.OrderEnum;
        return false;
    }

    private static bool Formation_GetHashCode(Formation __instance, ref int __result)
    {
        if (!MockFormation.ForShell(__instance, out _)) return true;
        __result = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(__instance);
        return false;
    }

    private static bool Formation_Equals(Formation __instance, object __0, ref bool __result)
    {
        if (!MockFormation.ForShell(__instance, out _)) return true;
        __result = ReferenceEquals(__instance, __0);
        return false;
    }

    public void Dispose()
    {
        harmony.UnpatchAll(harmony.Id);
        ByContainer.Clear();
    }
}
