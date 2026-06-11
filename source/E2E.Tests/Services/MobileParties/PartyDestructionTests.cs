using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobileParties;

public class PartyDestructionTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    public PartyDestructionTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerDestroyParty_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var clientParty));


            Assert.NotNull(clientParty);
        }

        // Act

        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            party.RemoveParty();
        });

        // Assert
        Assert.False(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var _));

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var _));
        }
    }

    [Fact]
    public void ServerDestroyPartyWithNullDestroyer_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        // Act
        // Vanilla passes a null destroyer for despawn-style destructions (e.g. patrol culling).
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            DestroyPartyAction.Apply(null, party);
        });

        // Assert
        Assert.False(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var _));

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var _));
        }
    }

    [Fact]
    public void ServerNestedDestroyParty_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        // Act
        // A destroy can run as a vanilla side effect nested inside another replicated action,
        // where patches are skipped via AllowedThread (e.g. a settlement ownership change culling
        // its patrol). It must still replicate to clients.
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));

            using (new AllowedThread())
            {
                DestroyPartyAction.Apply(null, party);
            }
        });

        // Assert
        Assert.False(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var _));

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var _));
        }
    }

    [Fact]
    public void ClientDestroyParty_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();

        string? partyId = null;
        server.Call(() =>
        {
            var partyComponent = GameObjectCreator.CreateInitializedObject<LordPartyComponent>();
            var clan = GameObjectCreator.CreateInitializedObject<Clan>();
            var party = MobileParty.CreateParty("This should not set", partyComponent);

            Assert.True(server.ObjectManager.TryGetId(party, out partyId));
        });

        Assert.NotNull(partyId);

        // Act
        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<MobileParty>(partyId, out var clientParty));

            clientParty.RemoveParty();
        });

        // Assert
        Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var _));

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var _));
        }
    }
}