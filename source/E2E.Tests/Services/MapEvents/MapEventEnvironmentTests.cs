using E2E.Tests.Environment.Instance;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEventParties.Messages;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobilePartyAIs.Patches;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
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
    public void NewMapEvent_ClientReplicaStartsWithNoRetreatState()
    {
        var ctx = CreateServerMapEvent();

        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<MapEvent>(ctx.MapEventId, out var mapEvent));
                Assert.Equal(BattleSideEnum.None, mapEvent.RetreatingSide);
                Assert.Equal(0, mapEvent.PursuitRoundNumber);
                Assert.False(mapEvent.EndedByRetreat);
            });
        }
    }

    [Fact]
    public void ServerRetreatState_SyncsPursuitRound_ToAllClients()
    {
        var ctx = CreateServerMapEvent();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(ctx.MapEventId, out var mapEvent));
            mapEvent.RetreatingSide = BattleSideEnum.Defender;
            mapEvent.PursuitRoundNumber = 2;
        });

        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<MapEvent>(ctx.MapEventId, out var mapEvent));
                Assert.Equal(BattleSideEnum.Defender, mapEvent.RetreatingSide);
                Assert.Equal(2, mapEvent.PursuitRoundNumber);
                Assert.False(mapEvent.EndedByRetreat);
            });
        }
    }

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
    public void TwoAlliedPlayersLoseBattle_BothTakenCaptive_SyncAllClients()
    {
        // Arrange — two allied players share the losing side; an AI captor wins the battle.
        var (hero1, party1) = CreatePlayerHeroParty("player1");
        var (hero2, party2) = CreatePlayerHeroParty("player2");
        var captorPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        // Act — the captor wins; the result is committed the live way (a client sets the victory BattleState,
        // the server applies it authoritatively and captures both losers there).
        DefeatAlliedPlayersByBattleStateSync(captorPartyId, playersAreAttackers: false, (hero1, party1), (hero2, party2));

        // Assert — both defeated players are prisoners of the captor on every instance.
        foreach (var heroId in new[] { hero1, hero2 })
        {
            AssertCaptivity(Server, heroId, captorPartyId);
            foreach (var client in Clients)
                AssertCaptivity(client, heroId, captorPartyId);
        }
    }

    [Fact]
    public void TwoAlliedPlayersAttackAiAndLose_BothTakenCaptive_SyncAllClients()
    {
        // The live coop scenario: two allied players ATTACK an AI party and LOSE, so the AI defender wins
        // (DefenderVictory) — the mirror of the test above (where the players defend). Reported manually as a bug:
        // the losers are left with just their hero and NOT taken captive, with the PlayerEncounter still open.
        var (hero1, party1) = CreatePlayerHeroParty("player1");
        var (hero2, party2) = CreatePlayerHeroParty("player2");
        var captorPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        DefeatAlliedPlayersByBattleStateSync(captorPartyId, playersAreAttackers: true, (hero1, party1), (hero2, party2));

        // Both defeated attacker-players should be prisoners of the captor on every instance.
        foreach (var heroId in new[] { hero1, hero2 })
        {
            AssertCaptivity(Server, heroId, captorPartyId);
            foreach (var client in Clients)
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
    public void ReleasedHero_StaysWounded_SyncAllClients()
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

        // Assert — release does not instantly heal the freed captive: the hero stays wounded everywhere
        // (it recovers gradually through normal healing over time), and that wounded state stays consistent
        // across the server and every client.
        AssertHeroHitPoints(Server, heroId, expectFull: false);
        foreach (var client in Clients)
        {
            AssertHeroHitPoints(client, heroId, expectFull: false);
        }
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
    public void EscapeFromCaptivity_RestoresExactlyOneMan_OnServerAndAllClients()
    {
        // Arrange — a player party (hero + its spawned roster) loses a battle and is captured. BR-061:
        // the heroes AND the regular troops become the captor's prisoners, so snapshot the counts first —
        // harness parties spawn with nondeterministic rosters (the lord party includes its own bootstrap
        // lord hero, captured via the companion capture). The player hero itself is added by
        // DefeatPlayerPartyInBattle AFTER this snapshot, hence the explicit +1 below.
        var (heroId, partyId) = CreatePlayerHeroParty("MyControllerId");
        var captorPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var capturedTroops = GetPartyNonHeroManCount(Server, partyId);
        var capturedRidingHeroes = GetPartyLiveHeroCount(Server, partyId);
        DefeatPlayerPartyInBattle(heroId, partyId, captorPartyId);

        // The troop transfer replicates as coalesced roster deltas; drain them before reading client state.
        TestEnvironment.FlushCoalescer();

        AssertCaptivity(Server, heroId, captorPartyId);

        // The capture parks the party: the member roster is emptied on the server AND on every
        // client. The phantom-troop bug: the hero's capture-time removal never replicated, so
        // clients kept a stale hero element that the release's re-add then doubled.
        AssertPartyManCount(Server, partyId, 0);
        foreach (var client in Clients)
        {
            AssertPartyManCount(client, partyId, 0);
        }

        // ...and the captor holds the player hero (+1), every riding hero AND the party's troops as
        // prisoners (BR-061), counted once everywhere (a replicated prison-roster add applied on top of a
        // locally derived one used to double the count on clients).
        AssertPartyPrisonerCount(Server, captorPartyId, capturedTroops + capturedRidingHeroes + 1);
        foreach (var client in Clients)
        {
            AssertPartyPrisonerCount(client, captorPartyId, capturedTroops + capturedRidingHeroes + 1);
        }

        // Act — the player escapes ("you were able to get away"): the owning client requests the
        // release and the server applies it authoritatively.
        ReleasePlayerByEscapeRequest(Clients.First(), heroId, partyId);

        // Assert — the player is free and the restored party counts exactly one man everywhere, and
        // exactly the hero's element left the captor's prison roster: the captured troops AND the other
        // captured riding heroes remain the captor's prisoners (escape frees the hero, not the army).
        AssertCaptivity(Server, heroId, null);
        AssertPlayerPartyRestored(Server, heroId, partyId);
        AssertPartyPrisonerCount(Server, captorPartyId, capturedTroops + capturedRidingHeroes);
        foreach (var client in Clients)
        {
            AssertCaptivity(client, heroId, null);
            AssertHeroInPartyRoster(client, heroId, partyId);
            AssertPartyPrisonerCount(client, captorPartyId, capturedTroops + capturedRidingHeroes);
        }
    }

    [Fact]
    public void EscapeFromCaptivity_ProtectsPlayerFromFormerCaptorForTwelveHours()
    {
        var (heroId, partyId) = CreatePlayerHeroParty("MyControllerId");
        var captorPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        DefeatPlayerPartyInBattle(heroId, partyId, captorPartyId);

        ReleasePlayerByEscapeRequest(Clients.First(), heroId, partyId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(captorPartyId, out var captorParty));
            Assert.True(DefaultMobilePartyAIModelPatches.DisablePlayerAttackTimes.TryGetValue(captorParty.Ai, out var disabledAttackTimes));
            Assert.True(disabledAttackTimes.ContainsKey(playerParty));

            // The test bootstrap stubs HoursFromNow to Zero, so replace only the deadline before exercising IsPast.
            var disabledUntil = Campaign.Current.MapTimeTracker.Now + CampaignTime.Hours(12);
            DefaultMobilePartyAIModelPatches.PreventAttacksUntil(captorParty, playerParty, disabledUntil);
            Assert.InRange(disabledUntil.RemainingHoursFromNow, 11.9f, 12.1f);

            playerParty.IgnoreByOtherPartiesTill(CampaignTime.Now);
            captorParty.RecentEventsMorale = 100f;

            Assert.True(captorParty.Morale > 0f);
            Assert.False(Campaign.Current.Models.MobilePartyAIModel.ShouldConsiderAttacking(captorParty, playerParty));
        });
    }

    [Fact]
    public void PlayerCapturedViaBattleResult_RosterIsZeroNotNegative_AndReleaseRestoresOne()
    {
        // Reproduces the live bug: a party captured via the battle-RESULT path (BattleState victory -> OnBattleWon
        // -> capture, the path the coop commit-on-conclusion now drives) ends with a NEGATIVE roster (-1), and a
        // release only brings it back to 0 instead of restoring the hero (1). The direct-capture path
        // (EscapeFromCaptivity_...) gets this right; this asserts the result path matches.
        var (heroId, partyId) = CreatePlayerHeroParty("MyControllerId");
        var captorPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        DefeatPlayerByBattleStateSync(heroId, partyId, captorPartyId);

        // The captured party's roster must be empty (0) — not -1.
        AssertPartyManCount(Server, partyId, 0);
        foreach (var client in Clients)
            AssertPartyManCount(client, partyId, 0);

        // On release the hero is restored — exactly 1, not 0.
        ReleasePlayerAfterCaptorDefeated(heroId);
        AssertPartyManCount(Server, partyId, 1);
    }

    [Fact]
    public void TwoAlliedPlayersCaptured_EachRosterIsZeroNotNegative()
    {
        // The live coop case: two allied players share the losing side and are both captured. Reported bug: a
        // captured party's roster goes to -1 (then 0 on release). Asserts each captured party's roster is 0.
        var (hero1, party1) = CreatePlayerHeroParty("player1");
        var (hero2, party2) = CreatePlayerHeroParty("player2");
        var captorPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        DefeatAlliedPlayersByBattleStateSync(captorPartyId, playersAreAttackers: true, (hero1, party1), (hero2, party2));

        foreach (var partyId in new[] { party1, party2 })
        {
            AssertPartyManCount(Server, partyId, 0);
            foreach (var client in Clients)
                AssertPartyManCount(client, partyId, 0);
        }
    }

    [Fact]
    public void CaptureWithTroops_ForfeitsTroopsEverywhere_AndEscapeRestoresOnlyHero()
    {
        // Arrange — the player party holds the hero plus a stack of regular troops on every instance.
        var (heroId, partyId) = CreatePlayerHeroParty("MyControllerId");
        var troopCharacterId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        SeedPartyTroopOnAll(partyId, troopCharacterId, 5);
        var captorPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        // Act — capture. Captivity forfeits the party's troops; the parked roster must be empty on
        // the clients too, not just on the server, or later roster deltas land on misaligned indices.
        DefeatPlayerPartyInBattle(heroId, partyId, captorPartyId);

        AssertPartyManCount(Server, partyId, 0);
        foreach (var client in Clients)
        {
            AssertPartyManCount(client, partyId, 0);
        }

        // ...then the player escapes.
        ReleasePlayerByEscapeRequest(Clients.First(), heroId, partyId);

        // Assert — the freed party holds exactly the hero everywhere; no stale troop stacks or
        // phantom counts survive on the clients.
        AssertPlayerPartyRestored(Server, heroId, partyId);
        foreach (var client in Clients)
        {
            AssertHeroInPartyRoster(client, heroId, partyId);
        }
    }

    [Fact]
    public void CaptureWithDepletedHeroElement_RecalculatesEmptyRosterTotal()
    {
        var (heroId, partyId) = CreatePlayerHeroParty("MyControllerId");
        var captorPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(captorPartyId, out var captorParty));

            using (new AllowedThread())
            {
                playerParty.MemberRoster.AddNewElement(hero.CharacterObject, -1);
            }

            Server.Resolve<IMessageBroker>().Publish(this, new PrisonerTaken(captorParty.Party, hero, playerParty));

            Assert.Equal(0, playerParty.MemberRoster.Count);
            Assert.Equal(0, playerParty.MemberRoster.TotalManCount);
        });
    }

    [Fact]
    public void DuplicateEscapeRequests_ReleaseOnlyOnce()
    {
        // Arrange — a troopless player is captured.
        var (heroId, partyId) = CreatePlayerHeroParty("MyControllerId");
        var captorPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        DefeatPlayerPartyInBattle(heroId, partyId, captorPartyId);

        // Act — the same escape is requested twice (a duplicated message, or a client request racing
        // a server-initiated release). The second pass must not re-add the hero.
        ReleasePlayerByEscapeRequest(Clients.First(), heroId, partyId);
        ReleasePlayerByEscapeRequest(Clients.First(), heroId, partyId);

        // Assert — exactly one man everywhere.
        AssertPlayerPartyRestored(Server, heroId, partyId);
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
