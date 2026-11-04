using Common.Messaging;
using GameInterface.Services.Alleys.Messages;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Alleys;

/// <summary>Covers the "leave it empty" choice after a client wins a gang's alley fight (#3899).</summary>
public class AlleyClearTests : AlleyTestEnvironment
{
    public AlleyClearTests(ITestOutputHelper output) : base(output, numClients: 2)
    {
    }

    [Fact]
    public void LeaveItEmptyAfterWinningTheFight_FreesTheGangAlleyEverywhere()
    {
        var scenario = CreateAlleyDomainScenario();
        var clearingClient = Clients.First();

        // The listener MissionAlleyHandler.OnLeaveItEmpty reaches once the gang leader is beaten
        clearingClient.Call(() => Campaign.Current.GetCampaignBehavior<AlleyCampaignBehavior>().OnAlleyClearedByPlayer(
            clearingClient.GetRegisteredObject<Alley>(scenario.AttackerAlleyId)));

        Server.Call(() => Assert.Null(Server.GetRegisteredObject<Alley>(scenario.AttackerAlleyId).Owner));
        foreach (var client in Clients)
        {
            client.Call(() => Assert.Null(client.GetRegisteredObject<Alley>(scenario.AttackerAlleyId).Owner));
        }
    }

    [Fact]
    public void ClearedGangAlley_CanThenBeTakenOver()
    {
        var scenario = CreateAlleyDomainScenario();
        var clearingClient = Clients.First();

        clearingClient.Call(() => Campaign.Current.GetCampaignBehavior<AlleyCampaignBehavior>().OnAlleyClearedByPlayer(
            clearingClient.GetRegisteredObject<Alley>(scenario.AttackerAlleyId)));

        clearingClient.Call(() =>
        {
            Hero owner = clearingClient.GetRegisteredObject<Hero>(scenario.OwnerHeroId);
            var roster = TroopRoster.CreateDummyTroopRoster();
            roster.AddToCounts(owner.CharacterObject, 1);
            clearingClient.Resolve<IMessageBroker>().Publish(
                this,
                new AlleyAcquiredRequested(
                    clearingClient.GetRegisteredObject<Alley>(scenario.AttackerAlleyId), owner, owner, roster));
        });

        Server.Call(() => Assert.Same(
            Server.GetRegisteredObject<Hero>(scenario.OwnerHeroId),
            Server.GetRegisteredObject<Alley>(scenario.AttackerAlleyId).Owner));
        foreach (var client in Clients)
        {
            client.Call(() => Assert.Same(
                client.GetRegisteredObject<Hero>(scenario.OwnerHeroId),
                client.GetRegisteredObject<Alley>(scenario.AttackerAlleyId).Owner));
        }
    }
}
