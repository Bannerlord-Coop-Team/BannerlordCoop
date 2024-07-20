using Autofac.Features.OwnedInstances;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.PartyComponents;
public class CaravanPartyComponentTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    public CaravanPartyComponentTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerCreateParty_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        // Act
        string? partyId = null;

        server.Call(() =>
        {
            var owner = GameObjectCreator.CreateInitializedObject<Hero>();
            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            var culture = GameObjectCreator.CreateInitializedObject<CultureObject>();
            settlement.Culture = culture;
            var newParty = CaravanPartyComponent.CreateCaravanParty(owner, settlement, caravanLeader: owner);
            partyId = newParty.StringId;
        }, new MethodBase[]
        {
            AccessTools.Method(typeof(EnterSettlementAction), nameof(EnterSettlementAction.ApplyForParty)),
        });


        // Assert
        Assert.NotNull(partyId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var newParty));
            Assert.IsType<CaravanPartyComponent>(newParty.PartyComponent);
        }
    }

    [Fact]
    public void ClientCreateParty_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();

        // Act
        PartyComponent? partyComponent = null;
        client1.Call(() =>
        {
            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            var hero = GameObjectCreator.CreateInitializedObject<Hero>();

            partyComponent = new CaravanPartyComponent(settlement, hero, hero);
        });

        Assert.NotNull(partyComponent);

        // Assert
        Assert.False(client1.ObjectManager.TryGetId(partyComponent, out var _));
    }
}
