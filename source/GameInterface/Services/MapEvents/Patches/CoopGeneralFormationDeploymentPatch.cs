using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(Mission), nameof(Mission.SetFormationPositioningFromDeploymentPlan))]
internal class CoopGeneralFormationDeploymentPatch
{
    [HarmonyPostfix]
    private static void Postfix(Mission __instance, Formation formation)
    {
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive
            || formation.FormationIndex != FormationClass.NumberOfRegularFormations
            || formation.OrderPositionIsValid) return;

        var general = formation.Team.GeneralAgent;
        if (general == null || !general.IsActive()
            || general.Team != formation.Team || general.Mission != __instance) return;

        // An already-supplied returning hero can finish deployment without any new troop plan.
        var position = general.GetWorldPosition();
        if (position.IsValid)
            formation.SetPositioning(position);
    }
}
