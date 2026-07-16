using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Common.Messaging;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages.Leave;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// BR-091 (Idempotent Cleanup): mission and map-event cleanup operations must be safe to repeat without
/// duplicating rewards, deleting unrelated parties, or corrupting campaign state. The map-event finalize is
/// deduped per event instance (<c>BattleFinalizeHandler.TryMarkFinalized</c>, keyed by a
/// <see cref="System.Runtime.CompilerServices.ConditionalWeakTable{TKey, TValue}"/>), so a repeated finalize of
/// one battle is ignored and never reaches into a different, concurrent battle. The mission-active gate
/// (<see cref="BattleSpawnGate"/>) is likewise cleared idempotently on mission exit.
/// </summary>
public class IdempotentCleanupTests : MapEventTestBase
{
    public IdempotentCleanupTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// The "deleting unrelated parties" clause: with TWO concurrent map events, repeatedly finalizing event A must
    /// tear down A (once) and leave the unrelated event B and its participating parties' rosters completely
    /// untouched — the finalize is scoped to the event it targets, not a global sweep.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-091")]
    public void RepeatedFinalizeOfOneBattle_LeavesAnUnrelatedConcurrentBattleUntouched()
    {
        // Event B is the bystander battle: seed a known troop into both its parties so any stray mutation shows.
        var eventB = CreateServerMapEvent();
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        SeedPartyTroopOnAll(eventB.AttackerPartyId, troopId, 3);
        SeedPartyTroopOnAll(eventB.DefenderPartyId, troopId, 2);

        // The harness parties spawn with their own (varying) rosters, so capture each instance's post-seed
        // man counts — the exact state the repeated finalize of A must leave untouched.
        var instances = new List<EnvironmentInstance> { Server };
        instances.AddRange(Clients);
        var attackerBaseline = instances.ToDictionary(i => i, i => GetPartyManCount(i, eventB.AttackerPartyId));
        var defenderBaseline = instances.ToDictionary(i => i, i => GetPartyManCount(i, eventB.DefenderPartyId));

        // Event A is the one that gets finalized (twice).
        var eventA = CreateServerMapEvent();

        // Repeat the finalize of A: resolve it once and publish the finalize twice, back to back — a duplicate
        // cleanup (e.g. a post-migration second "done"). The first tears A down; the second must be a no-op.
        var disabled = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ExitToLast)))
            .ToList();

        var client1 = Clients.First();
        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<MapEvent>(eventA.MapEventId, out var mapEventA));
            var broker = client1.Resolve<IMessageBroker>();
            broker.Publish(this, new MapEventFinalizeAttempted(mapEventA));
            broker.Publish(this, new MapEventFinalizeAttempted(mapEventA)); // the repeated cleanup
        }, disabled);

        // A was cleaned up (once) on the server and every client...
        AssertMapEventRemoved(Server, eventA.MapEventId);
        foreach (var client in Clients)
            AssertMapEventRemoved(client, eventA.MapEventId);

        // ...while B — an unrelated concurrent battle — still exists everywhere with its rosters intact:
        // the total man count is unchanged from the pre-finalize baseline and the seeded troops are still there.
        foreach (var instance in instances)
        {
            AssertMapEventPresent(instance, eventB.MapEventId);
            AssertPartyRosterUntouched(instance, eventB.AttackerPartyId, troopId, attackerBaseline[instance], 3);
            AssertPartyRosterUntouched(instance, eventB.DefenderPartyId, troopId, defenderBaseline[instance], 2);
        }
    }

    private static int GetPartyManCount(EnvironmentInstance instance, string partyId)
    {
        int count = 0;
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            count = party.MemberRoster.TotalManCount;
        });
        return count;
    }

    private static void AssertPartyRosterUntouched(
        EnvironmentInstance instance, string partyId, string troopId, int expectedTotal, int expectedSeeded)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));

            Assert.True(expectedTotal == party.MemberRoster.TotalManCount,
                $"[{instance.GetType().Name}] party {partyId} roster changed: expected {expectedTotal} men, has {party.MemberRoster.TotalManCount}");
            Assert.True(expectedSeeded == party.MemberRoster.GetElementNumber(troop),
                $"[{instance.GetType().Name}] party {partyId} lost its seeded troops: expected {expectedSeeded}, has {party.MemberRoster.GetElementNumber(troop)}");
        });
    }

    /// <summary>
    /// The mission-side half of BR-091: the coop-battle-active gate (<see cref="BattleSpawnGate"/>) that the
    /// Missions stack clears on mission exit must be safe to clear more than once (a duplicated teardown, a
    /// migration, a reconnect) — a second <see cref="BattleSpawnGate.EndBattle"/> neither throws nor resurrects
    /// the previously-active battle.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-091")]
    public void RepeatedMissionEndBattle_ClearsTheActiveBattleGate_Idempotently()
    {
        BattleSpawnGate.BeginBattle("some-map-event-id");
        Assert.True(BattleSpawnGate.IsCoopBattleActive);

        // First teardown clears the gate.
        BattleSpawnGate.EndBattle();
        Assert.False(BattleSpawnGate.IsCoopBattleActive);
        Assert.Null(BattleSpawnGate.ActiveMapEventId);

        // A repeated teardown is a safe no-op — no throw, and the battle stays cleared.
        BattleSpawnGate.EndBattle();
        Assert.False(BattleSpawnGate.IsCoopBattleActive);
        Assert.Null(BattleSpawnGate.ActiveMapEventId);
    }

    private static void AssertMapEventRemoved(EnvironmentInstance instance, string mapEventId)
    {
        instance.Call(() =>
            Assert.False(instance.ObjectManager.TryGetObject<MapEvent>(mapEventId, out _),
                $"MapEvent {mapEventId} should be finalized/removed on {instance.GetType().Name}"));
    }

    private static void AssertMapEventPresent(EnvironmentInstance instance, string mapEventId)
    {
        instance.Call(() =>
            Assert.True(instance.ObjectManager.TryGetObject<MapEvent>(mapEventId, out _),
                $"Unrelated MapEvent {mapEventId} should still exist on {instance.GetType().Name}"));
    }
}
