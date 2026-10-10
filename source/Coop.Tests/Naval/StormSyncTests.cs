using Coop.Naval.Storms.Interfaces;
using Coop.Naval.Storms.Patches;
using HarmonyLib;
using NavalDLC.Map;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Naval;

public class StormSyncTests
{
    public static IEnumerable<object[]> PatchClasses => new[]
    {
        new object[] { typeof(StormPatches) },
        new object[] { typeof(StormManagerPatches) },
        new object[] { typeof(StormCampaignBehaviorPatches) },
        new object[] { typeof(NavalStormriderCampaignBehaviourPatches) },
    };

    [Theory]
    [MemberData(nameof(PatchClasses))]
    public void Patch_BindsToTheInstalledNavalDlc(Type patchClass)
    {
        var harmony = new Harmony($"coop.tests.naval.storms.{patchClass.Name}");
        try
        {
            Assert.NotEmpty(harmony.CreateClassProcessor(patchClass).Patch());
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [Fact]
    public void ApplyState_CopiesTheServerSnapshot()
    {
        var stormInterface = new StormInterface();
        var serverStorm = CreateStorm(Storm.StormTypes.Hurricane);
        serverStorm._currentPosition = new Vec2(120.5f, 340.25f);
        serverStorm._intensity = 0.75f;
        serverStorm._speed = 1f;
        serverStorm._developingStateFinishCampaignTime = new CampaignTime(1000L);
        serverStorm._finalizingStateStartCampaignTime = new CampaignTime(5000L);
        serverStorm._desiredMoveDirection = new Vec2(0f, 1f);
        serverStorm._currentMoveDirection = new Vec2(0.6f, 0.8f);
        serverStorm._previousPositionsAndRadius[2] = new Storm.PreviousData(new Vec2(100f, 300f), 38f);
        serverStorm._nextUpdatePreviousDataArrayIndex = 3;
        serverStorm._nextUpdateTime = new CampaignTime(2000L);

        var clientStorm = (Storm)FormatterServices.GetUninitializedObject(typeof(Storm));
        stormInterface.ApplyState(clientStorm, stormInterface.GetState(serverStorm));

        Assert.Equal(Storm.StormTypes.Hurricane, clientStorm.StormType);
        Assert.Equal(serverStorm._currentPosition, clientStorm._currentPosition);
        Assert.Equal(0.75f, clientStorm._intensity);
        Assert.Equal(1f, clientStorm._speed);
        Assert.Equal(serverStorm._developingStateFinishCampaignTime, clientStorm._developingStateFinishCampaignTime);
        Assert.Equal(serverStorm._finalizingStateStartCampaignTime, clientStorm._finalizingStateStartCampaignTime);
        Assert.Equal(serverStorm._desiredMoveDirection, clientStorm._desiredMoveDirection);
        Assert.Equal(serverStorm._currentMoveDirection, clientStorm._currentMoveDirection);
        Assert.Equal(serverStorm._previousPositionsAndRadius, clientStorm._previousPositionsAndRadius);
        Assert.Equal(3, clientStorm._nextUpdatePreviousDataArrayIndex);
        Assert.Equal(serverStorm._nextUpdateTime, clientStorm._nextUpdateTime);
        Assert.True(clientStorm.IsVisuallyDirty);
    }

    [Fact]
    public void ApplyState_EmptyTrail_KeepsAnEmptyArray()
    {
        var stormInterface = new StormInterface();
        var serverStorm = CreateStorm(Storm.StormTypes.Storm);
        serverStorm._previousPositionsAndRadius = null;

        var clientStorm = (Storm)FormatterServices.GetUninitializedObject(typeof(Storm));
        stormInterface.ApplyState(clientStorm, stormInterface.GetState(serverStorm));

        Assert.NotNull(clientStorm._previousPositionsAndRadius);
        Assert.Empty(clientStorm._previousPositionsAndRadius);
    }

    private static Storm CreateStorm(Storm.StormTypes stormType)
    {
        var storm = (Storm)FormatterServices.GetUninitializedObject(typeof(Storm));
        AccessTools.FieldRefAccess<Storm, Storm.StormTypes>(nameof(Storm.StormType))(storm) = stormType;
        storm._previousPositionsAndRadius = new Storm.PreviousData[Storm.PreviousPositionsCount];
        return storm;
    }
}
