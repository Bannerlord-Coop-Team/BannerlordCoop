using Common.Messaging;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Alleys.Messages;
using GameInterface.Services.TroopRosters.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Alleys;

/// <summary>The server only applies an alley request from the player whose hero it is about.</summary>
public class AlleyRequestSenderTests : AlleyTestEnvironment
{
    public AlleyRequestSenderTests(ITestOutputHelper output) : base(output, numClients: 2)
    {
    }

    private EnvironmentInstance OwnerClient => Clients.First();
    private EnvironmentInstance OtherClient => Clients.Last();

    [Fact]
    public void AcquireForAnotherPlayersHero_IsRefused()
    {
        var scenario = CreateAlleyDomainScenario();

        AcquireAlley(OtherClient, scenario, scenario.OwnerHeroId);

        Server.Call(() => Assert.Null(Server.GetRegisteredObject<Alley>(scenario.PlayerAlleyId).Owner));
    }

    [Fact]
    public void ManagingAnotherPlayersAlley_IsRefused()
    {
        var scenario = AcquiredByOwner();
        string troopId = CreateRegisteredObject<CharacterObject>();

        OtherClient.Call(() =>
        {
            var alley = OtherClient.GetRegisteredObject<Alley>(scenario.PlayerAlleyId);
            var roster = TroopRoster.CreateDummyTroopRoster();
            roster.AddToCounts(OtherClient.GetRegisteredObject<CharacterObject>(troopId), 7);
            var broker = OtherClient.Resolve<IMessageBroker>();
            broker.Publish(this, new ChangeAlleyOverseerRequested(alley, OtherClient.GetRegisteredObject<Hero>(scenario.OverseerHeroId)));
            broker.Publish(this, new SetAlleyGarrisonRequested(alley, roster));
            broker.Publish(this, new AbandonAlleyRequested(alley, fromClanScreen: true));
        });

        AssertOwnedByOwner(scenario);
        var management = GetManagementData(scenario.PlayerAlleyId);
        Assert.Equal(scenario.OwnerHeroId, management.OverseerId);
        Assert.DoesNotContain(management.Garrison, element => element.CharacterId == Server.GetHandle<CharacterObject>(troopId));
    }

    [Fact]
    public void ResolveDefenseOfAnotherPlayersAlley_IsRefused()
    {
        var scenario = AcquiredByOwner();
        ForceAttack(scenario);

        ResolveDefense(OtherClient, scenario, won: false);

        AssertOwnedByOwner(scenario);
        Assert.Equal(scenario.AttackerAlleyId, GetManagementData(scenario.PlayerAlleyId).UnderAttackByAlleyId);
    }

    private AlleyDomainScenario AcquiredByOwner()
    {
        var scenario = CreateAlleyDomainScenario();
        AcquireAlley(OwnerClient, scenario, scenario.OwnerHeroId);
        AssertOwnedByOwner(scenario);
        return scenario;
    }

    private void AssertOwnedByOwner(AlleyDomainScenario scenario) =>
        Server.Call(() => Assert.Same(Server.GetRegisteredObject<Hero>(scenario.OwnerHeroId),
            Server.GetRegisteredObject<Alley>(scenario.PlayerAlleyId).Owner));
}
