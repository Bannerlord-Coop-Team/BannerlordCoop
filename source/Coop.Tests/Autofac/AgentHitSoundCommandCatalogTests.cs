#if DEBUG
using Autofac;
using Common.Commands;
using Coop.Core.Client;
using GameInterface;
using GameInterface.Services.Missions;
using Xunit;

namespace Coop.Tests.Autofac;

public class AgentHitSoundCommandCatalogTests
{
    [Fact]
    public void ClientCatalogBuildsWithoutServerMissionMembership()
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule<ClientModule>();
        builder.RegisterModule<GameInterfaceModule>();

        using var container = builder.Build();

        Assert.False(container.IsRegistered<IMissionMembershipRegistry>());
        ICoopCommandRegistry catalog = container.Resolve<ICoopCommandRegistry>();
        Assert.True(catalog.Contains("coop.debug.battle.hit_sound_fixture_route"));
        Assert.True(catalog.Contains("coop.debug.battle.hit_sound_fixture_state"));
        Assert.True(catalog.Contains("coop.debug.battle.hit_sound_trace"));
    }
}
#endif
