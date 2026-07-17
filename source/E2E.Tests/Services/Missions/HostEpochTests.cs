using System;
using System.Linq;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.MapEvents;
using Missions.Messages;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// BR-102 (Host Epoch and Stale Host Rejection): "Each host assignment — the initial election and every
/// migration — shall carry a host epoch: a monotonically increasing generation number scoped to the battle
/// instance and issued by the server."
/// <para>
/// These tests cover the epoch's issuance and the assignment-side ordering it enables: the initial election
/// carries epoch 1 and every migration promotion increments it (the epoch counts HOST CHANGES, so a
/// successor-line append re-broadcasts the unchanged epoch), and a client that already holds a newer
/// assignment ignores a stale (lower-epoch) broadcast delivered late/out of order. The rejection of
/// host-authority MESSAGES stamped with a stale epoch is covered by
/// <see cref="HostEpochStaleConclusionTests"/>.
/// </para>
/// </summary>
public class HostEpochTests : MissionTestEnvironment
{
    public HostEpochTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    /// <summary>
    /// The initial election is epoch 1, the first migration promotion is epoch 2, and the second migration is
    /// epoch 3 — asserted on the server and on every client after each server broadcast.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-102")]
    public void InitialElectionIsEpochOne_AndEveryMigrationIncrementsTheEpoch()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B", "ctrl-C");
        var clients = Clients.ToArray();

        EnterBattle(clients[0], mapEventId); // ctrl-A first mission-ready -> elected host
        EnterBattle(clients[1], mapEventId); // ctrl-B -> successor
        EnterBattle(clients[2], mapEventId); // ctrl-C -> successor

        // The initial election issued epoch 1.
        AssertHost(Server, mapEventId, "ctrl-A", "ctrl-B", "ctrl-C");
        AssertEpochEverywhere(mapEventId, 1);

        // First migration: the host departs, ctrl-B is promoted -> epoch 2.
        DepartBattle("ctrl-A", mapEventId);
        AssertHost(Server, mapEventId, "ctrl-B", "ctrl-C");
        AssertEpochEverywhere(mapEventId, 2);

        // Second migration: the promoted host departs too, ctrl-C is promoted -> epoch 3.
        DepartBattle("ctrl-B", mapEventId);
        AssertHost(Server, mapEventId, "ctrl-C");
        AssertEpochEverywhere(mapEventId, 3);
    }

    /// <summary>
    /// The epoch counts HOST CHANGES only. A later player appending to the successor line (and a departed
    /// player re-entering at the tail after a migration) re-broadcasts the assignment with the UNCHANGED
    /// epoch — who the host is did not change.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-102")]
    public void SuccessorAppend_DoesNotChangeTheEpoch()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B");
        var clients = Clients.ToArray();

        EnterBattle(clients[0], mapEventId); // ctrl-A -> elected host, epoch 1
        AssertEpochEverywhere(mapEventId, 1);

        EnterBattle(clients[1], mapEventId); // ctrl-B appends to the successor line
        AssertHost(Server, mapEventId, "ctrl-A", "ctrl-B");
        AssertEpochEverywhere(mapEventId, 1); // append: same host, same epoch

        DepartBattle("ctrl-A", mapEventId);  // migration -> ctrl-B, epoch 2
        AssertHost(Server, mapEventId, "ctrl-B");
        AssertEpochEverywhere(mapEventId, 2);

        EnterBattle(clients[0], mapEventId); // the departed player re-enters -> successor tail append
        AssertHost(Server, mapEventId, "ctrl-B", "ctrl-A");
        AssertEpochEverywhere(mapEventId, 2); // still the same hosting generation
    }

    /// <summary>
    /// A stale <see cref="NetworkBattleHostAssigned"/> — a lower-epoch broadcast naming the OLD host,
    /// delivered late after a migration — must not overwrite the newer assignment a client already holds.
    /// An EQUAL-epoch broadcast is a successor-line update for the same hosting generation and applies.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-102")]
    public void StaleLowerEpochAssignment_DeliveredAfterMigration_DoesNotOverwriteTheNewerOne()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B");
        var clients = Clients.ToArray();

        EnterBattle(clients[0], mapEventId); // ctrl-A -> host (epoch 1)
        EnterBattle(clients[1], mapEventId); // ctrl-B -> successor

        DepartBattle("ctrl-A", mapEventId);  // migration -> ctrl-B is the host at epoch 2
        AssertHost(Server, mapEventId, "ctrl-B");
        AssertEpochEverywhere(mapEventId, 2);

        // A stale epoch-1 broadcast (the original election naming ctrl-A) arrives late on every client —
        // e.g. re-delivered/reordered around the migration. It must be ignored, not applied.
        foreach (var client in clients)
            client.SimulateMessage(Server.NetPeer,
                new NetworkBattleHostAssigned(mapEventId, "ctrl-A", Array.Empty<string>(), 1));

        foreach (var client in clients)
        {
            AssertHost(client, mapEventId, "ctrl-B");
            AssertEpoch(client, mapEventId, 2);
        }
        AssertIsLocalHost(clients[1], mapEventId, true);  // the migrated host keeps its authority
        AssertIsLocalHost(clients[0], mapEventId, false); // the former host does NOT get it back

        // An EQUAL-epoch broadcast is a successor-line update within the same hosting generation (e.g. a
        // reconnect append) and must apply.
        foreach (var client in clients)
            client.SimulateMessage(Server.NetPeer,
                new NetworkBattleHostAssigned(mapEventId, "ctrl-B", new[] { "ctrl-A" }, 2));

        foreach (var client in clients)
        {
            AssertHost(client, mapEventId, "ctrl-B", "ctrl-A");
            AssertEpoch(client, mapEventId, 2);
        }
    }

    private void AssertEpochEverywhere(string mapEventId, int expectedEpoch)
    {
        AssertEpoch(Server, mapEventId, expectedEpoch);
        foreach (var client in Clients)
            AssertEpoch(client, mapEventId, expectedEpoch);
    }

    private void AssertEpoch(EnvironmentInstance instance, string mapEventId, int expectedEpoch)
    {
        instance.Call(() =>
        {
            var registry = instance.Resolve<IBattleHostRegistry>();
            Assert.True(registry.TryGet(mapEventId, out var assignment),
                $"No host assignment for {mapEventId} on {instance.GetType().Name}");
            Assert.True(expectedEpoch == assignment.Epoch,
                $"Expected host epoch {expectedEpoch} on {instance.GetType().Name}, found {assignment.Epoch}");
        });
    }
}
