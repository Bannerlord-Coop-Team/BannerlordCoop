using GameInterface.Services.MapTracks.Data;
using GameInterface.Services.MapTracks.Messages;
using GameInterface.Surrogates;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace Coop.IntegrationTests.Serialization
{
    /// <summary>
    /// Locks the wire contract for the map track messages and the TrackSurrogate behind them.
    /// </summary>
    public class MapTrackNetworkMessageSerializationTest
    {
        public MapTrackNetworkMessageSerializationTest()
        {
            // Registers TrackSurrogate, and the CampaignTime/CampaignVec2 surrogates it leans on
            _ = new SurrogateCollection();
        }

        /// <summary>Every field <c>TracksMatch</c> compares has to survive, or removals stop finding
        /// their target on the client.</summary>
        [Fact]
        public void NetworkUpdateClientsMapTrackData_RoundTrips_EveryFieldRemovalMatchingCompares()
        {
            var track = CreateTrack();

            var copy = RoundTrip(new NetworkUpdateClientsMapTrackData(
                new Dictionary<string, List<MapTrackData>>
                {
                    ["party1"] = new() { new MapTrackData(track, "Kingdom_empire") }
                },
                isRemovingTracks: false));

            var copiedTrack = Assert.Single(copy.VisibleTrackChange["party1"]).Track;

            Assert.Equal(track.Direction, copiedTrack.Direction);
            Assert.Equal(track.PartyName.ToString(), copiedTrack.PartyName.ToString());
            Assert.Equal(track.Speed, copiedTrack.Speed);
            Assert.Equal(track.NumberOfAllMembers, copiedTrack.NumberOfAllMembers);
            Assert.Equal(track.NumberOfHealthyMembers, copiedTrack.NumberOfHealthyMembers);
            Assert.Equal(track.NumberOfMenWithHorse, copiedTrack.NumberOfMenWithHorse);
            Assert.Equal(track.NumberOfMenWithoutHorse, copiedTrack.NumberOfMenWithoutHorse);
            Assert.Equal(track.NumberOfPackAnimals, copiedTrack.NumberOfPackAnimals);
            Assert.Equal(track.NumberOfPrisoners, copiedTrack.NumberOfPrisoners);
            Assert.Equal(track.CreationTime, copiedTrack.CreationTime);
            Assert.Equal(track.Life, copiedTrack.Life);
            Assert.Equal(track.PartyType, copiedTrack.PartyType);
            Assert.Equal(track.IsPointer, copiedTrack.IsPointer);
        }

        /// <summary>The party name is flattened to a string and rebuilt on the far side, so it is the
        /// field most able to round-trip as something that no longer compares equal.</summary>
        [Fact]
        public void TrackSurrogate_RoundTrips_PartyName()
        {
            var track = CreateTrack();
            track.PartyName = new TextObject("Vlandian Militia");

            var copy = RoundTrip(new NetworkUpdateClientInitialVisibleTracks(
                new List<MapTrackData> { new(track, null) }));

            Assert.Equal("Vlandian Militia", Assert.Single(copy.VisibleTrackChanges).Track.PartyName.ToString());
        }

        /// <summary>The source faction rides alongside the track rather than on it, because hostility is
        /// resolved against whichever player is looking. Losing it silently drops enemy-track scouting
        /// xp (2x to 10x) and enemy colouring back to the neutral defaults.
        /// </summary>
        [Fact]
        public void NetworkUpdateClientsMapTrackData_RoundTrips_SourceMapFaction()
        {
            var copy = RoundTrip(new NetworkUpdateClientsMapTrackData(
                new Dictionary<string, List<MapTrackData>>
                {
                    ["party1"] = new() { new MapTrackData(CreateTrack(), "Kingdom_vlandia") },
                    ["party2"] = new() { new MapTrackData(CreateTrack(), null) }
                },
                isRemovingTracks: false));

            Assert.Equal("Kingdom_vlandia", Assert.Single(copy.VisibleTrackChange["party1"]).MapFactionId);

            // A track whose party had no map faction travels as absent and must not arrive as ""
            Assert.Null(Assert.Single(copy.VisibleTrackChange["party2"]).MapFactionId);
        }

        /// <summary>Per-player delivery means the dictionary keying has to survive: a client applies only
        /// the entry under its own party id and discards the rest.
        /// </summary>
        [Fact]
        public void NetworkUpdateClientsMapTrackData_RoundTrips_PerPlayerKeying()
        {
            var copy = RoundTrip(new NetworkUpdateClientsMapTrackData(
                new Dictionary<string, List<MapTrackData>>
                {
                    ["party1"] = new() { new MapTrackData(CreateTrack(), null), new MapTrackData(CreateTrack(), null) },
                    ["party2"] = new() { new MapTrackData(CreateTrack(), null) }
                },
                isRemovingTracks: false));

            Assert.Equal(2, copy.VisibleTrackChange.Count);
            Assert.Equal(2, copy.VisibleTrackChange["party1"].Count);
            Assert.Single(copy.VisibleTrackChange["party2"]);
        }

        /// <summary>Map arrows are pointers with no culture. The flag is what stops a client treating one
        /// as an ordinary spotted party, and what carries it past the culture guard in
        /// ApplyVisibleTrackChanges.</summary>
        [Fact]
        public void NetworkUpdateClientInitialVisibleTracks_RoundTrips_PointerWithoutCulture()
        {
            var arrow = CreateTrack();
            arrow.IsPointer = true;

            var copy = RoundTrip(new NetworkUpdateClientInitialVisibleTracks(
                new List<MapTrackData> { new(arrow, null) }));

            var copiedArrow = Assert.Single(copy.VisibleTrackChanges).Track;

            Assert.True(copiedArrow.IsPointer);
            Assert.Null(copiedArrow.Culture);
        }

        /// <summary>The removal direction carries the same payload and must keep its flag, or the client
        /// adds expired tracks instead of dropping them.</summary>
        [Fact]
        public void NetworkUpdateClientsMapTrackData_RoundTrips_IsRemovingTracks()
        {
            var copy = RoundTrip(new NetworkUpdateClientsMapTrackData(
                new Dictionary<string, List<MapTrackData>>
                {
                    ["party1"] = new() { new MapTrackData(CreateTrack(), null) }
                },
                isRemovingTracks: true));

            Assert.True(copy.IsRemovingTracks);
        }

        private static Track CreateTrack() => new()
        {
            Direction = 1.25f,
            PartyName = new TextObject("Test Party"),
            Culture = null,
            Speed = 4.5f,
            NumberOfAllMembers = 31,
            NumberOfHealthyMembers = 27,
            NumberOfMenWithHorse = 12,
            NumberOfMenWithoutHorse = 19,
            NumberOfPackAnimals = 3,
            NumberOfPrisoners = 5,
            CreationTime = CampaignTime.Hours(37f),
            Life = 22f,
            PartyType = Track.PartyTypeEnum.Bandit
        };

        private static T RoundTrip<T>(T original)
        {
            using var stream = new MemoryStream();
            Serializer.Serialize(stream, original);
            stream.Position = 0;
            return Serializer.Deserialize<T>(stream);
        }
    }
}
