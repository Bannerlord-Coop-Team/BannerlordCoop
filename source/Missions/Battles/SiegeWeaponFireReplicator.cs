using Common;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using Missions.Messages;
using System;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Missions.Battles;

/// <summary>
/// Replicates siege weapon animations between the mission clients: ranged weapon firing and battering ram
/// strikes. One client simulates each machine (the mission host, or a peer that claimed it by manning it —
/// see <see cref="SiegeMachineStateReplicator"/>); everyone else's copy is unmanned, so its arm never swings,
/// no stone spawns, and its ram never strikes. The capture patches report each simulator shot / ram hit; here
/// we broadcast it and every other client plays the machine animation, plus (for a shot) spawns a cosmetic
/// projectile with the resolved launch. Damage stays simulator-authoritative — the replayed stone is fired by
/// a non-locally-controlled puppet, so <c>BattleBlowInterceptPatch</c> drops its blows.
/// </summary>
public interface ISiegeWeaponFireReplicator : IDisposable
{
}

/// <inheritdoc cref="ISiegeWeaponFireReplicator"/>
public class SiegeWeaponFireReplicator : ISiegeWeaponFireReplicator
{
    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly INetworkAgentRegistry registry;

    public SiegeWeaponFireReplicator(IBattleNetwork network, IMessageBroker messageBroker, INetworkAgentRegistry registry)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.registry = registry;

        messageBroker.Subscribe<SiegeWeaponFired>(Handle_LocalFire);
        messageBroker.Subscribe<NetworkSiegeWeaponFired>(Handle_NetworkFire);
        messageBroker.Subscribe<RamHitStarted>(Handle_LocalRamHit);
        messageBroker.Subscribe<NetworkRamHit>(Handle_NetworkRamHit);
        messageBroker.Subscribe<GateHitByRam>(Handle_LocalGateHit);
        messageBroker.Subscribe<NetworkGateHit>(Handle_NetworkGateHit);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SiegeWeaponFired>(Handle_LocalFire);
        messageBroker.Unsubscribe<NetworkSiegeWeaponFired>(Handle_NetworkFire);
        messageBroker.Unsubscribe<RamHitStarted>(Handle_LocalRamHit);
        messageBroker.Unsubscribe<NetworkRamHit>(Handle_NetworkRamHit);
        messageBroker.Unsubscribe<GateHitByRam>(Handle_LocalGateHit);
        messageBroker.Unsubscribe<NetworkGateHit>(Handle_NetworkGateHit);
    }

    // [Owner] one of our simulated siege machines fired — broadcast the resolved launch so the rest replay it.
    private void Handle_LocalFire(MessagePayload<SiegeWeaponFired> payload)
    {
        var fire = payload.What;

        Guid shooterId = Guid.Empty;
        if (fire.Shooter != null && registry.TryGetAgentInfo(fire.Shooter, out var info))
            shooterId = info.AgentId;

        network.SendAll(new NetworkSiegeWeaponFired(
            fire.Weapon.Id.Id, shooterId, fire.Position, fire.Direction, fire.Orientation, fire.BaseSpeed, fire.Speed, fire.MissileItem?.StringId));
    }

    // Another client's simulated siege machine fired — swing the arm and throw a cosmetic stone.
    private void Handle_NetworkFire(MessagePayload<NetworkSiegeWeaponFired> payload)
    {
        var msg = payload.What;

        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;
            // The machine's simulator fired natively; everyone else replays it.
            if (SiegeMissionAuthorityGate.IsMachineSimulatedLocally(msg.MachineId)) return;

            var weapon = FindMissionObject<RangedSiegeWeapon>(msg.MachineId);
            if (weapon == null) return;

            PlayFireAnimation(weapon);
            SpawnProjectile(weapon, msg);
        });
    }

    private static T FindMissionObject<T>(int id) where T : MissionObject
    {
        foreach (var missionObject in Mission.Current.MissionObjects)
        {
            if (missionObject is T match && match.Id.Id == id) return match;
        }
        return null;
    }

    // Vanilla plays FireAnimations on the machine's skeleton owners when it enters WaitingBeforeProjectileLeaving
    // (RangedSiegeWeapon.OnRangedSiegeWeaponStateChange); a peer never reaches that state, so drive it directly.
    private static void PlayFireAnimation(RangedSiegeWeapon weapon)
    {
        var skeletons = weapon.SkeletonOwnerObjects;
        var animations = weapon.FireAnimations;
        if (skeletons == null || animations == null) return;

        for (int i = 0; i < skeletons.Length && i < animations.Length; i++)
        {
            if (skeletons[i] == null || string.IsNullOrEmpty(animations[i])) continue;
            skeletons[i].SetAnimationAtChannelSynched(animations[i], 0);
        }
    }

    private void SpawnProjectile(RangedSiegeWeapon weapon, NetworkSiegeWeaponFired msg)
    {
        if (string.IsNullOrEmpty(msg.MissileItemId) || msg.ShooterAgentId == Guid.Empty) return;

        Agent shooter = null;
        if (registry.TryGetAgentInfo(msg.ShooterAgentId, out var info) && info.Agent != null)
        {
            // Spawn only with a non-local shooter, so the cosmetic stone's blows are dropped (attacker not
            // locally controlled) — the real damage arrives as routed blows and synced hit points.
            if (registry.IsLocallyControlled(info.Agent)) return;
            shooter = info.Agent;
        }
        else
        {
            // The simulator's crew agent may not exist here (an unattributed puppet); the stone must still
            // fly. Any inert puppet keeps the blow-drop guarantee — it keys on Controller.None, not on the
            // shooter's identity. AddCustomMissile cannot take a null shooter.
            shooter = FindStandInShooter();
            if (shooter == null) return;
        }

        var missileItem = MBObjectManager.Instance.GetObject<ItemObject>(msg.MissileItemId);
        if (missileItem == null) return;

        // Ammo count 1 matches the simulator's own launch exactly (physics-inert, cosmetic parity).
        var missileWeapon = new MissionWeapon(missileItem, null, null, 1);
        Mission.Current.AddCustomMissile(shooter, missileWeapon, msg.Position, msg.Direction, msg.Orientation, msg.BaseSpeed, msg.Speed, addRigidBody: false, weapon);
    }

    private static Agent FindStandInShooter()
    {
        foreach (var agent in Mission.Current.Agents)
        {
            if (agent.IsHuman && agent.IsActive() && agent.Controller == AgentControllerType.None) return agent;
        }

        return null;
    }

    // [Simulator] our ram struck the gate — broadcast so everyone else plays the swing (their unmanned ram
    // never strikes).
    private void Handle_LocalRamHit(MessagePayload<RamHitStarted> payload)
    {
        var hit = payload.What;
        network.SendAll(new NetworkRamHit(hit.Ram.Id.Id, hit.PowerStage, hit.Progress));
    }

    // The simulator's ram struck — play the ram body swing animation. Rams are crew-grantable, so the
    // gate is per-machine: the host must replay a client-simulated ram's swing too.
    private void Handle_NetworkRamHit(MessagePayload<NetworkRamHit> payload)
    {
        var msg = payload.What;

        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;
            if (SiegeMissionAuthorityGate.IsMachineSimulatedLocally(msg.MachineId)) return;

            var ram = FindMissionObject<BatteringRam>(msg.MachineId);
            var body = ram?._batteringRamBody?.GetFirstScriptOfType<SynchedMissionObject>();
            if (body == null) return;

            // Mirror BatteringRam.StartHitAnimationWithProgress's body animation; the crew's pull animation
            // rides the normal agent sync for the simulator's troops.
            body.SetAnimationAtChannelSynched(RamHitAnimation(msg.PowerStage), 0);
            if (msg.Progress > 0f) body.SetAnimationChannelParameterSynched(0, msg.Progress);
        });
    }

    private static string RamHitAnimation(int powerStage) => powerStage switch
    {
        1 => "batteringram_fire_weak",
        2 => "batteringram_fire",
        _ => "batteringram_fire_weakest",
    };

    // [Simulator] our ram hit a gate — broadcast the hit with its damage so the host applies it to the
    // authoritative gate and everyone replays the reaction.
    private void Handle_LocalGateHit(MessagePayload<GateHitByRam> payload)
    {
        network.SendAll(new NetworkGateHit(payload.What.Gate.Id.Id, payload.What.Ram.Id.Id, payload.What.Damage));
    }

    // A granted ram strikes only on its simulator, so the host (gate authority — gates are never claimed)
    // applies the carried damage through vanilla TriggerOnHit: its own OnHitTaken plays the reaction and
    // the synced hit points/destruction carry the damage to everyone. Other peers replay the cosmetic
    // reaction, mirroring CastleGate.OnHitTaken's condition.
    private void Handle_NetworkGateHit(MessagePayload<NetworkGateHit> payload)
    {
        var msg = payload.What;

        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;
            if (SiegeMissionAuthorityGate.IsMachineSimulatedLocally(msg.RamId)) return;

            var gate = FindMissionObject<CastleGate>(msg.GateId);
            if (gate == null) return;

            if (SiegeMissionAuthorityGate.IsMachineSimulatedLocally(msg.GateId))
            {
                var ram = FindMissionObject<BatteringRam>(msg.RamId);
                if (ram == null || gate.DestructionComponent == null) return;

                gate.DestructionComponent.TriggerOnHit(null, msg.Damage, gate.GameEntity.GlobalPosition,
                    gate.GameEntity.GetGlobalFrame().rotation.f, in MissionWeapon.Invalid, -1, ram);
                return;
            }

            if (msg.Damage < 200 || gate.State != CastleGate.GateState.Closed) return;

            gate._door?.SetAnimationAtChannelSynched(gate.HitAnimationName, 0);
            gate._plank?.SetAnimationAtChannelSynched(gate.PlankHitAnimationName, 0);
            gate.DestructionComponent?.BurstHeavyHitParticles();
            Mission.Current.MakeSound(SoundEvent.GetEventIdFromString("event:/mission/siege/door/hit"),
                gate.GameEntity.GlobalPosition, soundCanBePredicted: false, isReliable: true, -1, -1);
        });
    }
}
