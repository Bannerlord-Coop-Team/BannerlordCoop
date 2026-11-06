using GameInterface.Services.MapEvents.Messages.Leave;
using ProtoBuf;
using Xunit;

namespace GameInterface.Tests.Serialization;

/// <summary>Finalization replies retain the battle they are allowed to close.</summary>
public class MapEventFinalizedSerializationTests
{
    [Fact]
    public void FinalizedReply_RetainsMapEventId()
    {
        var copy = Serializer.DeepClone(new NetworkMapEventFinalized("MapEvent_Created_45739"));

        Assert.Equal("MapEvent_Created_45739", copy.MapEventId);
    }
}
