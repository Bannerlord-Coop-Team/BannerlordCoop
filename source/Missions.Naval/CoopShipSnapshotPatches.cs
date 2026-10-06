using HarmonyLib;
using NavalDLC.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Naval;

namespace Missions.Naval;

// Ship.OnShipDamaged destroys a ship at 0 HP; for a snapshot that would null the owner the mission still reads and raise
// OnShipDestroyed for a ship no party holds. ApplyInternal, not the tiny Apply the JIT may inline into OnShipDamaged.
[HarmonyPatch(typeof(DestroyShipAction), nameof(DestroyShipAction.ApplyInternal))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class SnapshotShipDestroyPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Ship ship) => !CoopShipSnapshots.Contains(ship);
}

// Mission damage to a snapshot is not campaign ship damage, so it grants its owner party no Boatswain XP.
[HarmonyPatch(typeof(NavalSkillLevellingManager), nameof(NavalSkillLevellingManager.OnShipDamaged))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class SnapshotShipDamageXpPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Ship ship) => !CoopShipSnapshots.Contains(ship);
}
