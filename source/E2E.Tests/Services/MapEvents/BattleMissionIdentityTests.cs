using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.Missions;
using GameInterface.Services.MapEvents;
using TaleWorlds.CampaignSystem.MapEvents;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// End-to-end coverage of mission identity (BR-104): every battle mission has a unique identifier — the
/// server-assigned map-event object-manager id — that persists for the life of the battle instance, including
/// across host migrations and across a player's retreat while other players remain (BR-054). Messages from a
/// previous or unrelated battle mission must not affect the current mission. Runs on three clients so host
/// migration and a mid-line retreat can both be driven against the same instance.
/// </summary>
public class BattleMissionIdentityTests : MissionTestEnvironment
{
    public BattleMissionIdentityTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    /// <summary>
    /// BR-104: a departure message scoped to a DIFFERENT (previous/unrelated) battle instance id — even one
    /// that names the current host — must not affect the current mission's instance record. The current host
    /// line and the current instance id are untouched on the server and every client.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-104")]
    public void MemberDeparture_ScopedToAForeignBattleInstanceId_DoesNotAffectCurrentInstance()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B");
        var clients = Clients.ToArray();
        EnterBattle(clients[0], mapEventId); // ctrl-A -> host
        EnterBattle(clients[1], mapEventId); // ctrl-B -> successor
        AssertHost(Server, mapEventId, "ctrl-A", "ctrl-B");

        // A departure for an unrelated battle instance id, naming the current host, arrives at the server.
        const string foreignInstanceId = "previous-unrelated-battle-instance";
        DepartBattle("ctrl-A", foreignInstanceId);

        // The current instance is untouched: same host line, same instance id, everywhere.
        AssertHost(Server, mapEventId, "ctrl-A", "ctrl-B");
        foreach (var client in Clients)
            AssertHost(client, mapEventId, "ctrl-A", "ctrl-B");
        AssertBattleInstanceIdentity(Server, mapEventId);
        foreach (var client in Clients)
            AssertBattleInstanceIdentity(client, mapEventId);
    }

    /// <summary>
    /// Asserts the battle instance identity on <paramref name="instance"/>: the map event still resolves by
    /// <paramref name="mapEventId"/> and round-trips back to it (identity unchanged), and the battle instance
    /// record (host registry) is still keyed by that same id.
    /// </summary>
    private void AssertBattleInstanceIdentity(EnvironmentInstance instance, string mapEventId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent),
                $"map event {mapEventId} should still resolve on {instance.GetType().Name}");
            Assert.True(instance.ObjectManager.TryGetId(mapEvent, out var roundTripId));
            Assert.Equal(mapEventId, roundTripId);
            Assert.True(instance.Resolve<IBattleHostRegistry>().TryGet(mapEventId, out _),
                $"battle instance record should be keyed by {mapEventId} on {instance.GetType().Name}");
        });
    }
}
