using E2E.Tests.Environment;
using E2E.Tests.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobileParties;
public class VillagerPartyComponentTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    public VillagerPartyComponentTests(ITestOutputHelper output)
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
            var village = GameObjectCreator.CreateInitializedObject<Village>();
            var newParty = VillagerPartyComponent.CreateVillagerParty("TestId", village, 5);
            partyId = newParty.StringId;
        });


        // Assert
        Assert.NotNull(partyId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var newParty));
            Assert.IsType<VillagerPartyComponent>(newParty.PartyComponent);
        }
    }

    [Fact]
    public void ClientCreateParty_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();

        var village = GameObjectCreator.CreateInitializedObject<Village>();
        server.ObjectManager.AddNewObject(village, out var villageId);

        foreach (var client in TestEnvironment.Clients)
        {
            var clientVillage = GameObjectCreator.CreateInitializedObject<Village>();
            client.ObjectManager.AddExisting(villageId, clientVillage);
        }

        // Act
        const string partyId = "TestId";
        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<Village>(villageId, out var village));
            VillagerPartyComponent.CreateVillagerParty(partyId, village, 5);
        }, new[] { AccessTools.Method(typeof(MobileParty), nameof(MobileParty.ResetCached)) });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var _));
        }
    }
}
