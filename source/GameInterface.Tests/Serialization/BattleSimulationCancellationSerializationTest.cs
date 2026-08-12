using System.IO;
using GameInterface.Services.MapEvents.Messages.Start;
using ProtoBuf;
using Xunit;

namespace GameInterface.Tests.Serialization;

public class BattleSimulationCancellationSerializationTest
{
    // Ensures the cancellation message keeps its MapEventId after serialization.
    [Fact]
    public void NetworkCancelBattleSimulation_RoundTrips()
    {
        var original = new NetworkCancelBattleSimulation("mapEvent_1");

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Position = 0;

        var copy = Serializer.Deserialize<NetworkCancelBattleSimulation>(stream);

        Assert.Equal(original.MapEventId, copy.MapEventId);
    }   
}