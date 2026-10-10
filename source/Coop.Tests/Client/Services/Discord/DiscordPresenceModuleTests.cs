using Autofac;
using Common.Messaging;
using Common.Network.Messages;
using Coop.Core.Client.Messages;
using Coop.Core.Client.Services.Discord;
using DiscordRPC;
using Moq;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Coop.Tests.Client.Services.Discord;

public class DiscordPresenceModuleTests
{
    [Fact]
    public async Task SharedPresence_SurvivesSessionDisposalAndClosesWithApplication()
    {
        var connection = new Mock<IDiscordRpcConnection>();
        connection.Setup(c => c.Initialize()).Returns(true);
        var builder = new ContainerBuilder();
        builder.RegisterModule<DiscordPresenceModule>();
        builder.RegisterInstance(connection.Object).As<IDiscordRpcConnection>().ExternallyOwned();
        using var application = builder.Build();
        var presence = (DiscordPresenceClient)application.Resolve<IDiscordPresenceClient>();
        using var broker = new MessageBroker();

        presence.SetMainMenu();
        await presence.PendingWork;
        VerifyMainMenu(connection, Times.Once());

        for (int i = 0; i < 2; i++)
        {
            using (var session = CreateSession(presence, broker))
            {
                Assert.Same(presence, session.Resolve<IDiscordPresenceClient>());
                broker.Publish(this, new NetworkConnected());
                broker.Publish(this, new ClientCampaignReady());
                await presence.PendingWork;
                connection.Verify(c => c.SetPresence(It.Is<RichPresence>(p =>
                    p.Details == "In a co-op campaign" && p.State == "1 player")), Times.Exactly(i + 1));
            }
            await presence.PendingWork;
            VerifyMainMenu(connection, Times.Exactly(i + 2));
            connection.Verify(c => c.Dispose(), Times.Never);
        }

        connection.Verify(c => c.Initialize(), Times.Once);
        application.Dispose();
        await presence.PendingWork;
        connection.Verify(c => c.Dispose(), Times.Once);
    }

    private static IContainer CreateSession(IDiscordPresenceClient presence, IMessageBroker broker)
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule<DiscordPresenceModule>();
        builder.RegisterInstance(presence).As<IDiscordPresenceClient>().ExternallyOwned();
        builder.RegisterInstance(broker).As<IMessageBroker>().ExternallyOwned();
        builder.RegisterType<DiscordPresenceHandler>().AutoActivate();
        return builder.Build();
    }

    private static void VerifyMainMenu(Mock<IDiscordRpcConnection> connection, Times times)
    {
        connection.Verify(c => c.SetPresence(It.Is<RichPresence>(p =>
            p.State == "Main Menu" && p.Details == null && p.Party == null)), times);
    }
}
