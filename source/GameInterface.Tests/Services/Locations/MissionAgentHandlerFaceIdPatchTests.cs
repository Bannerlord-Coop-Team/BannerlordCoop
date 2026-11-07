using Common.Util;
using GameInterface.Services.Locations.Patches;
using HarmonyLib;
using SandBox.Missions.MissionLogics;
using System;
using Xunit;

namespace GameInterface.Tests.Services.Locations;

public class MissionAgentHandlerFaceIdPatchTests
{
    [Fact]
    public void GetAllProps_ResetsFaceIdsLeftByPreviousScene()
    {
        var harmony = new Harmony($"{nameof(MissionAgentHandlerFaceIdPatchTests)}.{Guid.NewGuid():N}");
        var getAllProps = AccessTools.Method(typeof(MissionAgentHandler), nameof(MissionAgentHandler.GetAllProps));

        try
        {
            harmony.CreateClassProcessor(typeof(MissionAgentHandlerFaceIdPatch)).Patch();
            // the rest of GetAllProps needs a real scene, so stop after the coop prefix
            harmony.Patch(getAllProps, prefix: new HarmonyMethod(
                AccessTools.Method(typeof(MissionAgentHandlerFaceIdPatchTests), nameof(SkipOriginal)), Priority.Last));

            // values a village center like aserai_village_c leaves behind
            MissionAgentHandler._disabledFaceId = 0;
            MissionAgentHandler._disabledFaceIdForAnimals = 0;

            ObjectHelper.SkipConstructor<MissionAgentHandler>().GetAllProps();

            Assert.Equal(-1, MissionAgentHandler._disabledFaceId);
            Assert.Equal(1, MissionAgentHandler._disabledFaceIdForAnimals);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            MissionAgentHandler._disabledFaceId = -1;
            MissionAgentHandler._disabledFaceIdForAnimals = 1;
        }
    }

    private static bool SkipOriginal() => false;
}
