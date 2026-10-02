using Autofac;
using Common;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Issues.Patches;
using GameInterface.Surrogates;
using Moq;
using ProtoBuf;
using ProtoBuf.Meta;
using System;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

using Issue = ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue;
using Quest = ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest;

[Collection(ModInformationRoleCollection.Name)]
public class ArtisanOverpricedGoodsTests : IDisposable
{
    private readonly bool previousRole = ModInformation.IsServer;

    public void Dispose()
    {
        ModInformation.IsServer = previousRole;
        ContainerProvider.Clear();
    }

    [Fact]
    public void CreationCapturePreservesCalculatedPricesAndTimesWithoutRecalculating()
    {
        var source = ObjectHelper.SkipConstructor<Issue>();
        source._requestedTradeGood = ObjectHelper.SkipConstructor<ItemObject>();
        source.CounterOfferHero = ObjectHelper.SkipConstructor<Hero>();
        var expected = new ArtisanOverpricedGoodsIssueValues(23, 9127, 0.73f,
            new CampaignTime(1000), new CampaignTime(987654321), "issue_721");
        var subject = new ArtisanOverpricedGoodsIssueInterface();
        subject.ApplyValues(source, expected);

        Assert.True(subject.TryCaptureFields(source, out var captured));
        Assert.Same(source._requestedTradeGood, captured.Item);
        Assert.Same(source.CounterOfferHero, captured.CounterOfferHero);
        Assert.Equal(expected.RequestedAmount, captured.Values.RequestedAmount);
        Assert.Equal(expected.RewardGold, captured.Values.RewardGold);
        Assert.Equal(expected.Difficulty, captured.Values.Difficulty);
        Assert.Equal(expected.CreationTime, captured.Values.CreationTime);
        Assert.Equal(expected.DueTime, captured.Values.DueTime);
        Assert.Equal(expected.IssueId, captured.Values.IssueId);
    }

    [Fact]
    public void CreationWireMessageKeepsGenerationReferencesAndWorldPriceReward()
    {
        var model = RuntimeTypeModel.Create();
        model.Add(typeof(CampaignTime), false).SetSurrogate(typeof(CampaignTimeSurrogate));
        var values = new ArtisanOverpricedGoodsIssueValues(17, 6143, 0.47f,
            new CampaignTime(321), new CampaignTime(456789), "issue_813");
        var original = new NetworkArtisanOverpricedGoodsIssueCreated("artisan", "iron", "merchant", 12, values);
        using var stream = new MemoryStream();
        model.Serialize(stream, original);
        stream.Position = 0;
        var copy = (NetworkArtisanOverpricedGoodsIssueCreated)model.Deserialize(stream, null, original.GetType());

        Assert.Equal(original.OwnerId, copy.OwnerId);
        Assert.Equal(original.ItemId, copy.ItemId);
        Assert.Equal(original.CounterOfferHeroId, copy.CounterOfferHeroId);
        Assert.Equal(original.Generation, copy.Generation);
        Assert.Equal(values.RequestedAmount, copy.Values.RequestedAmount);
        Assert.Equal(values.RewardGold, copy.Values.RewardGold);
        Assert.Equal(values.Difficulty, copy.Values.Difficulty);
        Assert.Equal(values.CreationTime, copy.Values.CreationTime);
        Assert.Equal(values.DueTime, copy.Values.DueTime);
        Assert.Equal(values.IssueId, copy.Values.IssueId);
    }

    [Theory]
    [InlineData(ArtisanOverpricedGoodsAction.DeliverPartial)]
    [InlineData(ArtisanOverpricedGoodsAction.DeliverFull)]
    [InlineData(ArtisanOverpricedGoodsAction.AcceptMerchantOffer)]
    [InlineData(ArtisanOverpricedGoodsAction.StartLordSolution)]
    [InlineData(ArtisanOverpricedGoodsAction.AcceptLordOffer)]
    [InlineData(ArtisanOverpricedGoodsAction.RefuseLordOffer)]
    public void RequestWireMessagePreservesActionAndStaleProgressCheck(ArtisanOverpricedGoodsAction action)
    {
        var copy = Serializer.DeepClone(new RequestArtisanOverpricedGoodsAction("artisan", 5, 11, action));
        Assert.Equal("artisan", copy.OwnerId);
        Assert.Equal(5, copy.Generation);
        Assert.Equal(11, copy.ExpectedDelivered);
        Assert.Equal(action, copy.Action);
    }

    [Fact]
    public void ChainedSuccessCannotCompleteDeliveryWhileWaitingForTheServer()
    {
        var policy = new Mock<ISyncPolicy>();
        policy.Setup(p => p.AllowOriginal()).Returns(false);
        var builder = new ContainerBuilder();
        builder.RegisterInstance(policy.Object).As<ISyncPolicy>();
        ContainerProvider.SetContainer(builder.Build());

        Assert.False(ArtisanOverpricedGoodsChainedSuccessPatch.Prefix(ObjectHelper.SkipConstructor<Quest>()));
    }

    [Fact]
    public void NoActiveSessionKeepsVanillaDeliveryAndCompletion()
    {
        ContainerProvider.SetContainer(new ContainerBuilder().Build());
        var quest = ObjectHelper.SkipConstructor<Quest>();
        Assert.True(ArtisanOverpricedGoodsDeliveryPatches.Partial(quest));
        Assert.True(ArtisanOverpricedGoodsChainedSuccessPatch.Prefix(quest));
    }

    [Fact]
    public void ReceivingUnrelatedStateDoesNotAuthorizeChainedSuccess()
    {
        ModInformation.IsServer = false;
        var policy = new Mock<ISyncPolicy>();
        policy.Setup(p => p.AllowOriginal()).Returns(false);
        var builder = new ContainerBuilder();
        builder.RegisterInstance(policy.Object).As<ISyncPolicy>();
        ContainerProvider.SetContainer(builder.Build());

        using (new AllowedThread())
            Assert.False(ArtisanOverpricedGoodsChainedSuccessPatch.Prefix(ObjectHelper.SkipConstructor<Quest>()));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void FinalizationGuardOnlyAuthorizesReceivedSuccessOnAClient(bool server, bool expected)
    {
        ModInformation.IsServer = server;
        var policy = new Mock<ISyncPolicy>();
        policy.Setup(p => p.AllowOriginal()).Returns(false);
        var builder = new ContainerBuilder();
        builder.RegisterInstance(policy.Object).As<ISyncPolicy>();
        ContainerProvider.SetContainer(builder.Build());

        using (new IssueFinalizeAuthorityGuard())
            Assert.Equal(expected, ArtisanOverpricedGoodsChainedSuccessPatch.Prefix(ObjectHelper.SkipConstructor<Quest>()));
    }
}
