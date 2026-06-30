using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Caravans.Data;
using GameInterface.Services.Caravans.Handlers;
using GameInterface.Services.Caravans.Interfaces;
using GameInterface.Services.Caravans.Messages;
using GameInterface.Services.ObjectManager;
using Moq;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.Caravans;

/// <summary>
/// Prevents campaign-global state changes in these tests from racing other test classes.
/// </summary>
[CollectionDefinition(nameof(CaravansCampaignBehaviorHandlerCollection), DisableParallelization = true)]
public class CaravansCampaignBehaviorHandlerCollection
{
}

/// <summary>
/// Tests received caravan campaign behavior updates and their game-thread lifetime checks.
/// </summary>
[Collection(nameof(CaravansCampaignBehaviorHandlerCollection))]
public class CaravansCampaignBehaviorHandlerTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly Mock<IMessageBroker> messageBroker = new();
    private readonly Mock<IObjectManager> objectManager = new();
    private readonly CaravansCampaignBehaviorHandler handler;
    private readonly Action<MessagePayload<NetworkUpdateTradeActionLogsForParty>> tradeLogSubscriber;

    static CaravansCampaignBehaviorHandlerTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
    }

    public CaravansCampaignBehaviorHandlerTests()
    {
        Action<MessagePayload<NetworkUpdateTradeActionLogsForParty>> subscriber = null!;
        messageBroker
            .Setup(broker => broker.Subscribe(It.IsAny<Action<MessagePayload<NetworkUpdateTradeActionLogsForParty>>>()))
            .Callback<Action<MessagePayload<NetworkUpdateTradeActionLogsForParty>>>(callback => subscriber = callback);

        handler = new CaravansCampaignBehaviorHandler(
            messageBroker.Object,
            objectManager.Object,
            new Mock<INetwork>().Object,
            new Mock<ISessionCaravansPlayerDataInterface>().Object);

        tradeLogSubscriber = subscriber ?? throw new InvalidOperationException("Trade-log subscriber was not registered");
    }

    [Fact]
    public void NetworkTradeLogUpdate_WhenCampaignEndsBeforeApply_SkipsUpdateAndContinuesQueue()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        MobileParty resolvedParty = party;
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging("party-1", out resolvedParty))
            .Returns(true);

        Campaign originalCampaign = null!;
        var gameThreadBlocked = new ManualResetEventSlim(false);
        var releaseGameThread = new ManualResetEventSlim(false);

        GameThread.Run(() =>
        {
            originalCampaign = Campaign.Current;
            Campaign.Current = ObjectHelper.SkipConstructor<Campaign>();
        }, blocking: true);

        GameThread.Run(() =>
        {
            gameThreadBlocked.Set();
            releaseGameThread.Wait(Timeout);
            Campaign.Current = null;
        });

        try
        {
            Assert.True(gameThreadBlocked.Wait(Timeout), "game thread did not enter the teardown gate");

            tradeLogSubscriber(new MessagePayload<NetworkUpdateTradeActionLogsForParty>(
                this,
                new NetworkUpdateTradeActionLogsForParty("party-1", new List<TradeActionLogData>())));

            releaseGameThread.Set();
            GameThread.Run(() => { }, blocking: true);

            MobileParty unused;
            objectManager.Verify(
                manager => manager.TryGetObjectWithLogging("party-1", out unused),
                Times.Never);
        }
        finally
        {
            releaseGameThread.Set();
            GameThread.Run(() => Campaign.Current = originalCampaign, blocking: true);
        }
    }

    [Fact]
    public void NetworkTradeLogUpdate_ResolvesPartyAndAppliesOnGameThread()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        MobileParty resolvedParty = party;
        int lookupThreadId = 0;
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging("party-1", out resolvedParty))
            .Callback(() => lookupThreadId = Environment.CurrentManagedThreadId)
            .Returns(true);

        Campaign originalCampaign = null!;
        CaravansCampaignBehavior caravansBehavior = null!;
        int gameThreadId = 0;

        GameThread.Run(() =>
        {
            originalCampaign = Campaign.Current;
            var campaign = ObjectHelper.SkipConstructor<Campaign>();
            Campaign.Current = campaign;
            caravansBehavior = new CaravansCampaignBehavior();
            var behaviorManager = new Mock<ICampaignBehaviorManager>();
            behaviorManager
                .Setup(manager => manager.GetBehavior<CaravansCampaignBehavior>())
                .Returns(caravansBehavior);
            campaign.AddCampaignBehaviorManager(behaviorManager.Object);
            gameThreadId = Environment.CurrentManagedThreadId;
        }, blocking: true);

        try
        {
            tradeLogSubscriber(new MessagePayload<NetworkUpdateTradeActionLogsForParty>(
                this,
                new NetworkUpdateTradeActionLogsForParty("party-1", new List<TradeActionLogData>())));

            GameThread.Run(() => { }, blocking: true);

            Assert.Equal(gameThreadId, lookupThreadId);
            Assert.True(caravansBehavior._tradeActionLogs.ContainsKey(party));
            Assert.Empty(caravansBehavior._tradeActionLogs[party]);
        }
        finally
        {
            GameThread.Run(() => Campaign.Current = originalCampaign, blocking: true);
        }
    }

    public void Dispose()
    {
        handler.Dispose();
    }
}
