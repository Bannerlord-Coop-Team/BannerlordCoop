using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.Armies;
using GameInterface.Services.Armies.Messages;
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
    public void ServerDisbandArmyWithoutMainParty_PreservesLocalCameraTargets()
    {
        var server = TestEnvironment.Server;
        string? armyId = null;
        string? leaderPartyId = null;
        string? observerPartyId = null;

        server.Call(() =>
        {
            var kingdom = GameObjectCreator.CreateInitializedObject<Kingdom>();
            var leaderParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var army = new Army(kingdom, leaderParty, Army.ArmyTypes.Patrolling);

            Assert.True(server.ObjectManager.TryGetId(army, out armyId));
            Assert.True(server.ObjectManager.TryGetId(leaderParty, out leaderPartyId));
            var observerParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            Assert.True(server.ObjectManager.TryGetId(observerParty, out observerPartyId));
            Campaign.Current.MainParty = null;
            Assert.Null(MobileParty.MainParty);
        });

        var armyOwner = TestEnvironment.Clients.First();
        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                var localPartyId = client == armyOwner ? leaderPartyId : observerPartyId;
                Campaign.Current.MainParty = client.GetRegisteredObject<MobileParty>(localPartyId);
                Assert.False(Hero.MainHero.IsPrisoner);
                Campaign.Current.CameraFollowParty = MobileParty.MainParty.Party;
            });
        }

        server.NetworkSentMessages.Clear();
        server.Call(() =>
        {
            var army = server.GetRegisteredObject<Army>(armyId);
            var leaderParty = server.GetRegisteredObject<MobileParty>(leaderPartyId);
            server.Resolve<IArmyDisbander>().Disband(army, Army.ArmyDispersionReason.Unknown);

            Assert.Null(leaderParty.Army);
            Assert.False(server.ObjectManager.TryGetObject<Army>(armyId, out _));
        });

        var removal = Assert.Single(server.NetworkSentMessages.GetMessages<NetworkRemovePartyInArmy>());
        Assert.Equal(leaderPartyId, removal.MobilePartyId);
        Assert.Empty(removal.ClientMobilePartyId);
        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.False(client.ObjectManager.TryGetObject<Army>(armyId, out _));
                var leaderParty = client.GetRegisteredObject<MobileParty>(leaderPartyId);
                Assert.Null(leaderParty.Army);
                var localPartyId = client == armyOwner ? leaderPartyId : observerPartyId;
                var localParty = client.GetRegisteredObject<MobileParty>(localPartyId);
                Assert.Same(localParty.Party, Campaign.Current.CameraFollowParty);
            });
        }
    }
}
