using GameInterface.Services.MapEvents.Messages.Conversation;
using ProtoBuf;

namespace Coop.IntegrationTests.Serialization
{
    public class ConversationEndedNetworkMessageSerializationTest
    {
        [Fact]
        public void NetworkConversationEnded_RoundTrips_FinishedMapEventId()
        {
            using var stream = new MemoryStream();
            Serializer.Serialize(stream, new NetworkConversationEnded("request-1", "MapEvent_Created_1"));
            stream.Position = 0;

            var copy = Serializer.Deserialize<NetworkConversationEnded>(stream);

            Assert.Equal("request-1", copy.RequestId);
            Assert.Equal("MapEvent_Created_1", copy.FinishedMapEventId);
        }
    }
}
