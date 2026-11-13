using GameInterface.Services.Ships.Messages;
using ProtoBuf;
using System.IO;
using TaleWorlds.Core;
using Xunit;

namespace Coop.Tests.GameInterface.Services.Ships;

public class NetworkNpcFleetAttemptRecordedTests
{
    [Fact]
    public void RecordedAttempt_RoundTripsOverTheWire()
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, new NetworkNpcFleetAttemptRecorded("MapEvent_1", BattleSideEnum.Defender, aiHulls: 5,
            playerHulls: 3, countedPartyIds: new[] { "MobileParty_1", "MobileParty_2" }));
        stream.Position = 0;

        var copy = Serializer.Deserialize<NetworkNpcFleetAttemptRecorded>(stream);

        Assert.Equal(("MapEvent_1", BattleSideEnum.Defender, 5, 3), (copy.InstanceId, copy.Side, copy.AiHulls, copy.PlayerHulls));
        Assert.Equal(new[] { "MobileParty_1", "MobileParty_2" }, copy.CountedPartyIds);
    }
}
