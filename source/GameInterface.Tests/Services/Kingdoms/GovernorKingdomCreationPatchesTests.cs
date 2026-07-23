using Autofac;
using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Kingdoms.Patches;
using GameInterface.Tests;
using GameInterface.Tests.Bootstrap;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Services.Kingdoms;

[Collection(ModInformationRoleCollection.Name)]
public class GovernorKingdomCreationPatchesTests
{
    private static GovernorCampaignBehavior CreateGovernorBehavior(string kingdomName, string cultureId)
    {
        return CreateGovernorBehavior(kingdomName, cultureId, out var _);
    }

    private static GovernorCampaignBehavior CreateGovernorBehavior(string kingdomName, string cultureId, out CultureObject culture)
    {
        var behavior = ObjectHelper.SkipConstructor<GovernorCampaignBehavior>();
        culture = ObjectHelper.SkipConstructor<CultureObject>();
        culture.StringId = cultureId;

        behavior._kingdomCreationChosenName = new TextObject(kingdomName);
        behavior._kingdomCreationChosenCulture = culture;

        return behavior;
    }

    private static void RegisterCultureId(PatchBootstrap bootstrap, CultureObject culture, string cultureId)
    {
        Assert.True(bootstrap.Container.Resolve<IObjectManager>().AddExisting(cultureId, culture));
    }

    [Fact]
    public void GovernorBehaviorPatch_TargetsFinalKingdomCreationConsequence()
    {
        Assert.True(GovernorKingdomCreationPatches.TargetMethodExists());
    }

    [Fact]
    public void ClientFinalization_PublishesKingdomCreationRequestAndSkipsOriginal()
    {
        using var bootstrap = new PatchBootstrap();
        var behavior = CreateGovernorBehavior("Real Kingdom", "native_empire", out var culture);
        RegisterCultureId(bootstrap, culture, "empire");
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
        using var bootstrap = new PatchBootstrap();
        var behavior = CreateGovernorBehavior(kingdomName, "native_empire", out var culture);
        RegisterCultureId(bootstrap, culture, "empire");

        bool created = GovernorKingdomCreationPatches.TryCreateKingdomCreationRequest(behavior, out var request);

        Assert.False(created);
        Assert.Null(request);
    }
}
