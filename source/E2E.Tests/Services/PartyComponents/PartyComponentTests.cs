using E2E.Tests.Environment;
using E2E.Tests.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.PartyComponents;
public class PartyComponentTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    public PartyComponentTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
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

        // Act
        var firstClient = TestEnvironment.Clients.First();

        firstClient.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party1));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(party2Id, out var party2));

            party1.PartyComponent.MobileParty = party2;
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party1));
            Assert.NotEqual(party1.PartyComponent.MobileParty.StringId, party2Id);
        }
    }
}
