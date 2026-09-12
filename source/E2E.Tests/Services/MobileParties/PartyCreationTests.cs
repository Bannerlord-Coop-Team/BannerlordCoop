using E2E.Tests.Environment;
using E2E.Tests.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobileParties;

public class PartyCreationTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    public PartyCreationTests(ITestOutputHelper output)
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
            var partyComponent = GameObjectCreator.CreateInitializedObject<LordPartyComponent>();
            var clan = GameObjectCreator.CreateInitializedObject<Clan>();
            var party = MobileParty.CreateParty("This should not set", partyComponent);

            Assert.True(server.ObjectManager.TryGetId(party, out partyId));
        });

        // Assert
        Assert.NotNull(partyId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var newParty));
        }
    }

    [Fact]
    public void ServerCreateParty_ClientPartyGetsStableNonZeroId()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();

        // Act
        string? partyId = null;
        server.Call(() =>
        {
            var partyComponent = GameObjectCreator.CreateInitializedObject<LordPartyComponent>();
            var clan = GameObjectCreator.CreateInitializedObject<Clan>();
            var party = MobileParty.CreateParty("StableIdTestParty", partyComponent);

            Assert.True(server.ObjectManager.TryGetId(party, out partyId));
        });

        Assert.NotNull(partyId);

        // MBObjectBase.GetHashCode is Id-based, so the Id must be assigned before anything can key
        // the party into a dictionary (e.g. PartyNameplatesVM) and never change afterwards; a hash
        // that mutates after insert strands the entry and made dead parties' nameplates
        // unremovable ("NameFailed - BanditPartyPatch" ghosts).
        uint firstObservedId = 0;
        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<MobileParty>(partyId, out var clientParty));
            firstObservedId = clientParty.Id.InternalValue;
            Assert.NotEqual(0u, firstObservedId);
        });

        // Pump another roundtrip so any deferred game-thread work (the world-list add) has run,
        // then confirm the Id did not change.
        server.Call(() => { });

        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<MobileParty>(partyId, out var clientParty));
            Assert.Equal(firstObservedId, clientParty.Id.InternalValue);
        });
    }

    [Fact]
    public void ClientCreateParty_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();

        // Act
        string? partyId = null;
        client1.Call(() =>
        {
            var clientParty = new MobileParty();

            partyId = clientParty.StringId;
        }, new[] { AccessTools.Method(typeof(MobileParty), nameof(MobileParty.ResetCached)) });

        // Assert
        Assert.False(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var _));

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var _));
        }
    }
}