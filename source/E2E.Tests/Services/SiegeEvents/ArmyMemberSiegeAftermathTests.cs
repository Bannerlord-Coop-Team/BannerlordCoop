using Common.Util;
using E2E.Tests.Environment;
using GameInterface.Services.MapEvents.Interfaces;
using GameInterface.Services.SiegeEvents.Interfaces;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents;

/// <summary>Verifies the end of a won siege hands an army member to the aftermath route (#3903).</summary>
public class ArmyMemberSiegeAftermathTests : IDisposable
{
    private readonly E2ETestEnvironment environment;

    public ArmyMemberSiegeAftermathTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
    }

    public void Dispose() => environment.Dispose();

    [Fact]
    public void EndState_ArmyMemberOfWonSiege_IsRoutedToAftermathInsteadOfFinished()
    {
        var client = environment.Clients.First();
        var siegeEvents = new Mock<ISiegeEventInterface>();
        siegeEvents.Setup(siege => siege.TryRouteArmyMemberToAftermath()).Returns(true);

        client.Call(() =>
        {
            var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            encounter.EncounterState = PlayerEncounterState.End;
            Campaign.Current.PlayerEncounter = encounter;
            try
            {
                new PlayerEncounterInterface(siegeEvents.Object).UpdateInternalAfterBattle(encounter);

                // the route finishes the encounter itself; the plain Finish must not run first
                Assert.Same(encounter, Campaign.Current.PlayerEncounter);
                Assert.True(encounter._stateHandled);
            }
            finally
            {
                Campaign.Current.PlayerEncounter = null;
            }
        });

        siegeEvents.Verify(siege => siege.TryRouteArmyMemberToAftermath(), Times.Once);
    }
}