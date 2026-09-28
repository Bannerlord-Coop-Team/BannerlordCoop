using GameInterface.Services.Villages.Commands;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

public class MapEventFixtureRecoveryTests : MapEventTestBase
{
    public MapEventFixtureRecoveryTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void PartiallyFinalizedFixture_ReleasesPartiesAndRemovesReplicas()
    {
        var context = CreateServerMapEvent();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out var mapEvent));
            var parties = new[] { mapEvent.AttackerSide.LeaderParty, mapEvent.DefenderSide.LeaderParty };
            mapEvent.State = MapEventState.WaitingRemoval;
            Assert.True(mapEvent.IsFinalized);
            Assert.True(MapEventDebugCommands.HasAttachedParties(mapEvent, parties));

            MapEventDebugCommands.RecoverPartiallyFinalizedMapEvent(mapEvent, parties);

            Assert.False(MapEventDebugCommands.HasAttachedParties(mapEvent, parties));
            Assert.False(Server.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out _));
            Assert.All(parties, party => Assert.Null(party.MobileParty.MapEvent));
        }, MapEventDisabledMethods);

        foreach (var client in Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out _));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(context.AttackerPartyId, out var attacker));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(context.DefenderPartyId, out var defender));
            Assert.Null(attacker.MapEvent);
            Assert.Null(defender.MapEvent);
        }
    }
}
