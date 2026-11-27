using Autofac;
using Coop.Core.Server;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Services.Telemetry;
using Coop.Tests.Mocks;
using GameInterface.Registry;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using Moq;
using Xunit.Abstractions;

namespace Coop.Tests;

internal class ServerTestComponent : TestComponentBase
{
    public ServerTestComponent(ITestOutputHelper output) : base(output)
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule<ServerModule>();
        builder.RegisterModule<RegistryModule>();
        builder.RegisterType<MockServerTelemetryUploader>()
            .As<IServerTelemetryUploader>()
            .As<IBattlesFoughtUploader>()
            .SingleInstance();

        RegisterMock<ICoopServer>(builder);
        RegisterMock<IJoinValidationDenialLog>(builder);

        Container = BuildContainer(builder);
    }

    /// <summary>Makes the player manager mock bind <paramref name="peer"/> to a registration and returns it.</summary>
    /// <remarks>Pass it as the acknowledged player, since the join save only goes to a peer that still plays it.</remarks>
    public Player BindPeerToPlayer(NetPeer peer)
    {
        var player = new Player("BoundController", "Hero_Bound", "MobileParty_Bound", "Clan_Bound", "Character_Bound");
        var registered = player;
        Container.Resolve<Mock<IPlayerManager>>()
            .Setup(manager => manager.TryGetCurrentPlayer(It.Is<NetPeer>(candidate => ReferenceEquals(candidate, peer)), out registered))
            .Returns(true);
        return player;
    }
}
