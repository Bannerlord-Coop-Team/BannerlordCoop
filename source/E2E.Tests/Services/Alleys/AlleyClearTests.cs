using E2E.Tests.Environment.Instance;
using GameInterface.Services.Alleys.Interfaces;
using GameInterface.Services.TroopRosters.Data;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
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

        LeaveItEmpty(clearingClient, scenario.AttackerAlleyId);

        Server.Call(() => Assert.Null(Server.GetRegisteredObject<Alley>(scenario.AttackerAlleyId).Owner));
        foreach (var client in Clients)
        {
            client.Call(() => Assert.Null(client.GetRegisteredObject<Alley>(scenario.AttackerAlleyId).Owner));
        }
    }

    [Fact]
    public void LeaveItEmpty_ChangesRelationsOnceEverywhere()
    {
        var scenario = CreateAlleyDomainScenario();
        string merchantId = AddNotables(scenario);

        LeaveItEmpty(Clients.First(), scenario.AttackerAlleyId);

        AssertRelations(scenario, merchantId, gangLeaderRelation: -5, merchantRelation: 1);
    }

    [Fact]
    public void ClearingTheAttackingGang_EndsTheAttackOnThePlayersAlley()
    {
        var scenario = CreateAlleyDomainScenario();
        var ownerClient = Clients.First();
        AcquireAlley(
            ownerClient,
            scenario,
            scenario.OwnerHeroId,
            new TroopRosterElementData(CharacterHandle(scenario.OwnerHeroId), 1, 0, 0));
        ForceAttack(scenario);
        Assert.Equal(scenario.AttackerAlleyId, GetManagementData(scenario.PlayerAlleyId).UnderAttackByAlleyId);
        Assert.NotNull(GetClientAlleyData(ownerClient, scenario.PlayerAlleyId).UnderAttackBy);

        LeaveItEmpty(ownerClient, scenario.AttackerAlleyId);

        Assert.Null(GetManagementData(scenario.PlayerAlleyId).UnderAttackByAlleyId);
        Assert.Null(GetClientAlleyData(ownerClient, scenario.PlayerAlleyId).UnderAttackBy);
        foreach (var client in Clients)
        {
            client.Call(() => Assert.Null(client.Resolve<IAlleyCampaignBehaviorInterface>()
                .ClientAlleyData[scenario.PlayerAlleyId].UnderAttackByAlleyId));
        }
    }

    // The listener MissionAlleyHandler.OnLeaveItEmpty reaches once the gang leader is beaten
    private static void LeaveItEmpty(EnvironmentInstance client, string alleyId)
    {
        client.Call(() => Campaign.Current.GetCampaignBehavior<AlleyCampaignBehavior>().OnAlleyClearedByPlayer(
            client.GetRegisteredObject<Alley>(alleyId)));
    }

    /// <summary>Makes the gang leader and a new merchant notables of the alley settlement on the server.</summary>
    private string AddNotables(AlleyDomainScenario scenario)
    {
        string merchantId = CreateRegisteredObject<Hero>();
        ConfigureHeroIdentity(merchantId);

        Server.Call(() =>
        {
            Hero merchant = Server.GetRegisteredObject<Hero>(merchantId);
            merchant.SetNewOccupation(Occupation.Merchant);
            Settlement settlement = Server.GetRegisteredObject<Settlement>(scenario.SettlementId);
            settlement._notablesCache ??= new MBList<Hero>();
            settlement._notablesCache.Add(Server.GetRegisteredObject<Hero>(scenario.GangLeaderId));
            settlement._notablesCache.Add(merchant);
        });

        return merchantId;
    }

    private void AssertRelations(AlleyDomainScenario scenario, string merchantId, int gangLeaderRelation, int merchantRelation)
    {
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                Hero clearer = instance.GetRegisteredObject<Hero>(scenario.OwnerHeroId);
                Assert.Equal(gangLeaderRelation, clearer.GetRelation(instance.GetRegisteredObject<Hero>(scenario.GangLeaderId)));
                Assert.Equal(merchantRelation, clearer.GetRelation(instance.GetRegisteredObject<Hero>(merchantId)));
            });
        }
    }

    private uint CharacterHandle(string heroId)
    {
        uint handle = 0;
        Server.Call(() => Assert.True(Server.ObjectManager.TryGetHandle(
            Server.GetRegisteredObject<Hero>(heroId).CharacterObject, out handle)));
        return handle;
    }
}
