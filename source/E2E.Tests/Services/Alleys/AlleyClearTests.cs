using Common.Messaging;
using Common.Network;
using Common.Util;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Alleys.Interfaces;
using GameInterface.Services.Alleys.Messages;
using GameInterface.Services.TroopRosters.Data;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
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
        var scenario = CreateClearScenario();
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
        var scenario = CreateClearScenario();
        string merchantId = AddNotables(scenario);

        LeaveItEmpty(Clients.First(), scenario.AttackerAlleyId);

        AssertRelations(scenario, merchantId, gangLeaderRelation: -5, merchantRelation: 1);
    }

    [Fact]
    public void RepeatedClearRequest_DoesNotChangeRelationsAgain()
    {
        var scenario = CreateClearScenario();
        string merchantId = AddNotables(scenario);
        var clearingClient = Clients.First();
        var request = new RequestClearAlley(scenario.AttackerAlleyId, scenario.OwnerHeroId, scenario.GangLeaderId);

        clearingClient.Call(() => clearingClient.Resolve<INetwork>().SendAll(request));
        clearingClient.Call(() => clearingClient.Resolve<INetwork>().SendAll(request));

        Server.Call(() => Assert.Null(Server.GetRegisteredObject<Alley>(scenario.AttackerAlleyId).Owner));
        AssertRelations(scenario, merchantId, gangLeaderRelation: -5, merchantRelation: 1);
    }

    [Fact]
    public void StaleClearOfAnAlreadyFreeAlley_IsIgnored()
    {
        var scenario = CreateClearScenario();
        string merchantId = AddNotables(scenario);

        // The gang left on the server before the client's request arrived; the client still shows it.
        Server.Call(() =>
        {
            using (new AllowedThread())
            {
                Server.GetRegisteredObject<Alley>(scenario.AttackerAlleyId).SetOwner(null);
            }
        });

        LeaveItEmpty(Clients.First(), scenario.AttackerAlleyId);

        Server.Call(() => Assert.Null(Server.GetRegisteredObject<Alley>(scenario.AttackerAlleyId).Owner));
        AssertRelations(scenario, merchantId, gangLeaderRelation: 0, merchantRelation: 0);
    }

    [Fact]
    public void StaleClearOfAnAlleyANewGangLeaderNowHolds_IsIgnored()
    {
        var scenario = CreateClearScenario();
        string merchantId = AddNotables(scenario);
        string newGangLeaderId = CreateGangLeader();

        // Another gang took the alley on the server before the client's request arrived; the client still shows the old one.
        Server.Call(() =>
        {
            using (new AllowedThread())
            {
                Server.GetRegisteredObject<Alley>(scenario.AttackerAlleyId)
                    .SetOwner(Server.GetRegisteredObject<Hero>(newGangLeaderId));
            }
        });

        LeaveItEmpty(Clients.First(), scenario.AttackerAlleyId);

        Server.Call(() => Assert.Same(
            Server.GetRegisteredObject<Hero>(newGangLeaderId),
            Server.GetRegisteredObject<Alley>(scenario.AttackerAlleyId).Owner));
        AssertRelations(scenario, merchantId, gangLeaderRelation: 0, merchantRelation: 0);
    }

    [Fact]
    public void ClearOfAnAlleyOwnedByAPlayer_IsIgnored()
    {
        var scenario = CreateClearScenario();
        var clients = Clients.ToArray();
        AcquireAlley(
            clients[0],
            scenario,
            scenario.OwnerHeroId,
            new TroopRosterElementData(CharacterHandle(scenario.OwnerHeroId), 1, 0, 0));
        var managementBefore = GetManagementData(scenario.PlayerAlleyId);

        // The second player's client clears an alley the first player now owns.
        clients[1].Call(() => Game.Current.PlayerTroop =
            clients[1].GetRegisteredObject<Hero>(scenario.OverseerHeroId).CharacterObject);
        LeaveItEmpty(clients[1], scenario.PlayerAlleyId);

        Server.Call(() => Assert.Same(
            Server.GetRegisteredObject<Hero>(scenario.OwnerHeroId),
            Server.GetRegisteredObject<Alley>(scenario.PlayerAlleyId).Owner));
        foreach (var client in clients)
        {
            client.Call(() => Assert.Same(
                client.GetRegisteredObject<Hero>(scenario.OwnerHeroId),
                client.GetRegisteredObject<Alley>(scenario.PlayerAlleyId).Owner));
        }

        var managementAfter = GetManagementData(scenario.PlayerAlleyId);
        Assert.Equal(managementBefore.OverseerId, managementAfter.OverseerId);
        Assert.Equal(managementBefore.Garrison, managementAfter.Garrison);
        var ownerData = GetClientAlleyData(clients[0], scenario.PlayerAlleyId);
        clients[0].Call(() => Assert.Same(
            clients[0].GetRegisteredObject<Hero>(scenario.OwnerHeroId),
            ownerData.AssignedClanMember));
    }

    [Fact]
    public void ClearRequestFromAPeerNotControllingTheClearer_IsIgnored()
    {
        var scenario = CreateClearScenario();
        string merchantId = AddNotables(scenario);
        var otherClient = Clients.Last();

        // The second player's peer names the first player's hero as the clearer.
        otherClient.Call(() => otherClient.Resolve<INetwork>().SendAll(
            new RequestClearAlley(scenario.AttackerAlleyId, scenario.OwnerHeroId, scenario.GangLeaderId)));

        Server.Call(() => Assert.Same(
            Server.GetRegisteredObject<Hero>(scenario.GangLeaderId),
            Server.GetRegisteredObject<Alley>(scenario.AttackerAlleyId).Owner));
        AssertRelations(scenario, merchantId, gangLeaderRelation: 0, merchantRelation: 0);
    }

    [Fact]
    public void ClearingTheAttackingGang_EndsTheAttackOnThePlayersAlley()
    {
        var scenario = CreateClearScenario();
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

    [Fact]
    public void ClearingADifferentGang_KeepsTheAttackOnThePlayersAlley()
    {
        var scenario = CreateClearScenario();
        var ownerClient = Clients.First();
        AcquireAlley(
            ownerClient,
            scenario,
            scenario.OwnerHeroId,
            new TroopRosterElementData(CharacterHandle(scenario.OwnerHeroId), 1, 0, 0));
        ForceAttack(scenario);

        // Made after the attack so the attack can only have come from the scenario's gang alley.
        string otherGangAlleyId = CreateAlley(scenario.SettlementId, "other_gang_alley");
        string otherGangLeaderId = CreateGangLeader();
        Server.Call(() => Server.GetRegisteredObject<Alley>(otherGangAlleyId)
            .SetOwner(Server.GetRegisteredObject<Hero>(otherGangLeaderId)));

        LeaveItEmpty(ownerClient, otherGangAlleyId);

        Server.Call(() => Assert.Null(Server.GetRegisteredObject<Alley>(otherGangAlleyId).Owner));
        Assert.Equal(scenario.AttackerAlleyId, GetManagementData(scenario.PlayerAlleyId).UnderAttackByAlleyId);
        var ownerData = GetClientAlleyData(ownerClient, scenario.PlayerAlleyId);
        ownerClient.Call(() => Assert.Same(
            ownerClient.GetRegisteredObject<Alley>(scenario.AttackerAlleyId),
            ownerData.UnderAttackBy));
    }

    [Fact]
    public void ClearedGangAlley_CanThenBeTakenOver()
    {
        var scenario = CreateClearScenario();
        var clearingClient = Clients.First();

        LeaveItEmpty(clearingClient, scenario.AttackerAlleyId);

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

    // Each client's peer controls its own player hero, so the server accepts its requests.
    private AlleyDomainScenario CreateClearScenario()
    {
        var scenario = CreateAlleyDomainScenario();
        var clients = Clients.ToArray();
        ConnectRegisteredPlayer(clients[0], "AlleyOwner");
        ConnectRegisteredPlayer(clients[1], "AlleyOverseer");
        return scenario;
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

    private string CreateGangLeader()
    {
        string gangLeaderId = CreateRegisteredObject<Hero>();
        ConfigureHeroIdentity(gangLeaderId);
        Server.Call(() => Server.GetRegisteredObject<Hero>(gangLeaderId).SetNewOccupation(Occupation.GangLeader));
        return gangLeaderId;
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
