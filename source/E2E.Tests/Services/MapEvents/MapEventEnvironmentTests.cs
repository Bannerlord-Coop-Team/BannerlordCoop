using E2E.Tests.Environment.Instance;
using GameInterface.Services.Entity;
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
        var (_, partyId) = CreatePlayerHeroParty("MyControllerId");

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
        var (heroId, _) = CreatePlayerHeroParty("MyControllerId");
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
        var (heroId, partyId) = CreatePlayerHeroParty("MyControllerId");
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
    public void PlayerLosesBattle_CaptureSyncs_ViaBattleStateResult()
    {
        // Arrange
        var (heroId, partyId) = CreatePlayerHeroParty("MyControllerId");
        var captorPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        // Act — the player loses the battle and the result is committed the way the live game does it: a
        // client sets the winning BattleState, which the server applies authoritatively and captures there.
        DefeatPlayerByBattleStateSync(heroId, partyId, captorPartyId);

        // Assert — the capture replicated to every instance (the bug: under AllowedThread the server captured
        // natively without replication, so clients saw no capture and the party was never parked).
        AssertCaptivity(Server, heroId, captorPartyId);
        foreach (var client in Clients)
        {
            AssertCaptivity(client, heroId, captorPartyId);
        }

        // ...and the captured player party was parked (deactivated) on the server.
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.False(party.IsActive, "Captured player party should be deactivated on the server");
        });
    }

    [Fact]
    public void ClientWoundsControlledHero_RequestsHitPoints_SyncsToServerAndClients()
    {
        // The hero is controlled by one specific client (its controller id matches the player's).
        var owningClient = Clients.First();
        owningClient.Resolve<IControllerIdProvider>().SetControllerId("MyControllerId");

        var (heroId, _) = CreatePlayerHeroParty("MyControllerId");

        // The owning client wounds its hero locally, exactly as its mission does
        // (Mission.OnAgentRemoved -> Hero.set_HitPoints). HitPoints is server-authoritative, so this must be
        // forwarded to the server rather than stay a client-only change.
        owningClient.Call(() =>
        {
            Assert.True(owningClient.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            hero.HitPoints = 1;
        });

        // The server received the request and applied it authoritatively...
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.Equal(1, hero.HitPoints);
        });

        // ...and the authoritative value replicated back to every client.
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                Assert.Equal(1, hero.HitPoints);
            });
        }
    }

    [Fact]
    public void ReleasedHero_IsHealed_NotWounded_SyncAllClients()
    {
        // Arrange — the player loses a battle and is taken prisoner.
        var (heroId, partyId) = CreatePlayerHeroParty("MyControllerId");
        var captorPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        DefeatPlayerPartyInBattle(heroId, partyId, captorPartyId);

        // Model the lost battle wounding the hero (HitPoints is a server-authoritative synced property).
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            hero.HitPoints = 1;
        });

        // The wound replicates to every client.
        foreach (var client in Clients)
        {
            AssertHeroHitPoints(client, heroId, expectFull: false);
        }

        // Act — the captor is defeated and the player is freed.
        ReleasePlayerAfterCaptorDefeated(heroId);

        // Assert — the released hero is restored to full health everywhere, so it is no longer shown wounded
        // in the party roster (vanilla never re-adds a released hero as wounded).
        AssertHeroHitPoints(Server, heroId, expectFull: true);
        AssertNoWoundedInParty(Server, partyId);
        foreach (var client in Clients)
        {
            AssertHeroHitPoints(client, heroId, expectFull: true);
            AssertNoWoundedInParty(client, partyId);
        }
    }

    private void AssertNoWoundedInParty(EnvironmentInstance instance, string partyId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(
                party.MemberRoster.TotalWoundedHeroes == 0,
                $"[{instance.GetType().Name}] released party should have no wounded heroes: wounded={party.MemberRoster.TotalWoundedHeroes}");
        });
    }

    private void AssertHeroHitPoints(EnvironmentInstance instance, string heroId, bool expectFull)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            if (expectFull)
            {
                Assert.True(
                    hero.HitPoints == hero.MaxHitPoints,
                    $"[{instance.GetType().Name}] released hero should be at full health: HitPoints={hero.HitPoints} Max={hero.MaxHitPoints}");
            }
            else
            {
                Assert.True(
                    hero.HitPoints < hero.MaxHitPoints,
                    $"[{instance.GetType().Name}] hero should be wounded: HitPoints={hero.HitPoints} Max={hero.MaxHitPoints}");
            }
        });
    }

    [Fact]
    public void CaptorDefeated_ReleasesPlayer_AndRestoresParty()
    {
        // Arrange — the player loses a battle and is taken prisoner by the captor party. Capture parks the
        // player party (emptied + deactivated) and makes the hero the captor's prisoner.
        var (heroId, partyId) = CreatePlayerHeroParty("MyControllerId");
        var captorPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        DefeatPlayerPartyInBattle(heroId, partyId, captorPartyId);

        AssertCaptivity(Server, heroId, captorPartyId);

        // Act — the captor party is later defeated by another party, which frees its prisoners after the
        // battle (the scenario that previously left the player captive with a vanished party).
        ReleasePlayerAfterCaptorDefeated(heroId);

        // Assert — the player is freed on every instance...
        AssertCaptivity(Server, heroId, null);
        foreach (var client in Clients)
        {
            AssertCaptivity(client, heroId, null);
        }

        // ...and its party is restored to the map on the authoritative server...
        AssertPlayerPartyRestored(Server, heroId, partyId);

        // ...with the released hero re-added to the party roster on every client too. The release re-adds the
        // hero on the server (AddElementToMemberRoster); without that replicating, the freed party shows 0
        // troops on the client.
        foreach (var client in Clients)
        {
            AssertHeroInPartyRoster(client, heroId, partyId);
        }
    }

    [Fact]
    public void ServerEndCaptivity_OfPlayerHero_SyncAllClients()
    {
        // Arrange
        var (heroId, _) = CreatePlayerHeroParty("MyControllerId");
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
