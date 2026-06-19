using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms.Patches;
using HarmonyLib;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Services.Kingdoms;

public class GovernorKingdomCreationPatchesTests
{
    private static GovernorCampaignBehavior CreateGovernorBehavior(string kingdomName, string cultureId)
    {
        var behavior = ObjectHelper.SkipConstructor<GovernorCampaignBehavior>();
        var culture = ObjectHelper.SkipConstructor<CultureObject>();
        culture.StringId = cultureId;

        AccessTools.Field(typeof(GovernorCampaignBehavior), "_kingdomCreationChosenName")
            .SetValue(behavior, new TextObject(kingdomName));
        AccessTools.Field(typeof(GovernorCampaignBehavior), "_kingdomCreationChosenCulture")
            .SetValue(behavior, culture);

        return behavior;
    }

    [Fact]
    public void GovernorBehaviorPatch_TargetsFinalKingdomCreationConsequence()
    {
        Assert.True(GovernorKingdomCreationPatches.TargetMethodExists());
    }

    [Fact]
    public void ClientFinalization_PublishesKingdomCreationRequestAndSkipsOriginal()
    {
        var behavior = CreateGovernorBehavior("Real Kingdom", "empire");
        var published = new List<KingdomCreationRequested>();
        Action<MessagePayload<KingdomCreationRequested>> capture = payload => published.Add(payload.What);
        bool originalIsServer = ModInformation.IsServer;
        MessageBroker.Instance.Subscribe(capture);

        try
        {
            ModInformation.IsServer = false;

            bool runOriginal = GovernorKingdomCreationPatches.FinalizationPrefix(behavior);

            Assert.False(runOriginal);
            KingdomCreationRequested request = Assert.Single(published);
            Assert.Equal("Real Kingdom", request.KingdomName);
            Assert.Equal("empire", request.CultureId);
        }
        finally
        {
            ModInformation.IsServer = originalIsServer;
            MessageBroker.Instance.Unsubscribe(capture);
        }
    }

    [Fact]
    public void ServerFinalization_RunsOriginalAndDoesNotPublishRequest()
    {
        var behavior = CreateGovernorBehavior("Real Kingdom", "empire");
        var published = new List<KingdomCreationRequested>();
        Action<MessagePayload<KingdomCreationRequested>> capture = payload => published.Add(payload.What);
        bool originalIsServer = ModInformation.IsServer;
        MessageBroker.Instance.Subscribe(capture);

        try
        {
            ModInformation.IsServer = true;

            bool runOriginal = GovernorKingdomCreationPatches.FinalizationPrefix(behavior);

            Assert.True(runOriginal);
            Assert.Empty(published);
        }
        finally
        {
            ModInformation.IsServer = originalIsServer;
            MessageBroker.Instance.Unsubscribe(capture);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyKingdomName_DoesNotCreateClientRequest(string kingdomName)
    {
        var behavior = CreateGovernorBehavior(kingdomName, "empire");

        bool created = GovernorKingdomCreationPatches.TryCreateKingdomCreationRequest(behavior, out var request);

        Assert.False(created);
        Assert.Null(request);
    }
}
