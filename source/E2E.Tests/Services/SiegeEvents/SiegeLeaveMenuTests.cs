using Common.Util;
using Coop.Core.Client.Services.SiegeEvents.Messages;
using Coop.Core.Server.Services.SiegeEvents.Messages;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.SiegeEvents.Patches;
using HarmonyLib;
using Helpers;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents;

/// <summary>
/// Issue #2263 bug class: native flows that clear <see cref="MobileParty.BesiegerCamp"/> client-locally,
/// which sync drops, so the party kept besieging on the server while the client walked away. Covers the
/// suppressed leave menus (<c>MenuHelper.EncounterLeaveConsequence</c>,
/// <c>leave_siege_after_attack_on_consequence</c>, the join_encounter leave lambda,
/// <c>break_in_leave_consequence</c>) whose approval finishes the local menu, and the embedded camp
/// writes (try-to-get-away accept, <c>PlayerEncounter.DoPlayerDefeat</c>,
/// <c>SafePassageBarterable.Apply</c>) that keep their native continuation and round-trip only the camp
/// removal (FinishLocalMenus=false). Each test drives the REAL Harmony-patched method, not a
/// hand-published message.
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
    public void ClientJoinEncounterLeave_WhileBesieging_RemovesCampOnServerAndClients()
    {
        // Arrange
        var leavingClient = Clients.First();
        var (partyId, _) = SetupBesiegingPlayerParty(leavingClient);

        // Act: the compiler-generated join_encounter leave lambda, resolved the same way the patch
        // resolves its target.
        leavingClient.Call(() =>
        {
            InvokePatchedJoinEncounterLeave();
        }, LeaveRoundTripDisabledMethods);

        // Assert: suppressed-menu shape — the approval still owes the requester its local menu finish.
        var request = Assert.Single(leavingClient.NetworkSentMessages.GetMessages<NetworkRequestBreakSiege>());
        Assert.Equal(partyId, request.PartyId);
        Assert.True(request.FinishLocalMenus);

        var approval = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBreakSiegeApproved>());
        Assert.True(approval.Approved);
        Assert.True(approval.FinishLocalMenus);
        AssertBesiegerCamp(Server, partyId, expectCamp: false);

        foreach (var client in Clients)
        {
            AssertBesiegerCamp(client, partyId, expectCamp: false);
        }

        // The lambda holds the party after PlayerEncounter.Finish; the prefix reissues that hold.
        AssertPartyHeld(leavingClient, partyId);
    }

    [Fact]
    public void ClientBreakInLeave_WhileBesieging_RemovesCampOnServerAndClients()
    {
        // Arrange
        var leavingClient = Clients.First();
        var (partyId, _) = SetupBesiegingPlayerParty(leavingClient);

        // Act: join_siege_event's "Don't get involved." consequence.
        leavingClient.Call(() =>
        {
            InvokePatchedBreakInLeave();
        }, LeaveRoundTripDisabledMethods);

        // Assert
        var request = Assert.Single(leavingClient.NetworkSentMessages.GetMessages<NetworkRequestBreakSiege>());
        Assert.Equal(partyId, request.PartyId);
        Assert.True(request.FinishLocalMenus);

        var approval = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBreakSiegeApproved>());
        Assert.True(approval.Approved);
        AssertBesiegerCamp(Server, partyId, expectCamp: false);

        foreach (var client in Clients)
        {
            AssertBesiegerCamp(client, partyId, expectCamp: false);
        }

        // Native holds the party after the (approval-deferred) Finish; the prefix reissues that hold.
        AssertPartyHeld(leavingClient, partyId);

        // No map-event side was wired, so the battle-leave branch stays quiet (see the map-event-side test).
        Assert.Empty(leavingClient.NetworkSentMessages.GetMessages<NetworkRequestLeaveBattle>());
    }

    [Fact]
    public void ClientBreakInLeave_WhileOnMapEventSide_RoutesBattleLeave()
    {
        // Arrange: a besieger that a nearby sally-out/relief battle also pulled onto a map-event side.
        // Native's break_in_leave clears MapEventSide before the camp; that single-party removal is not
        // auto-synced, so the prefix must route it or the party stays in that battle on the server. The side
        // is wired on the leaving client only (the branch just reads MobileParty.MapEventSide); the returning
        // removal's native RemovePartyInternal is suppressed so the stand-in side is never dereferenced.
        var leavingClient = Clients.First();
        var (partyId, _) = SetupBesiegingPlayerParty(leavingClient);
        string? partyBaseId = null;
        leavingClient.Call(() =>
        {
            Assert.True(leavingClient.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            party.Party._mapEventSide = ObjectHelper.SkipConstructor<MapEventSide>();
            // The battle-leave request carries the PartyBase id (a separate registered object from the MobileParty).
            Assert.True(leavingClient.ObjectManager.TryGetId(party.Party, out partyBaseId));
        });
        Assert.NotNull(partyBaseId);

        // Act: join_siege_event's "Don't get involved." consequence.
        var disabledMethods = LeaveRoundTripDisabledMethods
            .Append(AccessTools.Method(typeof(MapEventSide), "RemovePartyInternal"))
            .ToList();
        leavingClient.Call(() =>
        {
            InvokePatchedBreakInLeave();
        }, disabledMethods);

        // Assert: the battle-side removal was routed, not applied locally — exactly one leave-battle request
        // named the leaver, and only from the leaver.
        var leaveRequest = Assert.Single(leavingClient.NetworkSentMessages.GetMessages<NetworkRequestLeaveBattle>());
        Assert.Equal(partyBaseId, leaveRequest.PartyId);
        Assert.Empty(Clients.Last().NetworkSentMessages.GetMessages<NetworkRequestLeaveBattle>());

        // ...and the camp break still routes alongside it.
        var breakRequest = Assert.Single(leavingClient.NetworkSentMessages.GetMessages<NetworkRequestBreakSiege>());
        Assert.Equal(partyId, breakRequest.PartyId);
        AssertBesiegerCamp(Server, partyId, expectCamp: false);

        // The leaver is still held.
        AssertPartyHeld(leavingClient, partyId);
    }

    [Fact]
    public void ClientLeaveSoldiersBehindAccept_WhileBesieging_RoutesCampRemovalWithoutMenuFinish()
    {
        // Arrange
        var leavingClient = Clients.First();
        var (partyId, _) = SetupBesiegingPlayerParty(leavingClient);

        // Act: the try-to-get-away accept keeps its native body (troop sacrifice, debrief menu) live in
        // the real game, so the prefix lets it run; here the body is suppressed because it needs a live
        // encounter and campaign models, while the routing prefix still fires first.
        var disabledMethods = LeaveRoundTripDisabledMethods
            .Append(AccessTools.Method(typeof(EncounterGameMenuBehavior), nameof(EncounterGameMenuBehavior.game_menu_encounter_leave_your_soldiers_behind_accept_on_consequence)))
            .ToList();
        leavingClient.Call(() =>
        {
            InvokePatchedLeaveSoldiersBehindAccept();
        }, disabledMethods);

        // Assert: embedded-write shape — the native flow owns its menus, so the approval must not
        // finish them.
        var request = Assert.Single(leavingClient.NetworkSentMessages.GetMessages<NetworkRequestBreakSiege>());
        Assert.Equal(partyId, request.PartyId);
        Assert.False(request.FinishLocalMenus);

        var approval = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBreakSiegeApproved>());
        Assert.True(approval.Approved);
        Assert.False(approval.FinishLocalMenus);
        AssertBesiegerCamp(Server, partyId, expectCamp: false);

        foreach (var client in Clients)
        {
            AssertBesiegerCamp(client, partyId, expectCamp: false);
        }
    }

    [Fact]
    public void ClientPlayerDefeat_WhileBesieging_RoutesCampRemovalWithoutMenuFinish()
    {
        // Arrange
        var leavingClient = Clients.First();
        var (partyId, _) = SetupBesiegingPlayerParty(leavingClient);

        // Act: DoPlayerDefeat must keep running natively (Finish, taken-prisoner menu), so its body is
        // suppressed here only because it needs a live map event; the routing prefix still fires first.
        var disabledMethods = LeaveRoundTripDisabledMethods
            .Append(AccessTools.Method(typeof(PlayerEncounter), nameof(PlayerEncounter.DoPlayerDefeat)))
            .ToList();
        leavingClient.Call(() =>
        {
            InvokePatchedPlayerDefeat();
        }, disabledMethods);

        // Assert
        var request = Assert.Single(leavingClient.NetworkSentMessages.GetMessages<NetworkRequestBreakSiege>());
        Assert.Equal(partyId, request.PartyId);
        Assert.False(request.FinishLocalMenus);

        var approval = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBreakSiegeApproved>());
        Assert.True(approval.Approved);
        Assert.False(approval.FinishLocalMenus);
        AssertBesiegerCamp(Server, partyId, expectCamp: false);

        foreach (var client in Clients)
        {
            AssertBesiegerCamp(client, partyId, expectCamp: false);
        }
    }

    [Fact]
    public void ClientSafePassageApply_WhileBesieging_RoutesCampRemovalWithoutMenuFinish()
    {
        // Arrange
        var leavingClient = Clients.First();
        var (partyId, _) = SetupBesiegingPlayerParty(leavingClient);

        // Act: Apply's body needs the live encounter's party lists, so it is suppressed; the prefix
        // requires PlayerEncounter.Current, which the driver stands up around the call.
        var disabledMethods = LeaveRoundTripDisabledMethods
            .Append(AccessTools.Method(typeof(SafePassageBarterable), nameof(SafePassageBarterable.Apply)))
            .ToList();
        leavingClient.Call(() =>
        {
            InvokePatchedSafePassageApply();
        }, disabledMethods);

        // Assert
        var request = Assert.Single(leavingClient.NetworkSentMessages.GetMessages<NetworkRequestBreakSiege>());
        Assert.Equal(partyId, request.PartyId);
        Assert.False(request.FinishLocalMenus);

        var approval = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBreakSiegeApproved>());
        Assert.True(approval.Approved);
        Assert.False(approval.FinishLocalMenus);
        AssertBesiegerCamp(Server, partyId, expectCamp: false);

        foreach (var client in Clients)
        {
            AssertBesiegerCamp(client, partyId, expectCamp: false);
        }
    }

    [Fact]
    public void ClientPlayerDefeat_WithoutBesiegerCamp_DoesNotRouteBreakSiege()
    {
        // Arrange: a defeat with no siege involved must stay fully native.
        var leavingClient = Clients.First();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        SetMainParty(leavingClient, partyId);

        // Act
        var disabledMethods = LeaveRoundTripDisabledMethods
            .Append(AccessTools.Method(typeof(PlayerEncounter), nameof(PlayerEncounter.DoPlayerDefeat)))
            .ToList();
        leavingClient.Call(() =>
        {
            InvokePatchedPlayerDefeat();
        }, disabledMethods);

        // Assert
        Assert.Empty(leavingClient.NetworkSentMessages.GetMessages<NetworkRequestBreakSiege>());
    }

    [Fact]
    public void ServerPlayerDefeat_RunsNativeWithoutRouting()
    {
        // Arrange: the host player's defeat stays native (patches live); nothing in the server
        // container handles a routed break request.
        var (partyId, _) = SetupBesiegingPlayerParty(Server);

        // Act
        var disabledMethods = LeaveRoundTripDisabledMethods
            .Append(AccessTools.Method(typeof(PlayerEncounter), nameof(PlayerEncounter.DoPlayerDefeat)))
            .ToList();
        Server.Call(() =>
        {
            InvokePatchedPlayerDefeat();
        }, disabledMethods);

        // Assert: no routing and no prefix-side camp write — the (here suppressed) native body owns it.
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkRequestBreakSiege>());
        AssertBesiegerCamp(Server, partyId, expectCamp: true);
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

    /// <summary>Invokes the real (Harmony-patched) join_encounter leave lambda, resolved through the
    /// same IL scan the patch's TargetMethod uses, on the compiler's closure singleton (or an
    /// uninitialized closure — the lambda captures nothing).</summary>
    private static void InvokePatchedJoinEncounterLeave()
    {
        var method = JoinEncounterLeaveLambdaPatches.ResolveJoinEncounterLeaveConsequence();
        Assert.NotNull(method);

        object instance = null;
        if (!method.IsStatic)
        {
            instance = AccessTools.Field(method.DeclaringType, "<>9")?.GetValue(null)
                ?? FormatterServices.GetUninitializedObject(method.DeclaringType);
        }

        method.Invoke(instance, new object[method.GetParameters().Length]);
    }

    /// <summary>Invokes the real (Harmony-patched)
    /// <c>EncounterGameMenuBehavior.break_in_leave_consequence</c> — the join_siege_event menu's
    /// "Don't get involved." option.</summary>
    private static void InvokePatchedBreakInLeave()
    {
        var method = AccessTools.Method(typeof(EncounterGameMenuBehavior), nameof(EncounterGameMenuBehavior.break_in_leave_consequence));
        Assert.NotNull(method);

        var behavior = ObjectHelper.SkipConstructor<EncounterGameMenuBehavior>();
        method.Invoke(behavior, new object[] { null });
    }

    /// <summary>Invokes the real (Harmony-patched)
    /// <c>EncounterGameMenuBehavior.game_menu_encounter_leave_your_soldiers_behind_accept_on_consequence</c>
    /// — the try-to-get-away accept.</summary>
    private static void InvokePatchedLeaveSoldiersBehindAccept()
    {
        var method = AccessTools.Method(typeof(EncounterGameMenuBehavior), nameof(EncounterGameMenuBehavior.game_menu_encounter_leave_your_soldiers_behind_accept_on_consequence));
        Assert.NotNull(method);

        var behavior = ObjectHelper.SkipConstructor<EncounterGameMenuBehavior>();
        method.Invoke(behavior, new object[] { null });
    }

    /// <summary>Invokes the real (Harmony-patched) <c>PlayerEncounter.DoPlayerDefeat</c> on a
    /// constructor-skipped encounter; the prefix reads only main-party state.</summary>
    private static void InvokePatchedPlayerDefeat()
    {
        var method = AccessTools.Method(typeof(PlayerEncounter), nameof(PlayerEncounter.DoPlayerDefeat));
        Assert.NotNull(method);

        var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
        method.Invoke(encounter, Array.Empty<object>());
    }

    /// <summary>Invokes the real (Harmony-patched) <c>SafePassageBarterable.Apply</c>. The prefix bails
    /// without a current encounter (native no-ops there), so a constructor-skipped encounter is stood up
    /// around the call; the skip-constructed barterable's null parties make the sally-out branch-A check
    /// fall through to the main-party camp write path.</summary>
    private static void InvokePatchedSafePassageApply()
    {
        var method = AccessTools.Method(typeof(SafePassageBarterable), nameof(SafePassageBarterable.Apply));
        Assert.NotNull(method);

        var barterable = ObjectHelper.SkipConstructor<SafePassageBarterable>();
        Campaign.Current.PlayerEncounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
        try
        {
            method.Invoke(barterable, Array.Empty<object>());
        }
        finally
        {
            Campaign.Current.PlayerEncounter = null;
        }
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

    /// <summary>
    /// Asserts the party was held on the given instance. break_in_leave_consequence and the join_encounter
    /// leave lambda end with an unconditional <see cref="MobileParty.SetMoveModeHold"/> after
    /// <see cref="PlayerEncounter.Finish"/>; the approval performs that Finish under an AllowedThread, so
    /// <c>PlayerEncounterPatches.FinishPostfix</c> skips its hold and the prefix must reissue it.
    /// </summary>
    private static void AssertPartyHeld(EnvironmentInstance instance, string partyId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.Equal(AiBehavior.Hold, party.DefaultBehavior);
            Assert.Equal(AiBehavior.Hold, party.ShortTermBehavior);
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
