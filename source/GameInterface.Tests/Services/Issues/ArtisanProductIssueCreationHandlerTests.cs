using Common;
using Common.Messaging;
using Common.Network;
using Common.Tests.Utils;
using Common.Util;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Handlers;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using Moq;
using System;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

[Collection(ModInformationRoleCollection.Name)]
public sealed class ArtisanProductIssueCreationHandlerTests : IDisposable
{
    private readonly bool wasServer = ModInformation.IsServer;
    private readonly TestMessageBroker broker = new();
    private readonly Mock<IObjectManager> objects = new();
    private readonly RecordingCreation creation = new();
    private readonly IssueGenerationRegistry generations = new();
    private readonly Hero owner = ObjectHelper.SkipConstructor<Hero>();
    private readonly Hero recipient = ObjectHelper.SkipConstructor<Hero>();
    private readonly Hero merchant = ObjectHelper.SkipConstructor<Hero>();
    private readonly Settlement destination = ObjectHelper.SkipConstructor<Settlement>();
    private readonly ItemObject item = ObjectHelper.SkipConstructor<ItemObject>();
    private readonly ArtisanProductIssueCreationHandler handler;

    static ArtisanProductIssueCreationHandlerTests()
        => RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);

    public ArtisanProductIssueCreationHandlerTests()
    {
        ModInformation.IsServer = false;
        SetupObject("owner", owner);
        SetupObject("destination", destination);
        SetupObject("recipient", recipient);
        SetupObject("merchant", merchant);
        SetupObject("item", item);
        handler = new ArtisanProductIssueCreationHandler(broker, Mock.Of<INetwork>(), objects.Object,
            generations, creation);
    }

    [Fact]
    public void ReceivedCreationPreservesAuthoritativeFieldsAndGeneration()
    {
        broker.Publish(this, Message(7));
        Drain();

        Assert.Equal(1, creation.ApplyCount);
        Assert.Same(owner, creation.Owner);
        Assert.Same(destination, creation.Fields.TargetSettlement);
        Assert.Same(recipient, creation.Fields.TargetHero);
        Assert.Same(merchant, creation.Fields.CounterOfferHero);
        Assert.Same(item, creation.Fields.Item);
        Assert.Equal(CampaignTime.Never, creation.Fields.DueTime);
        Assert.Equal("issue_913", creation.Fields.IssueId);
        Assert.Equal(914, creation.Fields.NextIssueIndex);
        Assert.True(generations.TryGetGeneration(owner, out var generation));
        Assert.Equal(7, generation);
    }

    [Fact]
    public void DuplicateAndOlderGenerationDoNotRecreateIssue()
    {
        broker.Publish(this, Message(7));
        broker.Publish(this, Message(7));
        broker.Publish(this, Message(6));
        Drain();

        Assert.Equal(1, creation.ApplyCount);
    }

    [Fact]
    public void MissingDestinationDoesNotAdvanceGeneration()
    {
        Settlement missing = null;
        objects.Setup(o => o.TryGetObjectWithLogging("destination", out missing)).Returns(false);
        broker.Publish(this, Message(7));
        Drain();

        Assert.False(generations.TryGetGeneration(owner, out _));
        Assert.Equal(0, creation.ApplyCount);
    }

    [Fact]
    public void ServerDoesNotApplyReceivedCreation()
    {
        ModInformation.IsServer = true;
        broker.Publish(this, Message(7));
        Drain();

        Assert.Equal(0, creation.ApplyCount);
    }

    [Fact]
    public void DestinationOverrideRestoresOuterScopeAfterException()
    {
        var other = ObjectHelper.SkipConstructor<Settlement>();
        using (new ArtisanProductDestinationScope(destination))
        {
            Assert.Throws<InvalidOperationException>((Action)(() =>
            {
                using (new ArtisanProductDestinationScope(other))
                    throw new InvalidOperationException();
            }));
            Assert.Same(destination, ArtisanProductDestinationScope.Destination);
        }
        Assert.Null(ArtisanProductDestinationScope.Destination);
    }

    private sealed class RecordingCreation : IArtisanProductIssueCreation
    {
        public int ApplyCount;
        public Hero? Owner;
        public ArtisanProductIssueFields Fields;

        public bool TryCapture(TaleWorlds.CampaignSystem.Issues.ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue issue,
            out ArtisanProductIssueFields fields)
        {
            fields = default;
            return false;
        }

        public void Apply(Hero owner, ArtisanProductIssueFields fields)
        {
            ApplyCount++;
            Owner = owner;
            Fields = fields;
        }
    }
    private static NetworkArtisanProductIssueCreated Message(int generation)
        => new("owner", "destination", "recipient", "item", "merchant", generation, CampaignTime.Never, "issue_913", 914);

    private void SetupObject<T>(string id, T value) where T : class
        => objects.Setup(o => o.TryGetObjectWithLogging(id, out value)).Returns(true);

    private static void Drain() => GameThread.Run(() => { }, blocking: true);

    public void Dispose()
    {
        Drain();
        handler.Dispose();
        ModInformation.IsServer = wasServer;
    }
}
