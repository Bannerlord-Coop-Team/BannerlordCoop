using Common.Messaging;
using Common.Util;
using GameInterface.Services.MapEvents.Messages;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
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
/// <para>
/// Mounts are registered with their own identity (see <c>OwnedAgentReplicator</c>/<c>PuppetSpawner</c>), so a
/// blow against a registered horse is gated by the HORSE's own authority (via
/// <see cref="BattleSpawnGate.AgentAuthority"/> — this static patch cannot reach the per-mission registry).
/// That also covers a masterless horse whose rider died. An UNregistered horse (e.g. a loose native one) falls
/// back to its rider's ownership. All of that puppet decision-making lives in
/// <see cref="IAgentAuthority.IsPuppet"/>.
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
        if (AllowedThread.IsThisThreadAllowed()) return true;

        if (__instance == null) return true;
        if (BattleSpawnGate.IsReplicatedDeath(__instance)) return true;

        // The authority seam is installed for the lifetime of the coop battle; if it is somehow absent, fall
        // back to applying the blow locally (the pre-seam default for own agents).
        var authority = BattleSpawnGate.AgentAuthority;
        if (authority == null) return true;

        // A missile blow credited to a puppet (another client's agent) is the replicated cosmetic siege stone;
        // its owner routes the real agent damage, so drop it here. Wall damage lands on a different path, so the
        // peer's walls still break from their own replica shot.
        if (blow.IsMissile)
        {
            var shooter = Mission.Current?.FindAgentWithIndex(blow.OwnerId);
            if (shooter != null && shooter.Controller == AgentControllerType.None)
                return false;
        }

        bool isMount = !__instance.IsHuman;

        // One decision for every agent kind: registered → authority compare; unregistered human → engine
        // Controller==None; unregistered mount → rider-keyed fallback (the old MountAuthorityProbe tri-state now
        // lives inside IsPuppet). Not a puppet → take the blow locally.
        if (!authority.IsPuppet(__instance)) return true;

        var attacker = Mission.Current?.FindAgentWithIndex(blow.OwnerId);
        PlaySuppressedHitSound(__instance, attacker, blow, collisionData);

        // Suppress locally and route the WHOLE blow (+ collision data) to the victim's owner, which re-applies
        // it through Agent.RegisterBlow so the engine resolves real damage/ragdoll/death. blow.OwnerId is the
        // attacker's LOCAL index here, resolve the agent so the owner can re-map it to its own local index.
        if (blow.InflictedDamage > 0)
        {
            MessageBroker.Instance.Publish(__instance, new BattlePuppetHit(__instance, attacker, blow, collisionData, isMount));
        }
        return false;
    }

    private static void PlaySuppressedHitSound(Agent victim, Agent attacker, Blow blow, AttackCollisionData collisionData)
    {
        if (blow.BlowFlag.HasAnyFlag(BlowFlags.NoSound)) return;

        var mission = victim.Mission ?? Mission.Current;
        if (mission == null) return;

        bool isCriticalBlow = blow.IsBlowCrit(victim.Monster.HitPoints * 4);
        bool isLowBlow = blow.IsBlowLow(victim.Monster.HitPoints);
        bool isOwnerHumanoid = attacker?.IsHuman ?? true;
        bool isNonTipThrust = blow.BlowFlag.HasAnyFlag(BlowFlags.NonTipThrust);
        int hitSound = blow.WeaponRecord.GetHitSound(isOwnerHumanoid, isCriticalBlow, isLowBlow, isNonTipThrust, blow.AttackType, blow.DamageType);
        float soundParameterForArmorType = Agent.GetSoundParameterForArmorType(victim.GetProtectorArmorMaterialOfBone(blow.BoneIndex));
        var parameter = new SoundEventParameter("Armor Type", soundParameterForArmorType);

        mission.MakeSound(hitSound, blow.GlobalPosition, soundCanBePredicted: false, isReliable: true, blow.OwnerId, victim.Index, ref parameter);
        if (blow.IsMissile && attacker != null)
            mission.MakeSoundOnlyOnRelatedPeer(CombatSoundContainer.SoundCodeMissionCombatPlayerhit, blow.GlobalPosition, attacker.Index);

        if (!collisionData.IsSneakAttack)
            mission.AddSoundAlarmFactorToAgents(attacker, in blow.GlobalPosition, 7f);
    }
}
