#if COOP_NAVAL
using Autofac;
using Missions.Battles;
using Missions.Naval;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalAgentFormationAssignerTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    public void CanJoinFormation_RefusesOnlyAShiplessFormationOfANavalBattle(bool isNavalBattle, bool formationHasHull, bool expected)
    {
        Assert.Equal(expected, NavalAgentFormationAssigner.CanJoinFormation(isNavalBattle, formationHasHull));
    }

    [Fact]
    public void NavalMissionModule_ReplacesTheMissionFormationAssigner()
    {
        var builder = new ContainerBuilder();
        builder.RegisterType<AgentFormationAssigner>().As<IAgentFormationAssigner>().InstancePerDependency();
        builder.RegisterModule<NavalMissionModule>();

        using var container = builder.Build();

        Assert.IsType<NavalAgentFormationAssigner>(container.Resolve<IAgentFormationAssigner>());
    }
}
#endif
