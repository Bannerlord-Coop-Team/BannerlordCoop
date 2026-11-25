using Common.Messaging;
using Common.Serialization;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.AdvancedStart.Data;

namespace Coop.IntegrationTests.Serialization
{
    public class AdvancedStartNetworkMessageSerializationTest
    {
        private readonly ProtoBufSerializer serializer = new ProtoBufSerializer(new SerializableTypeMapper());

        [Fact]
        public void NetworkTransferNewHero_RoundTrips_Selections()
        {
            var message = new NetworkTransferNewHero("player", new byte[] { 1, 2, 3 }, new[]
            {
                new AdvancedStartSelection("StartType", "vassal"),
                new AdvancedStartSelection("KingdomId", "vlandia"),
            });

            var copy = RoundTrip(message);

            Assert.Equal("player", copy.PlayerId);
            Assert.Equal(new byte[] { 1, 2, 3 }, copy.PlayerHero);
            Assert.Collection(copy.AdvancedStart,
                selection => { Assert.Equal("StartType", selection.Id); Assert.Equal("vassal", selection.Value); },
                selection => { Assert.Equal("KingdomId", selection.Id); Assert.Equal("vlandia", selection.Value); });
        }

        [Fact]
        public void NetworkAdvancedStartPolicy_RoundTrips_NullPolicy()
        {
            // A null policy is how the host turns the window off.
            var copy = RoundTrip(new NetworkAdvancedStartPolicy(null));

            Assert.Null(copy.Policy);
        }

        [Fact]
        public void NetworkAdvancedStartPolicy_RoundTrips_EmptyArraysAsNull()
        {
            var policy = new AdvancedStartPolicy(new[] { "default", "king" },
                worldStartTypes: new[] { "king" },
                kingdomStartTypes: new[] { "king" },
                defaultStartType: "king",
                worldStarted: true,
                playerRuledKingdomIds: Array.Empty<string>(),
                joinableKingdomIds: new[] { "vlandia" });

            var copy = RoundTrip(new NetworkAdvancedStartPolicy(policy)).Policy;

            Assert.Equal(new[] { "default", "king" }, copy.StartTypes);
            Assert.Equal(new[] { "king" }, copy.WorldStartTypes);
            Assert.Equal(new[] { "king" }, copy.KingdomStartTypes);
            Assert.Equal("king", copy.DefaultStartType);
            Assert.True(copy.WorldStarted);
            // protobuf-net writes nothing for an empty array, so readers have to treat null as empty.
            Assert.Null(copy.PlayerRuledKingdomIds);
            Assert.Equal(new[] { "vlandia" }, copy.JoinableKingdomIds);
        }

        private T RoundTrip<T>(T message) where T : IMessage
        {
            byte[] bytes = serializer.Serialize(message);
            return Assert.IsType<T>(serializer.Deserialize<IMessage>(bytes));
        }
    }
}
