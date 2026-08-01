using Common;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents;

/// <summary>
/// A besieging player defeated through the SERVER-committed battle path must be removed from its siege
/// camp everywhere. Native frees the party in <c>PlayerEncounter.DoPlayerDefeat</c>, which a
/// server-committed defeat never reaches (the defeated client's encounter is staged straight to End by
/// <c>MapEventResultsHandler</c>), and neither <c>TakePrisonerAction</c> nor the coop park touches
/// <see cref="MobileParty.BesiegerCamp"/> — so the captured player's party kept besieging on the server
/// while its hero sat in captivity. The capture now schedules a deferred server-authoritative camp
/// clear (<c>PlayerStartCaptivityPatches.ScheduleBesiegerCampClear</c>) whose replication frees the
/// party on every client.
/// </summary>
public class BesiegerCaptureTests : MapEventTestBase
{
    private IEnumerable<EnvironmentInstance> AllEnvironmentInstances => Clients.Append(Server);

    public BesiegerCaptureTests(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>Construction-time world dependencies of the siege object graph (see
    /// <c>SiegeLeaveMenuTests</c>): the camp-join internals and both siege-side initializers need a
    /// live town/scene.</summary>
    private static List<MethodBase> SiegeCreationDisabledMethods => new()
    {
        AccessTools.Method(typeof(MobileParty), nameof(MobileParty.OnPartyJoinedSiegeInternal)),
        AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.InitializeSiegeEventSide)),
        AccessTools.Method(typeof(Settlement), nameof(Settlement.InitializeSiegeEventSide)),
    };

    [Fact]
    public void BesiegingPlayerDefeated_ViaServerCommittedBattle_LeavesCampOnServerAndClients()
    {
        // Arrange — a registered player party in a battle it is about to lose. The camp is wired AFTER
        // the map event is initialized: a besieging defender makes native MapEvent.Initialize walk the
        // siege graph's involved parties, which the headless siege fixture (disabled side initializers)
        // cannot satisfy. Live, the full siege graph exists and Initialize handles a besieging defender
        // natively; the scenario under test — the camp's fate at CAPTURE time — is unaffected.
        var (heroId, partyId) = CreatePlayerHeroParty("MyControllerId");
        var captorPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var mapEventId = CreateBattleStateDefeatMapEvent(heroId, partyId, captorPartyId);
        WirePartyIntoBesiegerCamp(partyId);

        // Act — the player loses the battle through the real server-committed route: a client resolves
        // the battle and sets the winning BattleState, which the server applies authoritatively and
        // captures the defeated player there.
        CommitBattleStateDefeat(mapEventId);

        // The capture must NOT clear the camp inline: mid-commit, a last-besieger clear cascades into
        // SiegeEvent.FinalizeSiegeEvent, which would re-finalize the live map event being committed.
        // The clear is queued for the next game-thread drain instead.
        AssertBesiegerCamp(Server, partyId, expectCamp: true);

        DrainDeferredGameThreadActions();

        // Assert — the capture replicated to every instance...
        AssertCaptivity(Server, heroId, captorPartyId);
        foreach (var client in Clients)
        {
            AssertCaptivity(client, heroId, captorPartyId);
        }

        // ...and the deferred authoritative clear freed the party from its camp on the server...
        AssertBesiegerCamp(Server, partyId, expectCamp: false);

        // ...and the cleared camp replicated to every client.
        foreach (var client in Clients)
        {
            AssertBesiegerCamp(client, partyId, expectCamp: false);
        }
    }

    // ------------------------------------------------------------------
    // Setup / drivers
    // ------------------------------------------------------------------

    /// <summary>
    /// Builds a synced siege (settlement, siege event, besieger camp) and wires the party into the camp
    /// on every instance. The membership is wired by field so the world-dependent join internals stay
    /// out of the headless harness (see <c>SiegeLeaveMenuTests.SetupBesiegingPlayerParty</c>); the
    /// defeat under test then runs the real setter.
    /// </summary>
    private void WirePartyIntoBesiegerCamp(string partyId)
    {
        var siegeEventId = TestEnvironment.CreateRegisteredObject<SiegeEvent>(SiegeCreationDisabledMethods);

        // The camp is registered (and replicated) as its own object by its constructor patch; resolving
        // it by id keeps the fixture independent of the SiegeEvent.BesiegerCamp field-sync timing.
        string? campId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<SiegeEvent>(siegeEventId, out var siegeEvent));
            Assert.NotNull(siegeEvent.BesiegerCamp);
            Assert.True(Server.ObjectManager.TryGetId(siegeEvent.BesiegerCamp, out campId));
        });
        Assert.NotNull(campId);

        foreach (var instance in AllEnvironmentInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<BesiegerCamp>(campId!, out var camp));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));

                party._besiegerCamp = camp;
            });
        }
    }

    /// <summary>
    /// Drains the queued deferred actions on the server the way the live game loop does each frame
    /// (the harness marks the test thread as the game thread but nothing pumps the queue). The native
    /// camp-leave cascade needs a live campaign world (party-list removal, siege finalize), so it is
    /// disabled — the <see cref="MobileParty.BesiegerCamp"/> setter itself stays live so the
    /// authoritative clear and its replication are exercised (see <c>SiegeLeaveMenuTests</c>).
    /// </summary>
    private void DrainDeferredGameThreadActions()
    {
        var disabledMethods = new List<MethodBase>
        {
            AccessTools.Method(typeof(MobileParty), nameof(MobileParty.OnPartyLeftSiegeInternal)),
        };

        Server.Call(() =>
        {
            GameThread.Instance.Update(TimeSpan.Zero);
        }, disabledMethods);
    }

    /// <summary>Asserts whether the party is in a besieger camp on the given instance.</summary>
    private static void AssertBesiegerCamp(EnvironmentInstance instance, string partyId, bool expectCamp)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));

            if (expectCamp)
            {
                Assert.NotNull(party.BesiegerCamp);
            }
            else
            {
                Assert.Null(party.BesiegerCamp);
            }
        });
    }
}
