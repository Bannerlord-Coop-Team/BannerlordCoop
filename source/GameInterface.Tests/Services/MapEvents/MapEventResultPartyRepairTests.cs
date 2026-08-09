using GameInterface.Services.MapEvents.Patches;
using System.Reflection;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using Xunit;
using FormatterServices = System.Runtime.Serialization.FormatterServices;

namespace GameInterface.Tests.Services.MapEvents
{
    public class MapEventResultPartyRepairTests
    {
        private static readonly FieldInfo SidesField =
            typeof(MapEvent).GetField("_sides", BindingFlags.NonPublic | BindingFlags.Instance)!;
        private static readonly FieldInfo BattlePartiesField =
            typeof(MapEventSide).GetField("_battleParties", BindingFlags.NonPublic | BindingFlags.Instance)!;

        [Fact]
        public void PartiesWithoutParty_RemovesFromBothSides()
        {
            MapEventParty defender = CreateResolvedParty();
            MapEventParty attacker = CreateResolvedParty();
            MapEvent mapEvent = CreateMapEvent(
                CreateSide(CreatePartyWithoutParty(), defender),
                CreateSide(attacker, CreatePartyWithoutParty()));

            int removedPartyCount = MapEventPatches.RemovePartiesWithoutParty(mapEvent);

            Assert.Equal(2, removedPartyCount);
            Assert.Collection(mapEvent.DefenderSide.Parties, party => Assert.Same(defender, party));
            Assert.Collection(mapEvent.AttackerSide.Parties, party => Assert.Same(attacker, party));
        }

        [Fact]
        public void ResolvedParties_PreservesPartiesAndOrder()
        {
            MapEventParty firstDefender = CreateResolvedParty();
            MapEventParty secondDefender = CreateResolvedParty();
            MapEventParty attacker = CreateResolvedParty();
            MapEvent mapEvent = CreateMapEvent(
                CreateSide(firstDefender, secondDefender),
                CreateSide(attacker));

            int removedPartyCount = MapEventPatches.RemovePartiesWithoutParty(mapEvent);

            Assert.Equal(0, removedPartyCount);
            Assert.Collection(
                mapEvent.DefenderSide.Parties,
                party => Assert.Same(firstDefender, party),
                party => Assert.Same(secondDefender, party));
            Assert.Collection(mapEvent.AttackerSide.Parties, party => Assert.Same(attacker, party));
        }

        private static MapEvent CreateMapEvent(MapEventSide defender, MapEventSide attacker)
        {
            MapEvent mapEvent = (MapEvent)FormatterServices.GetUninitializedObject(typeof(MapEvent));
            SidesField.SetValue(mapEvent, new[] { defender, attacker });
            return mapEvent;
        }

        private static MapEventSide CreateSide(params MapEventParty[] parties)
        {
            MapEventSide side = (MapEventSide)FormatterServices.GetUninitializedObject(typeof(MapEventSide));
            BattlePartiesField.SetValue(side, new MBList<MapEventParty>(parties));
            return side;
        }

        private static MapEventParty CreateResolvedParty()
        {
            MapEventParty mapEventParty = CreatePartyWithoutParty();
            mapEventParty.Party = (PartyBase)FormatterServices.GetUninitializedObject(typeof(PartyBase));
            return mapEventParty;
        }

        private static MapEventParty CreatePartyWithoutParty() =>
            (MapEventParty)FormatterServices.GetUninitializedObject(typeof(MapEventParty));
    }
}
