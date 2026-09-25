using Missions.Messages;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

public interface ISiegeGateHitApplier
{
    // False means scene registration is incomplete; native application failures must not be retried.
    bool TryApply(NetworkGateHit message, bool applyDamage);
}

public class SiegeGateHitApplier : ISiegeGateHitApplier
{
    public bool TryApply(NetworkGateHit message, bool applyDamage)
    {
        var gate = FindMissionObject<CastleGate>(message.GateId);
        if (gate == null) return false;

        if (applyDamage)
        {
            var ram = FindMissionObject<BatteringRam>(message.RamId);
            if (ram == null || gate.DestructionComponent == null) return false;

            gate.DestructionComponent.TriggerOnHit(null, message.Damage, gate.GameEntity.GlobalPosition,
                gate.GameEntity.GetGlobalFrame().rotation.f, in MissionWeapon.Invalid, -1, ram);
            return true;
        }

        if (message.Damage < 200 || gate.State != CastleGate.GateState.Closed) return true;

        gate._door?.SetAnimationAtChannelSynched(gate.HitAnimationName, 0);
        gate._plank?.SetAnimationAtChannelSynched(gate.PlankHitAnimationName, 0);
        gate.DestructionComponent?.BurstHeavyHitParticles();
        Mission.Current.MakeSound(SoundEvent.GetEventIdFromString("event:/mission/siege/door/hit"),
            gate.GameEntity.GlobalPosition, soundCanBePredicted: false, isReliable: true, -1, -1);
        return true;
    }

    private static T FindMissionObject<T>(int id) where T : MissionObject
    {
        foreach (var missionObject in Mission.Current.MissionObjects)
        {
            if (missionObject is T match && match.Id.Id == id) return match;
        }
        return null;
    }
}
