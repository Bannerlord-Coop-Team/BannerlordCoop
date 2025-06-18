using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.PartyComponents;
public class PartyComponentTests : SyncTestBase
{
    public PartyComponentTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void ServerChange_MobileParty_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var party2Id = TestEnvironment.CreateRegisteredObject<MobileParty>();

        // Act
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party1));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(party2Id, out var party2));

            party1.PartyComponent.MobileParty = party2;
        });

        // Assert

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party1));
            Assert.Equal(party1.PartyComponent.MobileParty.StringId, party2Id);
        }
    }

    [Fact]
    public void ClientChange_MobileParty_NoChange()
    {
        // Arrange
        var server = TestEnvironment.Server;

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var party2Id = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party1));
        Assert.True(server.ObjectManager.TryGetId(party1.PartyComponent.MobileParty, out var serverPartyId));

        // Act
        var firstClient = TestEnvironment.Clients.First();

        firstClient.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party1));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(party2Id, out var party2));

            party1.PartyComponent.MobileParty = party2;
        });

        // Assert
        foreach (var client in TestEnvironment.Clients.Where(client => client != firstClient))
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var clientParty));
            Assert.True(client.ObjectManager.TryGetId(clientParty.PartyComponent.MobileParty, out var clientPartyId));
            Assert.Equal(serverPartyId, clientPartyId);
        }
    }
}
