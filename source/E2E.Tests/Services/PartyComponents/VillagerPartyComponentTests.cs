using E2E.Tests.Environment;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.PartyComponents;
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
        string? villageId = null;

        server.Call(() =>
        {
            var village = GameObjectCreator.CreateInitializedObject<Village>();

            var newVillage = GameObjectCreator.CreateInitializedObject<Village>();

            var newParty = VillagerPartyComponent.CreateVillagerParty("TestId", village, 5);
            VillagerPartyComponent villagers = (VillagerPartyComponent)newParty.PartyComponent;
            villagers.Village = newVillage;

            Assert.True(server.ObjectManager.TryGetId(newVillage, out villageId));

            partyId = newParty.StringId;
        });


        // Assert
        Assert.NotNull(partyId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var newParty));
            Assert.IsType<VillagerPartyComponent>(newParty.PartyComponent);
            VillagerPartyComponent villagers = (VillagerPartyComponent)newParty.PartyComponent;

            Assert.Equal(villageId, villagers.Village.StringId);
        }
    }

    [Fact]
    public void ClientCreateParty_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();

        var village = GameObjectCreator.CreateInitializedObject<Village>();

        // Act
        PartyComponent? partyComponent = null;
        client1.Call(() =>
        {
            partyComponent = new VillagerPartyComponent(village);
        });

        Assert.NotNull(partyComponent);


        // Assert
        Assert.False(client1.ObjectManager.TryGetId(partyComponent, out var _));
    }
}
