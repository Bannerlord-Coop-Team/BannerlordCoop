using E2E.Tests.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

public class MapEventResultCommitTests : MapEventTestBase
{
    public MapEventResultCommitTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// The server is authoritative for committing the battle economy (xp/renown/influence/morale/gold) and
    /// replicates it. After a battle the client's PlayerEncounter result path reaches
    /// <c>MapEvent.CommitCalculatedMapEventResults</c> locally and would re-apply that economy on top of the
    /// server's already-replicated values. The client patch skips that commit. We assert it via a party's
    /// <c>PlunderedGold</c>: the commit's gold step unconditionally zeroes PlunderedGold after applying it, so
    /// if the commit were not skipped on the client the seeded value would drop to 0; with the patch it stays.
    /// </summary>
    [Fact]
    public void ClientCommitCalculatedMapEventResults_DoesNotApply()
    {
        // Arrange — a server-authored battle, replicated to the client with its sides and parties.
        var ctx = CreateServerMapEvent();
        var client = Clients.First();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(ctx.MapEventId, out var mapEvent));

            var winnerParty = mapEvent.AttackerSide.Parties.First();

            // Seed a non-zero plunder. CommitGoldChanges (the last step of the commit) zeroes PlunderedGold on
            // every party after applying it, so a running commit would reset this to 0.
            winnerParty.PlunderedGold = 500;

            // The exact method the client's encounter result path reaches via CalculateAndCommitMapEventResults.
            AccessTools.Method(typeof(MapEvent), "CommitCalculatedMapEventResults").Invoke(mapEvent, null);

            // The client patch skipped the commit, so the seeded plunder was never consumed.
            Assert.Equal(500, winnerParty.PlunderedGold);
        }, MapEventDisabledMethods);
    }
}
