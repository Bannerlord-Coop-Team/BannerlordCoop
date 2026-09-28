using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Save.Messages;
using GameInterface.Services.Save.Patches;
using HarmonyLib;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Save;

[Collection(ModInformationRoleCollection.Name)]
public class SaveEndedPatchTests
{
    private const string SaveName = "MP";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Postfix_OnServer_PublishesSaveEndedThenResult(bool isSaveSuccessful)
    {
        var published = RunPostfix(isServer: true, isSaveSuccessful);

        Assert.Equal(2, published.Count);
        var stateChanged = Assert.IsType<GameSaveStateChanged>(published[0]);
        Assert.False(stateChanged.IsSaving);
        var completed = Assert.IsType<GameSaveCompleted>(published[1]);
        Assert.Equal(SaveName, completed.SaveName);
        Assert.Equal(isSaveSuccessful, completed.Success);
    }

    [Fact]
    public void Postfix_OnClient_PublishesNothing()
    {
        Assert.Empty(RunPostfix(isServer: false, isSaveSuccessful: true));
    }

    [Fact]
    public void Postfix_BindsToVanillaOnSaveEndedArguments()
    {
        // Harmony refuses a postfix parameter that the original method does not declare.
        var harmony = new Harmony(nameof(SaveEndedPatchTests));
        try
        {
            harmony.CreateClassProcessor(typeof(SaveEndedPatch)).Patch();

            var original = AccessTools.DeclaredMethod(typeof(SaveHandler), "OnSaveEnded");
            Assert.Contains(
                Harmony.GetPatchInfo(original).Postfixes,
                patch => patch.owner == harmony.Id && patch.PatchMethod.DeclaringType == typeof(SaveEndedPatch));
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    private static List<object> RunPostfix(bool isServer, bool isSaveSuccessful)
    {
        var published = new List<object>();
        Action<MessagePayload<GameSaveStateChanged>> captureState = payload => published.Add(payload.What);
        Action<MessagePayload<GameSaveCompleted>> captureResult = payload => published.Add(payload.What);
        bool originalIsServer = ModInformation.IsServer;
        MessageBroker.Instance.Subscribe(captureState);
        MessageBroker.Instance.Subscribe(captureResult);
        try
        {
            ModInformation.IsServer = isServer;
            SaveEndedPatch.Postfix(ObjectHelper.SkipConstructor<SaveHandler>(), isSaveSuccessful, SaveName);
        }
        finally
        {
            ModInformation.IsServer = originalIsServer;
            MessageBroker.Instance.Unsubscribe(captureState);
            MessageBroker.Instance.Unsubscribe(captureResult);
        }

        return published;
    }
}
