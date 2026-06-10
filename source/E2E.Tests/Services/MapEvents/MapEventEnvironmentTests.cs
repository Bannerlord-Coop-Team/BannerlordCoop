using E2E.Tests.Environment.Instance;
using GameInterface.Services.MobileParties.Extensions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// Exercises the richer MapEvent test environment provided by <see cref="MapEventTestBase"/>:
/// parties joining an existing battle, player-party designation, and player captivity. The UI
/// visual/context is mocked via <see cref="MapEventTestBase.MockMapEventVisual"/>.
/// </summary>
public class MapEventEnvironmentTests : MapEventTestBase
{
    public MapEventEnvironmentTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void ServerJoinParty_ToSide_SyncAllClients()
    {
        // Arrange
        var sideId = CreateServerMapEventSide();

        // Act
        var joinerId = JoinPartyToSide(sideId);

        // Assert — the reinforcing party exists and is part of the battle side on every client
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEventParty>(joinerId, out _));
            AssertPartyInSide(client, sideId, joinerId);
        }
    }

    [Fact]
    public void ServerJoinMultipleParties_ToSide_SyncAllClients()
    {
        // Arrange
        var sideId = CreateServerMapEventSide();

        // Act
        var firstJoinerId = JoinPartyToSide(sideId);
        var secondJoinerId = JoinPartyToSide(sideId);

        // Assert
        AssertPartyInSide(Server, sideId, firstJoinerId);
        AssertPartyInSide(Server, sideId, secondJoinerId);

        foreach (var client in Clients)
        {
            AssertPartyInSide(client, sideId, firstJoinerId);
            AssertPartyInSide(client, sideId, secondJoinerId);
        }
    }

    [Fact]
    public void CreatePlayerHeroParty_RegistersPlayerParty_OnAllInstances()
    {
        // Act
        var (_, partyId) = CreatePlayerHeroParty();

        // Assert — the party is recognized as a player party everywhere
        AssertIsPlayerParty(Server, partyId);
        foreach (var client in Clients)
        {
            AssertIsPlayerParty(client, partyId);
        }
    }

    [Fact]
    public void ServerStartCaptivity_OfPlayerHero_SyncAllClients()
    {
        // Arrange
        var (heroId, _) = CreatePlayerHeroParty();
        var captorPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        // Act
        StartCaptivity(heroId, captorPartyId);

        // Assert — captivity state is synced to every client
        AssertCaptivity(Server, heroId, captorPartyId);
        foreach (var client in Clients)
        {
            AssertCaptivity(client, heroId, captorPartyId);
        }
    }

    [Fact]
    public void PlayerPartyDefeatedInBattle_TakesPlayerHeroCaptive_SyncAllClients()
    {
        // Arrange
        var (heroId, partyId) = CreatePlayerHeroParty();
        var captorPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        // Act — the player party loses a battle; CaptureDefeatedPartyMembers runs on the server. Native removes
        // the defeated party's leader, so this only captures the hero if the coop patch reads the leader in a
        // prefix (before native) rather than a postfix.
        DefeatPlayerPartyInBattle(heroId, partyId, captorPartyId);

        // Assert — the defeated player hero is now a prisoner of the captor on every instance
        AssertCaptivity(Server, heroId, captorPartyId);
        foreach (var client in Clients)
        {
            AssertCaptivity(client, heroId, captorPartyId);
        }
    }

    [Fact]
    public void ServerEndCaptivity_OfPlayerHero_SyncAllClients()
    {
        // Arrange
        var (heroId, _) = CreatePlayerHeroParty();
        var captorPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        StartCaptivity(heroId, captorPartyId);

        // Act
        EndCaptivity(heroId);

        // Assert — every client sees the hero released
        AssertCaptivity(Server, heroId, null);
        foreach (var client in Clients)
        {
            AssertCaptivity(client, heroId, null);
        }
    }

    [Fact]
    public void SetMockPlayerEncounter_InstallsAndClears_PlayerEncounterContext()
    {
        // Arrange
        var encounteredPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        AssertHasPlayerEncounter(Server, expected: false);

        // Act — install a mocked encounter on the server
        var encounter = SetMockPlayerEncounter(Server, encounteredPartyId);

        // Assert — the context is live and carries the encountered party
        Assert.NotNull(encounter);
        AssertHasPlayerEncounter(Server, expected: true);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(encounteredPartyId, out var encountered));
            Assert.Same(encountered.Party, PlayerEncounter.EncounteredParty);
        });

        // Act — clear it
        ClearPlayerEncounter(Server);

        // Assert
        AssertHasPlayerEncounter(Server, expected: false);
    }

    [Fact]
    public void SetMockPlayerEncounter_IsPerInstance_NotSynced()
    {
        // Arrange / Act — encounter context is local engine state, installed only on the server
        SetMockPlayerEncounter(Server);

        // Assert — clients do not receive it
        AssertHasPlayerEncounter(Server, expected: true);
        foreach (var client in Clients)
        {
            AssertHasPlayerEncounter(client, expected: false);
        }
    }

    private static void AssertIsPlayerParty(EnvironmentInstance instance, string partyId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(
                MobilePartyExtensions.IsPlayerParty(party),
                $"Party {partyId} was expected to be a player party on {instance.GetType().Name}");
        });
    }
}
