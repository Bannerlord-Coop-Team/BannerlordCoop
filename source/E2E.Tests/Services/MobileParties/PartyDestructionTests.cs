using E2E.Tests.Environment;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
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
    public void ServerRemoveParty_FiresClientMobilePartyDestroyedEvent()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        MobileParty? clientParty = null;
        bool clientDestroyedEventFired = false;

        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject(partyId, out clientParty));

            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, (party, destroyer) =>
            {
                if (ReferenceEquals(party, clientParty)) clientDestroyedEventFired = true;
            });
        });

        // Act
        // A direct RemoveParty (no DestroyPartyAction) replicates to clients only as the
        // registry-level destroy (NetworkDestroyInstance), so the client-side teardown itself
        // must raise MobilePartyDestroyed: vanilla map UI - most visibly PartyNameplatesVM -
        // only removes a dead party's UI on that event (or a visibility flip, which does not
        // sync either). Without it the dead party's nameplate leaks and keeps rendering its
        // post-teardown name ("NameFailed - BanditPartyPatch" for bandits).
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            party.RemoveParty();
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var _));
        }

        Assert.True(clientDestroyedEventFired);
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