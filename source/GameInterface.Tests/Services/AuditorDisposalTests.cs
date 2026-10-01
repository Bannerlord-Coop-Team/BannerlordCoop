using Autofac;
using Common.Audit;
using Common.Messaging;
using Common.Network;
using Common.Tests.Utils;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Settlements.Audit;
using Moq;
using System.Linq;
using Xunit;

namespace GameInterface.Tests.Services;

/// <summary>Verifies auditors registered like <c>ServiceModule</c> does unsubscribe when their scope is disposed.</summary>
public class AuditorDisposalTests
{
    [Fact]
    public void ContainerDispose_UnsubscribesAuditors()
    {
        // The namespace scan only sees assemblies that are already loaded.
        _ = typeof(SettlementAuditor).Assembly;
        var auditorTypes = InterfaceCollector.GetInterfaces<IAuditor>("GameInterface").ToArray();
        Assert.True(auditorTypes.Length >= 4);

        var broker = new TestMessageBroker();
        var builder = new ContainerBuilder();
        // Externally owned, otherwise disposing the container clears the broker itself.
        builder.RegisterInstance(broker).As<IMessageBroker>().ExternallyOwned();
        builder.RegisterInstance(new Mock<INetwork>().Object).As<INetwork>();
        builder.RegisterInstance(new Mock<IObjectManager>().Object).As<IObjectManager>();
        builder.RegisterInstance(new Mock<INetworkConfig>().Object).As<INetworkConfig>();
        foreach (var type in auditorTypes)
        {
            builder.RegisterType(type).AsSelf().InstancePerLifetimeScope().AutoActivate();
        }

        var container = builder.Build();

        Assert.True(broker.GetTotalSubscribers() > 0);

        container.Dispose();

        Assert.Equal(0, broker.GetTotalSubscribers());
    }
}
