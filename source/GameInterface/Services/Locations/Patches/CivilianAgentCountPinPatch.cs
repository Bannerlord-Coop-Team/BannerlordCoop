using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Locations.Patches;

/// <summary>
/// Pins <see cref="BannerlordConfig.CivilianAgentCount"/> to a shared value while the seeded ambient
/// pass runs. The townsfolk and villager spawn counts scale by this value, which is derived from each
/// player's local "Battle Size" graphics setting and is never networked - so without this pin two
/// players on different settings would spawn a different number of NPCs, desyncing the seeded crowd.
/// Overriding it to a fixed high value makes the count depend only on the (identical) scene, so every
/// client agrees. Battle agent counts are unaffected because the override applies only during the pass.
/// </summary>
[HarmonyPatch(typeof(BannerlordConfig), nameof(BannerlordConfig.CivilianAgentCount), MethodType.Getter)]
internal class CivilianAgentCountPinPatch
{
    static void Postfix(ref float __result)
    {
        if (!AmbientSpawnSeedPatch.AmbientPassActive) return;

        // Any value shared across clients works; the maximum keeps the crowd as full as the scene allows.
        __result = BannerlordConfig.MaxBattleSize * 0.5f;
    }
}
