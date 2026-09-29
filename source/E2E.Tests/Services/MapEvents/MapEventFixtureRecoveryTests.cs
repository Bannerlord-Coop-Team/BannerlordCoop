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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PartiallyFinalizedFixture_ReleasesPartiesAndRemovesReplicas(bool partiesDetached)
    {
        var context = CreateServerMapEvent();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out var mapEvent));
            var parties = new[] { mapEvent.AttackerSide.LeaderParty, mapEvent.DefenderSide.LeaderParty };
            mapEvent.State = MapEventState.WaitingRemoval;
            Assert.True(mapEvent.IsFinalized);
            if (partiesDetached)
            {
                foreach (var party in parties) party._mapEventSide = null;
                mapEvent.AttackerSide.Clear();
                mapEvent.DefenderSide.Clear();
            }
            Assert.Equal(!partiesDetached, MapEventDebugCommands.HasAttachedParties(mapEvent, parties));
            Assert.True(Server.ObjectManager.Contains(mapEvent));

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
