using System;
using System.Collections.Generic;
using Common.Util;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Patches;
using HarmonyLib;
using SandBox.GameComponents;
using SandBox.ViewModelCollection;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

[Collection(ActiveCoopBattleCollection.Name)]
public class CoopAllowPlayerDeploymentPatchTests
{
    [Fact]
    public void SallyOutDefenderFinishesDeploymentWithoutTheOrderOfBattleItNeverOpened()
    {
        RunInSallyOut(BattleSideEnum.Defender, model =>
        {
            // Native deployment never deploys a sally-out defender's formations, so its Order of Battle is never
            // initialized. Deployment finish hands it the gate exactly as MissionGauntletOrderOfBattleUIHandler does.
            var orderOfBattle = ObjectHelper.SkipConstructor<SPOrderOfBattleVM>();
            AccessTools.Field(typeof(OrderOfBattleVM), "_allFormations")
                .SetValue(orderOfBattle, new List<OrderOfBattleFormationItemVM>());

            orderOfBattle.OnDeploymentFinalized(model.CanPlayerSideDeployWithOrderOfBattle());
        });
    }

    [Fact]
    public void SallyOutAttackerKeepsItsOwnOrderOfBattle()
    {
        RunInSallyOut(BattleSideEnum.Attacker, model => Assert.True(model.CanPlayerSideDeployWithOrderOfBattle()));
    }

    private static void RunInSallyOut(BattleSideEnum playerSide, Action<SandboxBattleInitializationModel> act)
    {
        var harmony = new Harmony("tests.coop-allow-player-deployment");
        var previousMission = Mission._current;
        var modelsField = AccessTools.Field(typeof(MissionGameModels), "<Current>k__BackingField");
        var previousModels = modelsField.GetValue(null);
        ActiveCoopBattleScope battleScope = null;
        try
        {
            var mission = ObjectHelper.SkipConstructor<Mission>();
            AccessTools.Field(typeof(Mission), "<MissionTeamAIType>k__BackingField")
                .SetValue(mission, Mission.MissionTeamAITypeEnum.SallyOut);
            mission.Teams = new Mission.TeamCollection(mission);
            var playerTeam = ObjectHelper.SkipConstructor<Team>();
            AccessTools.Field(typeof(Team), "<Side>k__BackingField").SetValue(playerTeam, playerSide);
            mission.Teams._playerTeam = playerTeam;
            Mission._current = mission;
            battleScope = new ActiveCoopBattleScope("sally-out-deployment");

            // The native model answers false for every sally-out; the coop gate decides on top of it.
            var model = new SandboxBattleInitializationModel();
            var models = ObjectHelper.SkipConstructor<MissionGameModels>();
            AccessTools.Field(typeof(MissionGameModels), "<BattleInitializationModel>k__BackingField").SetValue(models, model);
            modelsField.SetValue(null, models);

            Assert.NotEmpty(harmony.CreateClassProcessor(typeof(CoopAllowPlayerDeploymentPatch)).Patch());
            // A healthy local hero is on the field; the real lookup needs a live campaign and native agents.
            harmony.Patch(AccessTools.Method(typeof(CoopAllowPlayerDeploymentPatch), "LocalPlayerHeroOnField"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(CoopAllowPlayerDeploymentPatchTests), nameof(HeroOnField))));

            act(model);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            battleScope?.Dispose();
            modelsField.SetValue(null, previousModels);
            Mission._current = previousMission;
        }
    }

    private static bool HeroOnField(ref bool __result)
    {
        __result = true;
        return false;
    }
}
