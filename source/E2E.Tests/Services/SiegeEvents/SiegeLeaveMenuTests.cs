using Common.Util;
using Common.Network;
using Coop.Core.Client.Services.SiegeEvents.Messages;
using Coop.Core.Server.Services.SiegeEvents.Messages;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.MapEvents.Messages.Leave;
using HarmonyLib;
using Helpers;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents;

/// <summary>
/// Issue #2263: a besieging client's "Leave" clicks that funnel into a direct native camp write —
/// <c>MenuHelper.EncounterLeaveConsequence</c> (the encounter menu's Leave, e.g. after retreating out of
/// a siege battle) and <c>EncounterGameMenuBehavior.leave_siege_after_attack_on_consequence</c> (the
/// post-battle "Leave the siege" option) — must round-trip the camp removal through the server. The
/// native consequences cleared <see cref="MobileParty.BesiegerCamp"/> client-locally, which sync drops,
/// so the party kept besieging on the server while the client walked away. Each test drives the REAL
/// Harmony-patched method, not a hand-published message.
/// </summary>
public class SiegeLeaveMenuTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;
    private IEnumerable<EnvironmentInstance> AllEnvironmentInstances => Clients.Append(Server);

    public SiegeLeaveMenuTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    /// <summary>Construction-time world dependencies of the siege object graph (see
    /// <c>BesiegerCampSyncTests</c>): the camp-join internals and both siege-side initializers need a
    /// live town/scene.</summary>
    private static List<MethodBase> SiegeCreationDisabledMethods => new()
    {
        AccessTools.Method(typeof(MobileParty), nameof(MobileParty.OnPartyJoinedSiegeInternal)),
        AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.InitializeSiegeEventSide)),
        AccessTools.Method(typeof(Settlement), nameof(Settlement.InitializeSiegeEventSide)),
    };

    /// <summary>
    /// World dependencies hit while a leave click round-trips inline: the server-side camp cascade
    /// (party-list removal, impairment model, siege finalize) needs a live campaign world, and the
    /// approval's local menu exit needs a menu context. The <see cref="MobileParty.BesiegerCamp"/>
    /// setter itself stays live so the authoritative clear and its replication are exercised.
    /// </summary>
    private static List<MethodBase> LeaveRoundTripDisabledMethods => new()
    {
        AccessTools.Method(typeof(MobileParty), nameof(MobileParty.OnPartyLeftSiegeInternal)),
        AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ExitToLast)),
    };

    [Fact]
    public void ClientEncounterLeave_WhileBesieging_RemovesCampOnServerAndClients()
    {
        // Arrange
        var leavingClient = Clients.First();
        var (partyId, _) = SetupBesiegingPlayerParty(leavingClient);

        // Act
        leavingClient.Call(() =>
        {
            InvokePatchedEncounterLeave();
        }, LeaveRoundTripDisabledMethods);

        // Assert: the click was routed, not applied locally — exactly one break request named the leaver's party.
        var request = Assert.Single(leavingClient.NetworkSentMessages.GetMessages<NetworkRequestBreakSiege>());
        Assert.Equal(partyId, request.PartyId);
        Assert.Empty(Clients.Last().NetworkSentMessages.GetMessages<NetworkRequestBreakSiege>());

        // The server approved the request and cleared the camp authoritatively...
        var approval = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBreakSiegeApproved>());
        Assert.True(approval.Approved);
        AssertBesiegerCamp(Server, partyId, expectCamp: false);

        // ...and the cleared camp replicated to every client.
        foreach (var client in Clients)
        {
            AssertBesiegerCamp(client, partyId, expectCamp: false);
        }
    }

    [Fact]
    public void ClientLeaveSiegeAfterAttack_WhileBesieging_RemovesCampOnServerAndClients()
    {
        // Arrange
        var leavingClient = Clients.First();
        var (partyId, _) = SetupBesiegingPlayerParty(leavingClient);

        // Act
        leavingClient.Call(() =>
        {
            InvokePatchedLeaveSiegeAfterAttack();
        }, LeaveRoundTripDisabledMethods);

        // Assert
        var request = Assert.Single(leavingClient.NetworkSentMessages.GetMessages<NetworkRequestBreakSiege>());
        Assert.Equal(partyId, request.PartyId);

        var approval = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBreakSiegeApproved>());
        Assert.True(approval.Approved);
        AssertBesiegerCamp(Server, partyId, expectCamp: false);

        foreach (var client in Clients)
        {
            AssertBesiegerCamp(client, partyId, expectCamp: false);
        }
    }

    [Fact]
    public void NetworkPartyLeftBattle_WithSiegeCleanup_ClearsClientOnlyCamp()
    {
        var leavingClient = Clients.First();
        var (partyId, _) = SetupBesiegingPlayerParty(leavingClient);
        string partyBaseId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            party._besiegerCamp = null;
            Assert.True(Server.ObjectManager.TryGetId(party.Party, out partyBaseId));
        });

        Assert.NotNull(partyBaseId);
        AssertBesiegerCamp(Server, partyId, expectCamp: false);
        foreach (var client in Clients)
        {
            AssertBesiegerCamp(client, partyId, expectCamp: true);
        }

        Server.Call(() =>
        {
            Server.Resolve<INetwork>().SendAll(new NetworkPartyLeftBattle(partyBaseId, true));
        }, LeaveRoundTripDisabledMethods);

        foreach (var client in Clients)
        {
            AssertBesiegerCamp(client, partyId, expectCamp: false);
        }

        leavingClient.Call(() => Assert.Null(PlayerSiege.PlayerSiegeEvent));
    }

    [Fact]
    public void ClientEncounterLeave_WithoutBesiegerCamp_DoesNotRouteBreakSiege()
    {
        // Arrange: the client's main party fights no siege — a plain field-battle leave must stay native.
        var leavingClient = Clients.First();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        SetMainParty(leavingClient, partyId);

        // Act: the native body is suppressed (it needs a live encounter); the coop prefix still runs first.
        var disabledMethods = LeaveRoundTripDisabledMethods
            .Append(AccessTools.Method(typeof(MenuHelper), nameof(MenuHelper.EncounterLeaveConsequence)))
            .ToList();
        leavingClient.Call(() =>
        {
            InvokePatchedEncounterLeave();
        }, disabledMethods);

        // Assert: no break-siege request was forwarded for a party that is not besieging.
        Assert.Empty(leavingClient.NetworkSentMessages.GetMessages<NetworkRequestBreakSiege>());
    }

    [Fact]
    public void ServerEncounterLeave_RunsNativeWithoutRouting()
    {
        // Arrange: the host player is besieging; its leave stays native (patches live) instead of routing
        // a request nothing in the server container handles.
        var (partyId, _) = SetupBesiegingPlayerParty(Server);

        // Act: the native body is suppressed (it needs a live encounter); the coop prefix still runs first.
        var disabledMethods = LeaveRoundTripDisabledMethods
            .Append(AccessTools.Method(typeof(MenuHelper), nameof(MenuHelper.EncounterLeaveConsequence)))
            .ToList();
        Server.Call(() =>
        {
            InvokePatchedEncounterLeave();
        }, disabledMethods);

        // Assert: the server routed nothing — the camp write stays native (here suppressed with the body).
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkRequestBreakSiege>());
        AssertBesiegerCamp(Server, partyId, expectCamp: true);
    }

    // ------------------------------------------------------------------
    // Drivers
    // ------------------------------------------------------------------

    /// <summary>Invokes the real (Harmony-patched) <c>MenuHelper.EncounterLeaveConsequence</c> — the
    /// encounter menu's Leave funnel — via reflection so the coop prefix fires exactly as it does live
    /// (a direct call could be inlined past the detour).</summary>
    private static void InvokePatchedEncounterLeave()
    {
        var method = AccessTools.Method(typeof(MenuHelper), nameof(MenuHelper.EncounterLeaveConsequence));
        Assert.NotNull(method);
        method.Invoke(null, Array.Empty<object>());
    }

    /// <summary>Invokes the real (Harmony-patched)
    /// <c>EncounterGameMenuBehavior.leave_siege_after_attack_on_consequence</c> — the post-battle
    /// "Leave the siege" menu option. Neither the prefix nor the native body reads the behavior state
    /// or the menu args, so a constructor-skipped behavior and null args drive it faithfully.</summary>
    private static void InvokePatchedLeaveSiegeAfterAttack()
    {
        var method = AccessTools.Method(typeof(EncounterGameMenuBehavior), nameof(EncounterGameMenuBehavior.leave_siege_after_attack_on_consequence));
        Assert.NotNull(method);

        var behavior = ObjectHelper.SkipConstructor<EncounterGameMenuBehavior>();
        method.Invoke(behavior, new object[] { null });
    }

    // ------------------------------------------------------------------
    // Setup / assertions
    // ------------------------------------------------------------------

    /// <summary>
    /// Builds a synced siege (settlement, siege event, besieger camp) plus a synced player party, wires
    /// the party into the camp on every instance, and makes it <paramref name="leavingInstance"/>'s
    /// <see cref="MobileParty.MainParty"/>. The membership is wired by field so the world-dependent join
    /// internals stay out of the headless harness; the leave under test then runs the real setter.
    /// </summary>
    private (string partyId, string siegeEventId) SetupBesiegingPlayerParty(EnvironmentInstance leavingInstance)
    {
        var siegeEventId = TestEnvironment.CreateRegisteredObject<SiegeEvent>(SiegeCreationDisabledMethods);
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

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

        SetMainParty(leavingInstance, partyId);

        return (partyId, siegeEventId);
    }

    /// <summary>Makes the party the instance's <see cref="MobileParty.MainParty"/>. Runs in the
    /// instance's static scope, where <c>Campaign.Current</c> resolves to that instance.</summary>
    private static void SetMainParty(EnvironmentInstance instance, string partyId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Campaign.Current.MainParty = party;
            Assert.Same(party, MobileParty.MainParty);
        });
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
