using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.Armies;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Armies;

public class ArmyDestructionTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    public ArmyDestructionTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerDestroyArmy_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        string? armyId = null;
        server.Call(() =>
        {

            var kingdom = GameObjectCreator.CreateInitializedObject<Kingdom>();
            var mobileParty = GameObjectCreator.CreateInitializedObject<MobileParty>();


            var army = new Army(kingdom, mobileParty, Army.ArmyTypes.Patrolling);

            Assert.True(server.ObjectManager.TryGetId(army, out armyId));
        });

        Assert.NotNull(armyId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Army>(armyId, out var _));
        }

        // Act
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Army>(armyId, out var army));

            DisbandArmyAction.ApplyByObjectiveFinished(army);
        }, new[] { AccessTools.Method(typeof(PartyBase), nameof(PartyBase.UpdateVisibilityAndInspected)) });

        // Assert
        Assert.False(server.ObjectManager.TryGetObject<Army>(armyId, out var _));

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<Army>(armyId, out var _));
        }
    }

    [Fact]
    public void ClientDestroyArmy_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();

        string? armyId = null;
        server.Call(() =>
        {

            var kingdom = GameObjectCreator.CreateInitializedObject<Kingdom>();
            var mobileParty = GameObjectCreator.CreateInitializedObject<MobileParty>();

            var army = new Army(kingdom, mobileParty, Army.ArmyTypes.Patrolling);

            Assert.True(server.ObjectManager.TryGetId(army, out armyId));
        });

        Assert.NotNull(armyId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Army>(armyId, out var _));
        }

        // Act
        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<Army>(armyId, out var army));

            DisbandArmyAction.ApplyByObjectiveFinished(army);
        });

        // Assert
        Assert.True(server.ObjectManager.TryGetObject<Army>(armyId, out var _));

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Army>(armyId, out var _));
        }
    }

    [Fact]
    public void ServerDisbandArmyWithoutMainParty_SyncAllClients()
    {
        var server = TestEnvironment.Server;
        string? armyId = null;
        string? leaderPartyId = null;

        server.Call(() =>
        {
            var kingdom = GameObjectCreator.CreateInitializedObject<Kingdom>();
            var leaderParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var army = new Army(kingdom, leaderParty, Army.ArmyTypes.Patrolling);

            Assert.True(server.ObjectManager.TryGetId(army, out armyId));
            Assert.True(server.ObjectManager.TryGetId(leaderParty, out leaderPartyId));
            Campaign.Current.MainParty = null;
            Assert.Null(MobileParty.MainParty);

            server.Resolve<IArmyDisbander>().Disband(army, Army.ArmyDispersionReason.Unknown);

            Assert.Null(leaderParty.Army);
            Assert.False(server.ObjectManager.TryGetObject<Army>(armyId, out _));
        });

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<Army>(armyId, out _));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(leaderPartyId, out var leaderParty));
            Assert.Null(leaderParty.Army);
        }
    }
}
