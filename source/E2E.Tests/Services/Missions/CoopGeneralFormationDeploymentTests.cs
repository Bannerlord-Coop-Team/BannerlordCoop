using System;
using Common.Util;
using Moq;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Patches;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace E2E.Tests.Services.Missions;

public class CoopGeneralFormationDeploymentTests
{
    private static Mission mission;
    private static Team team;
    private static WorldPosition generalPosition;
    private static bool activeGeneral;
    private static int generalPositionReads;
    private static bool currentMission;

    [Theory]
    [InlineData(true, false, true, true, true, true)]
    [InlineData(true, true, true, true, true, false)]
    [InlineData(false, false, true, true, true, false)]
    [InlineData(true, false, false, true, true, false)]
    [InlineData(true, false, true, false, true, false)]
    [InlineData(true, false, true, true, false, false)]
    public void UnplannedGeneralUsesLivePositionWithoutReplacingARealPlan(
        bool coop, bool planned, bool generalFormation, bool active, bool sameMission, bool repaired)
    {
        var harmony = new Harmony("e2e.general-deployment");
        bool wasEnabled = BattleSpawnConfig.Enabled;
        try
        {
            mission = ObjectHelper.SkipConstructor<Mission>();
            team = ObjectHelper.SkipConstructor<Team>();
            team.GeneralAgent = ObjectHelper.SkipConstructor<Agent>();
            var formation = ObjectHelper.SkipConstructor<Formation>();
            AccessTools.Field(typeof(Formation), nameof(Formation.Team)).SetValue(formation, team);
            AccessTools.Field(typeof(Formation), nameof(Formation.FormationIndex)).SetValue(formation,
                generalFormation ? FormationClass.NumberOfRegularFormations : FormationClass.Infantry);
            formation._orderPosition = WorldPosition.Invalid;
            generalPosition = new WorldPosition(new UIntPtr(1), UIntPtr.Zero, new Vec3(10, 20, 3), true);
            var plannedPosition = new WorldPosition(new UIntPtr(1), UIntPtr.Zero, new Vec3(30, 40, 5), true);
            activeGeneral = active;
            currentMission = sameMission;
            generalPositionReads = 0;
            var plan = new Mock<IFormationDeploymentPlan>();
            plan.Setup(value => value.CreateNewDeploymentWorldPosition(WorldPosition.WorldPositionEnforcedCache.None))
                .Returns(planned ? plannedPosition : WorldPosition.Invalid);
            plan.Setup(value => value.GetDirection()).Returns(new Vec2(1, 0));
            var deployment = new Mock<IMissionDeploymentPlan>();
            deployment.Setup(value => value.GetFormationPlan(team, formation.FormationIndex, false)).Returns(plan.Object);
            mission._deploymentPlan = deployment.Object;
            BattleSpawnConfig.Enabled = true;
            if (coop) BattleSpawnGate.BeginBattle("retained-general-deployment");
            else BattleSpawnGate.EndBattle();

            Patch(harmony, typeof(Formation), nameof(Formation.SetPositioning), nameof(SetPositioning));
            Patch(harmony, typeof(Agent), nameof(Agent.GetWorldPosition), nameof(GetWorldPosition));
            Patch(harmony, typeof(Agent), nameof(Agent.IsActive), nameof(IsActive));
            Patch(harmony, typeof(Agent), "get_Team", nameof(GetTeam));
            Patch(harmony, typeof(Agent), "get_Mission", nameof(GetMission));
            Assert.NotEmpty(harmony.CreateClassProcessor(typeof(CoopGeneralFormationDeploymentPatch)).Patch());

            // Run vanilla plan application and the production postfix; only the engine positioning is mocked.
            mission.SetFormationPositioningFromDeploymentPlan(formation);
            Assert.Equal(planned || repaired, formation.OrderPositionIsValid);
            Assert.Equal(repaired ? 1 : 0, generalPositionReads);
            if (planned || repaired)
            {
                var position = formation.CreateNewOrderWorldPosition(WorldPosition.WorldPositionEnforcedCache.GroundVec3);
                Assert.Equal((planned ? plannedPosition : generalPosition).GetGroundVec3(), position.GetGroundVec3());
            }
            Assert.Same(team, formation.Team);
            Assert.Equal(new Vec2(1, 0), formation.Direction);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            BattleSpawnGate.EndBattle();
            BattleSpawnConfig.Enabled = wasEnabled;
        }
    }

    private static void Patch(Harmony harmony, Type type, string target, string prefix)
        => harmony.Patch(AccessTools.Method(type, target),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(CoopGeneralFormationDeploymentTests), prefix)));

    private static bool SetPositioning(Formation __instance, WorldPosition? position, Vec2? direction)
    {
        if (position.HasValue && position.Value.IsValid) __instance._orderPosition = position.Value;
        if (direction.HasValue) __instance.Direction = direction.Value;
        return false;
    }

    private static bool GetWorldPosition(ref WorldPosition __result)
    {
        generalPositionReads++;
        __result = generalPosition;
        return false;
    }

    private static bool IsActive(ref bool __result) { __result = activeGeneral; return false; }
    private static bool GetTeam(ref Team __result) { __result = team; return false; }
    private static bool GetMission(ref Mission __result) { __result = currentMission ? mission : null; return false; }
}
