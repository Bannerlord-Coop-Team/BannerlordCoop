using E2E.Tests.Environment.Instance;
using Common.Messaging;
using Common.Logging;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.MapEvents.TroopSupply;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEventParties.Messages;
using GameInterface.Services.MapEvents.Messages;
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

    [Theory]
    [InlineData(BattleState.None, BattleSideEnum.None)]
    [InlineData(BattleState.AttackerVictory, BattleSideEnum.Attacker)]
    [InlineData(BattleState.DefenderVictory, BattleSideEnum.Defender)]
    public void RepeatedOverrideWinner_DoesNotWriteManagedStateOrSendRequest(BattleState state, BattleSideEnum winner)
    {
        var battle = CreateServerMapEvent();
        var client = Clients.First();
        var diagnostics = new List<string>();
        Action<string> capture = message =>
        {
            if (message.Contains("Client updated managed") && message.Contains("BattleState"))
                diagnostics.Add(message);
        };
        client.NetworkSentMessages.Clear();
        OutputSinkManager.AddLogCallback(capture);
        try
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<MapEvent>(battle.MapEventId, out var mapEvent));
                mapEvent._battleState = state;
                mapEvent.SetOverrideWinner(winner);
                Assert.Equal(state, mapEvent.BattleState);
            });
            Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkChangeBattleState>());
            Assert.Empty(diagnostics);
        }
        finally
        {
            OutputSinkManager.RemoveLogCallback(capture);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChangedOverrideWinner_PreservesRequestAndServerValidation(bool runOnServer)
    {
        var battle = CreateServerMapEvent();
        var client = Clients.First();
        foreach (var instance in Clients.Append(Server))
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MapEvent>(battle.MapEventId, out var mapEvent));
                mapEvent._battleState = BattleState.DefenderVictory;
            });
        client.NetworkSentMessages.Clear();
        (runOnServer ? Server : client).Call(() =>
        {
            Assert.True((runOnServer ? Server : client).ObjectManager.TryGetObject<MapEvent>(battle.MapEventId, out var mapEvent));
            mapEvent.SetOverrideWinner(BattleSideEnum.None);
        });
        var requests = client.NetworkSentMessages.GetMessages<NetworkChangeBattleState>();
        if (runOnServer)
            Assert.Empty(requests);
        else
            Assert.Equal(BattleState.None, Assert.Single(requests).BattleState);
        foreach (var instance in Clients.Append(Server))
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MapEvent>(battle.MapEventId, out var mapEvent));
                var expected = runOnServer || instance == client ? BattleState.None : BattleState.DefenderVictory;
                Assert.Equal(expected, mapEvent.BattleState);
            });
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

        // Each captured party's roster is 0, not -1 (reported live bug).
        foreach (var partyId in new[] { party1, party2 })
        {
            AssertPartyManCount(Server, partyId, 0);
            foreach (var client in Clients)
                AssertPartyManCount(client, partyId, 0);
        }
    }

    [Fact]
    public void PlayerLosesBattle_CaptureSyncs_ViaBattleStateResult_AndReleaseRestoresOne()
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

        // The result path must leave an empty roster (0, not -1) and the release restores exactly the hero.
        AssertPartyManCount(Server, partyId, 0);
        foreach (var client in Clients)
            AssertPartyManCount(client, partyId, 0);

        ReleasePlayerAfterCaptorDefeated(heroId);
        AssertPartyManCount(Server, partyId, 1);
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

    [Theory]
    [InlineData(true, 42.6f, 43)]
    [InlineData(true, 0f, 1)]
    [InlineData(true, 100f, 100)]
    [InlineData(false, 25f, 100)]
    public void AgentRemoval_RequestsOwnedHealthWithoutLocalManagedWrite(bool ownsHero, float agentHealth, int expectedHealth)
    {
        var client = Clients.First();
        client.Resolve<IControllerIdProvider>().SetControllerId(ownsHero ? "owner" : "observer");
        var (heroId, _) = CreatePlayerHeroParty("owner");
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            hero.HitPoints = 100;
        });
        client.NetworkSentMessages.Clear();
        var diagnostics = new List<string>();
        Action<string> capture = message =>
        {
            if (message.Contains("Client updated managed") && message.Contains("HitPoints"))
                diagnostics.Add(message);
        };
        OutputSinkManager.AddLogCallback(capture);
        try
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                var origin = new CoopAgentOrigin(hero.CharacterObject, null, -1, null, new UniqueTroopDescriptor(1));
                origin.OnAgentRemoved(agentHealth);
            });
            foreach (var instance in Clients.Append(Server))
                instance.Call(() =>
                {
                    Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                    Assert.Equal(expectedHealth, hero.HitPoints);
                });
            var requests = client.NetworkSentMessages.GetMessages<NetworkHeroHitPointsChangeRequest>();
            if (ownsHero && expectedHealth != 100)
                Assert.Equal(expectedHealth, Assert.Single(requests).HitPoints);
            else
                Assert.Empty(requests);
            Assert.Empty(diagnostics);
        }
        finally
        {
            OutputSinkManager.RemoveLogCallback(capture);
        }
    }

    [Theory]
    [InlineData(true, 43)]
    [InlineData(false, 100)]
    public void MissionEnd_UsesRecordedAuthorityAndIgnoresLateRemoval(bool ownsHealth, int expectedHealth)
    {
        var client = Clients.First();
        client.Resolve<IControllerIdProvider>().SetControllerId("owner");
        var (heroId, _) = CreatePlayerHeroParty("owner");
        Server.Call(() => Server.GetRegisteredObject<Hero>(heroId).HitPoints = 100);
        client.Call(() =>
        {
            var hero = client.GetRegisteredObject<Hero>(heroId);
            var origin = new CoopAgentOrigin(hero.CharacterObject, null, -1, null, new UniqueTroopDescriptor(1));
            origin.OnMissionEnded(42.6f, ownsHealth);
            origin.OnAgentRemoved(90f);
        });
        foreach (var instance in Clients.Append(Server))
            instance.Call(() => Assert.Equal(expectedHealth, instance.GetRegisteredObject<Hero>(heroId).HitPoints));
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
        TestEnvironment.ConnectRegisteredPlayer(Clients.First(), "MyControllerId");
        var captorPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        DefeatPlayerPartyInBattle(heroId, partyId, captorPartyId);

        AssertCaptivity(Server, heroId, captorPartyId);

        // Model the lost battle wounding the hero (HitPoints is a server-authoritative synced property).
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            hero.HitPoints = 1;
        });
        foreach (var client in Clients)
        {
            AssertHeroHitPoints(client, heroId, expectFull: false);
        }

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

        // Release does not instantly heal the freed captive; it stays wounded everywhere.
        AssertHeroHitPoints(Server, heroId, expectFull: false);
        foreach (var client in Clients)
        {
            AssertHeroHitPoints(client, heroId, expectFull: false);
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
        TestEnvironment.ConnectRegisteredPlayer(Clients.First(), "MyControllerId");
        var captorPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var capturedTroops = GetPartyNonHeroManCount(Server, partyId);
        var capturedRidingHeroes = GetPartyLiveHeroCount(Server, partyId);
        DefeatPlayerPartyInBattle(heroId, partyId, captorPartyId);

        // The troop transfer replicates as coalesced roster deltas; drain them before reading client state.
        TestEnvironment.FlushCoalescer();

        // Native removes the defeated leader, so the hero is only captured if the coop patch reads it in a prefix.
        AssertCaptivity(Server, heroId, captorPartyId);
        foreach (var client in Clients)
        {
            AssertCaptivity(client, heroId, captorPartyId);
        }

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
        TestEnvironment.ConnectRegisteredPlayer(Clients.First(), "MyControllerId");
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
    public void CaptureWithTroops_ForfeitsTroopsEverywhere_AndEscapeRestoresOnlyHero()
    {
        // Arrange — the player party holds the hero plus a stack of regular troops on every instance.
        var (heroId, partyId) = CreatePlayerHeroParty("MyControllerId");
        TestEnvironment.ConnectRegisteredPlayer(Clients.First(), "MyControllerId");
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
        TestEnvironment.ConnectRegisteredPlayer(Clients.First(), "MyControllerId");
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
    public void PartyScreenDiscard_DuplicatedPlayerPrisoner_ClearsEveryCopyAndRestoresPartyOnce()
    {
        var (playerHeroId, playerPartyId) = CreatePlayerHeroParty("CaptiveControllerId");
        TestEnvironment.ConnectRegisteredPlayer(Clients.First(), "CaptiveControllerId");
        var (captorHeroId, captorPartyId) = CreatePlayerHeroParty("CaptorControllerId");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(captorHeroId, out var captorHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(captorPartyId, out var captorParty));

            using (new AllowedThread())
            {
                captorParty.MemberRoster.AddToCounts(captorHero.CharacterObject, 1);
                captorHero.PartyBelongedTo = captorParty;
            }
        });

        DefeatPlayerPartyInBattle(playerHeroId, playerPartyId, captorPartyId);
        TestEnvironment.FlushCoalescer();

        SeedPartyPrisoner(Server, captorPartyId, playerHeroId, 1);
        foreach (var client in Clients)
        {
            SeedPartyPrisoner(client, captorPartyId, playerHeroId, 2);
        }
        int unrelatedPrisoners = GetPartyPrisonerCount(Server, captorPartyId) - 2;
        Assert.True(unrelatedPrisoners >= 0);

        AssertPlayerPrisonerCount(Server, captorPartyId, playerHeroId, 2);
        foreach (var client in Clients)
        {
            AssertPlayerPrisonerCount(client, captorPartyId, playerHeroId, 3);
        }

        ReleasePlayerByPartyScreenDiscard(captorHeroId, captorPartyId, playerHeroId);

        AssertCaptivity(Server, playerHeroId, null);
        AssertPlayerPrisonerCount(Server, captorPartyId, playerHeroId, 0);
        AssertPartyPrisonerCount(Server, captorPartyId, unrelatedPrisoners);
        AssertPlayerPartyRestored(Server, playerHeroId, playerPartyId);

        foreach (var client in Clients)
        {
            AssertCaptivity(client, playerHeroId, null);
            AssertPlayerPrisonerCount(client, captorPartyId, playerHeroId, 0);
            AssertPartyPrisonerCount(client, captorPartyId, unrelatedPrisoners);
            AssertHeroInPartyRoster(client, playerHeroId, playerPartyId);
        }
    }

    [Fact]
    public void PartyScreenDiscard_ServerMissingPlayerPrisoner_ClearsStaleClientCopy()
    {
        // Arrange — capture a player normally, then reproduce the live divergence: the server has already
        // lost the prison-roster element while the captor client still displays it and captivity is active.
        var (playerHeroId, playerPartyId) = CreatePlayerHeroParty("CaptiveControllerId");
        TestEnvironment.ConnectRegisteredPlayer(Clients.First(), "CaptiveControllerId");
        var (captorHeroId, captorPartyId) = CreatePlayerHeroParty("CaptorControllerId");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(captorHeroId, out var captorHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(captorPartyId, out var captorParty));

            using (new AllowedThread())
            {
                captorParty.MemberRoster.AddToCounts(captorHero.CharacterObject, 1);
                captorHero.PartyBelongedTo = captorParty;
            }
        });

        DefeatPlayerPartyInBattle(playerHeroId, playerPartyId, captorPartyId);
        TestEnvironment.FlushCoalescer();
        RemovePartyPrisonerLocally(Server, captorPartyId, playerHeroId);

        AssertPlayerPrisonerCount(Server, captorPartyId, playerHeroId, 0);
        foreach (var client in Clients)
        {
            AssertPlayerPrisonerCount(client, captorPartyId, playerHeroId, 1);
        }

        // Act — the stale captor view submits the real normal Party-screen discard.
        ReleasePlayerByPartyScreenDiscard(captorHeroId, captorPartyId, playerHeroId);

        // Assert — the authority sends an absolute tombstone even though it had no element to mutate.
        AssertCaptivity(Server, playerHeroId, null);
        AssertPlayerPrisonerCount(Server, captorPartyId, playerHeroId, 0);
        AssertPlayerPartyRestored(Server, playerHeroId, playerPartyId);

        foreach (var client in Clients)
        {
            AssertCaptivity(client, playerHeroId, null);
            AssertPlayerPrisonerCount(client, captorPartyId, playerHeroId, 0);
            AssertHeroInPartyRoster(client, playerHeroId, playerPartyId);
        }
    }
}
