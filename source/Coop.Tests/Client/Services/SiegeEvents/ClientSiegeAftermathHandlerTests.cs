using Common.Tests.Utils;
using Coop.Core.Client.Services.SiegeEvents.Handlers;
using Coop.Core.Server.Services.SiegeEvents.Messages;
using Coop.Tests.Mocks;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEvents.Interfaces;
using Moq;
using System;
using System.Runtime.Serialization;
using System.Threading;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;

namespace Coop.Tests.Client.Services.SiegeEvents;

/// <summary>Verifies the client applies the server's siege aftermath messages to the local choice.</summary>
public sealed class ClientSiegeAftermathHandlerTests
{
    [Fact]
    public void DiscardedAftermath_ReleasesTheLocalChoice()
    {
        var broker = new TestMessageBroker();
        var settlement = CreateUninitialized<Settlement>();
        var objectManager = new Mock<IObjectManager>();
        objectManager.Setup(manager => manager.TryGetObjectWithLogging("settlement-1", out settlement)).Returns(true);
        using var released = new ManualResetEventSlim();
        var siegeEventInterface = new Mock<ISiegeEventInterface>();
        siegeEventInterface.Setup(service => service.ReleaseLocalAftermathChoice(settlement)).Callback(released.Set);
        using var handler = new ClientSiegeAftermathHandler(
            broker, new TestNetwork(), objectManager.Object, siegeEventInterface.Object);

        broker.Publish(this, new NetworkSiegeAftermathDiscarded("settlement-1"));

        // the release runs on the pumped game thread
        Assert.True(released.Wait(TimeSpan.FromSeconds(5)));
    }

#pragma warning disable SYSLIB0050 // Identity-only test doubles; no native constructors are invoked.
    private static T CreateUninitialized<T>() where T : class =>
        (T)FormatterServices.GetUninitializedObject(typeof(T));
#pragma warning restore SYSLIB0050
}
