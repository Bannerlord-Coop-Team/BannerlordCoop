using Autofac;
using Common.Messaging;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.Armies.Messages.Lifetime;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Armies;

public class ArmyCreationTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    public ArmyCreationTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerCreateArmy_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        // Act
        string? armyId = null;
        server.Call(() =>
        {
            
            var kingdom = GameObjectCreator.CreateInitializedObject<Kingdom>();
            var mobileParty = GameObjectCreator.CreateInitializedObject<MobileParty>();


            var army = new Army(kingdom, mobileParty, Army.ArmyTypes.Patrolling);

            Assert.True(server.ObjectManager.TryGetId(army, out armyId));
        });

        // Assert
        Assert.NotNull(armyId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Army>(armyId, out var _));
        }
    }

    [Fact]
    public void ClientCreateArmy_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();

        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        // Act
        string? armyId = null;
        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(client1.ObjectManager.TryGetObject<MobileParty>(partyId, out var mobileParty));


            var army = new Army(kingdom, mobileParty, Army.ArmyTypes.Patrolling);

            Assert.False(client1.ObjectManager.TryGetId(army, out armyId));
        });

        // Assert
        Assert.Null(armyId);
    }
}