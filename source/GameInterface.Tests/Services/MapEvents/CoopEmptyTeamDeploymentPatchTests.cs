using System;
using System.Linq;
using System.Reflection;
using Common.Util;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

[Collection(nameof(MissionCurrentCollection))]
public class CoopEmptyTeamDeploymentPatchTests
{
    private static readonly MethodInfo IsPlanMade = AccessTools.Method(typeof(DefaultMissionDeploymentPlan),
        nameof(DefaultMissionDeploymentPlan.IsPlanMade), new[] { typeof(Team) });
    private static readonly MethodInfo SpawnGate = AccessTools.Method(typeof(CoopEmptyTeamDeploymentPatch),
        nameof(CoopEmptyTeamDeploymentPatch.IsPlanMadeForSpawnGate));

    // An inlined IsPlanMade skips its postfix, so CheckDeployment must call the override directly (#4223)
    [Fact]
    public void CheckDeployment_ReadsEveryTeamPlanThroughTheSpawnGate()
    {
        var checkDeployment = AccessTools.Method(typeof(DefaultBattleMissionAgentSpawnLogic), "CheckDeployment");
        var original = PatchProcessor.GetOriginalInstructions(checkDeployment);
        int originalCalls = original.Count(instruction => instruction.Calls(IsPlanMade));
        Assert.True(originalCalls > 0);

        var patched = CoopEmptyTeamDeploymentPatch.CheckDeployment_Transpiler(original).ToList();

        Assert.DoesNotContain(patched, instruction => instruction.Calls(IsPlanMade));
        Assert.Equal(originalCalls, patched.Count(instruction => instruction.Calls(SpawnGate)));
        Assert.Equal(original.Count, patched.Count);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void UnplannedForeignTeam_IsTreatedAsPlannedOnlyWithACampaign(bool withCampaign, bool expectedPlanMade)
    {
        var previousMission = Mission._current;
        var previousCampaign = Campaign.Current;
        try
        {
            var mission = ObjectHelper.SkipConstructor<Mission>();
            mission.Teams = new Mission.TeamCollection(mission);
            mission.Teams._playerTeam = ObjectHelper.SkipConstructor<Team>();
            Mission._current = mission;
            Campaign.Current = withCampaign ? ObjectHelper.SkipConstructor<Campaign>() : null;
            BattleSpawnGate.BeginBattle("empty-team-deployment");
            var postfix = AccessTools.Method(typeof(CoopEmptyTeamDeploymentPatch), "IsPlanMade_Postfix");
            var args = new object[] { ObjectHelper.SkipConstructor<Team>(), false };

            postfix.Invoke(null, args);

            Assert.Equal(expectedPlanMade, (bool)args[1]);
        }
        finally
        {
            BattleSpawnGate.EndBattle();
            Campaign.Current = previousCampaign;
            Mission._current = previousMission;
        }
    }
}
