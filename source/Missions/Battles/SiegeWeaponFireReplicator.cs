using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using Missions.Messages;
using Serilog;
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
    private static readonly ILogger Logger = LogManager.GetLogger<SiegeWeaponFireReplicator>();

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

        // TEMP diagnostic: confirms the host captures + broadcasts, and whether the pilot resolved to an id.
        Logger.Information("[SiegeFireDiag] host fire machine={Machine} shooter={Shooter} item={Item}",
            fire.Weapon.Id.Id, shooterId, fire.MissileItem?.StringId ?? "null");

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

            var weapon = FindWeapon(msg.MachineId);
            // TEMP diagnostic: confirms the peer received the shot and resolved the machine.
            Logger.Information("[SiegeFireDiag] peer recv machine={Machine} found={Found} shooter={Shooter} item={Item}",
                msg.MachineId, weapon != null, msg.ShooterAgentId, msg.MissileItemId ?? "null");
            if (weapon == null) return;

            PlayFireAnimation(weapon);
            SpawnProjectile(weapon, msg);
        });
    }

    private static RangedSiegeWeapon FindWeapon(int machineId)
    {
        foreach (var missionObject in Mission.Current.MissionObjects)
        {
            if (missionObject is RangedSiegeWeapon weapon && weapon.Id.Id == machineId) return weapon;
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
            if (shooter == null)
            {
                Logger.Information("[SiegeFireDiag] peer spawn skipped machine={Machine}: no shooter stand-in", weapon.Id.Id);
                return;
            }
        }

        var missileItem = MBObjectManager.Instance.GetObject<ItemObject>(msg.MissileItemId);
        if (missileItem == null) return;

        // Ammo count 1 matches the simulator's own launch exactly (physics-inert, cosmetic parity).
        var missileWeapon = new MissionWeapon(missileItem, null, null, 1);
        Mission.Current.AddCustomMissile(shooter, missileWeapon, msg.Position, msg.Direction, msg.Orientation, msg.BaseSpeed, msg.Speed, addRigidBody: false, weapon);
        // TEMP diagnostic: confirms the cosmetic stone actually spawned.
        Logger.Information("[SiegeFireDiag] peer spawn machine={Machine} item={Item}", weapon.Id.Id, msg.MissileItemId);
    }

    private static Agent FindStandInShooter()
    {
        foreach (var agent in Mission.Current.Agents)
        {
            if (agent.IsHuman && agent.IsActive() && agent.Controller == AgentControllerType.None) return agent;
        }

        return null;
    }

    // [Host] our ram struck the gate — broadcast so peers play the swing (their unmanned ram never strikes).
    private void Handle_LocalRamHit(MessagePayload<RamHitStarted> payload)
    {
        var hit = payload.What;
        network.SendAll(new NetworkRamHit(hit.Ram.Id.Id, hit.PowerStage, hit.Progress));
    }

    // [Peer] the host's ram struck — play the ram body swing animation.
    private void Handle_NetworkRamHit(MessagePayload<NetworkRamHit> payload)
    {
        var msg = payload.What;

        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;
            if (SiegeMissionAuthorityGate.IsLocalAuthority) return;

            var ram = FindRam(msg.MachineId);
            var body = ram?._batteringRamBody?.GetFirstScriptOfType<SynchedMissionObject>();
            if (body == null) return;

            // Mirror BatteringRam.StartHitAnimationWithProgress's body animation; the crew's pull animation
            // rides the normal agent sync for the host's troops.
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

    private static BatteringRam FindRam(int machineId)
    {
        foreach (var missionObject in Mission.Current.MissionObjects)
        {
            if (missionObject is BatteringRam ram && ram.Id.Id == machineId) return ram;
        }
        return null;
    }

    // [Host] a ram struck a gate hard enough to react — broadcast so peers replay the flinch + sound.
    private void Handle_LocalGateHit(MessagePayload<GateHitByRam> payload)
    {
        network.SendAll(new NetworkGateHit(payload.What.Gate.Id.Id));
    }

    // [Peer] replay the gate hit reaction: door/plank flinch, heavy-hit particles, and the impact sound. Damage
    // and destruction level are synced separately, so this only plays the reaction, mirroring CastleGate.OnHitTaken.
    private void Handle_NetworkGateHit(MessagePayload<NetworkGateHit> payload)
    {
        var msg = payload.What;

        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;
            if (SiegeMissionAuthorityGate.IsLocalAuthority) return;

            var gate = FindGate(msg.GateId);
            if (gate == null) return;

            gate._door?.SetAnimationAtChannelSynched(gate.HitAnimationName, 0);
            gate._plank?.SetAnimationAtChannelSynched(gate.PlankHitAnimationName, 0);
            gate.DestructionComponent?.BurstHeavyHitParticles();
            Mission.Current.MakeSound(SoundEvent.GetEventIdFromString("event:/mission/siege/door/hit"),
                gate.GameEntity.GlobalPosition, soundCanBePredicted: false, isReliable: true, -1, -1);
        });
    }

    private static CastleGate FindGate(int gateId)
    {
        foreach (var missionObject in Mission.Current.MissionObjects)
        {
            if (missionObject is CastleGate gate && gate.Id.Id == gateId) return gate;
        }
        return null;
    }
}
