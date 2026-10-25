using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>[Game thread] The animations and stand-in shooter a client that does not simulate a ranged siege weapon
/// shows its owner's state and shots with, shared by the siege and ship weapon replicators.</summary>
internal static class RangedSiegeWeaponPresentation
{
    /// <summary>[Game thread] The arm animation a non-simulating client plays when a ranged weapon enters a replicated state.</summary>
    internal static void PlayWeaponStateAnimation(RangedSiegeWeapon rangedWeapon, RangedSiegeWeapon.WeaponState weaponState)
    {
        var skeletons = rangedWeapon.SkeletonOwnerObjects;
        if (skeletons == null) return;

        switch (weaponState)
        {
            case RangedSiegeWeapon.WeaponState.Reloading:
            {
                var animations = rangedWeapon.SetUpAnimations;
                for (int i = 0; i < skeletons.Length; i++)
                {
                    if (skeletons[i] == null) continue;
                    if (skeletons[i].GameEntity.IsSkeletonAnimationPaused())
                        skeletons[i].ResumeSkeletonAnimationSynched();
                    else if (animations != null && i < animations.Length && !string.IsNullOrEmpty(animations[i]))
                        skeletons[i].SetAnimationAtChannelSynched(animations[i], 0);
                }
                break;
            }
            case RangedSiegeWeapon.WeaponState.ReloadingPaused:
                foreach (var skeleton in skeletons)
                    skeleton?.PauseSkeletonAnimationSynched();
                break;
        }
    }

    // Vanilla plays FireAnimations on the machine's skeleton owners when it enters WaitingBeforeProjectileLeaving
    // (RangedSiegeWeapon.OnRangedSiegeWeaponStateChange); a peer never reaches that state, so drive it directly.
    internal static void PlayFireAnimation(RangedSiegeWeapon weapon)
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

    // Any live human puppet no client input drives, which a replayed projectile can be fired by without dealing damage here.
    internal static Agent FindStandInShooter()
    {
        foreach (var agent in Mission.Current.Agents)
        {
            if (agent.IsHuman && agent.IsActive() && agent.Controller == AgentControllerType.None) return agent;
        }

        return null;
    }
}
