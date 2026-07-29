using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.ObjectManager;
using Moq;
using System;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class HitRewardHandlerTests
{
    static HitRewardHandlerTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
    }

    [Fact]
    public void NetworkScoreboardUpdate_WithoutMission_SkipsObjectResolution()
    {
        Assert.Null(Mission.Current);

        Action<MessagePayload<NetworkUpdateScoreboardAfterUpgrades>>? subscriber = null;
        var messageBroker = new Mock<IMessageBroker>();
        messageBroker
            .Setup(b => b.Subscribe(It.IsAny<Action<MessagePayload<NetworkUpdateScoreboardAfterUpgrades>>>()!))
            .Callback<Action<MessagePayload<NetworkUpdateScoreboardAfterUpgrades>>>(handler => subscriber = handler);
        var objectManager = new Mock<IObjectManager>();

        using var handler = new HitRewardHandler(
            messageBroker.Object,
            objectManager.Object,
            new Mock<INetwork>().Object);

        Assert.NotNull(subscriber);
        subscriber(new MessagePayload<NetworkUpdateScoreboardAfterUpgrades>(
            this,
            new NetworkUpdateScoreboardAfterUpgrades(
                "map-event",
                "character",
                "party",
                BattleSideEnum.Attacker,
                1)));
        GameThread.Run(() => { }, blocking: true);

        objectManager.VerifyNoOtherCalls();
    }
}
