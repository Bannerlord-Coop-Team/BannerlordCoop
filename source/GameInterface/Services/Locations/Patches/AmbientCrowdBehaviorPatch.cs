using HarmonyLib;
using SandBox.Missions.AgentBehaviors;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Settlements.Locations;

namespace GameInterface.Services.Locations.Patches;

/// <summary>
/// Forces the re-enabled ambient crowd to stand still instead of wandering. A wandering agent's AI ticks every
/// frame off the global RNG (restored after the seeded spawn pass), so two clients diverge; standing at the
/// seeded spawn point is consistent. Applies only to characters constructed inside an ambient spawn handler
/// (see <see cref="AmbientCrowd"/>), so other NPCs spawned in the same scene are untouched.
/// </summary>
[HarmonyPatch]
internal static class AmbientCrowdStaticBehaviorPatch
{
    private static readonly LocationCharacter.AddBehaviorsDelegate FixedBehaviors =
        BehaviorSets.AddFixedCharacterBehaviors;

    static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(LocationCharacter));

    static void Prefix(object[] __args)
    {
        if (!AmbientCrowd.IsSpawning) return;

        // Swap the wandering behavior set for the fixed (stand-still) one. Found by type so it works across
        // every constructor overload; constructors without a behaviors argument are left untouched.
        for (int i = 0; i < __args.Length; i++)
        {
            if (__args[i] is LocationCharacter.AddBehaviorsDelegate)
            {
                __args[i] = FixedBehaviors;
                return;
            }
        }
    }
}

/// <summary>
/// Records the (shared culture-template) character of each ambient crowd entry as it is added, so the agents
/// built from it can be made non-interactable.
/// </summary>
[HarmonyPatch(typeof(Location), nameof(Location.AddCharacter))]
internal static class AmbientCrowdMarkPatch
{
    static void Postfix(LocationCharacter locationCharacter)
    {
        if (!AmbientCrowd.IsSpawning) return;

        var character = locationCharacter?.Character;
        if (character != null && !character.IsHero)
        {
            AmbientCrowd.Mark(character);
        }
    }
}
