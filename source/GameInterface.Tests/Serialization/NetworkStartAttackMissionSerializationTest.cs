using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Surrogates;
using ProtoBuf.Meta;
using System.IO;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Serialization;

public class NetworkStartAttackMissionSerializationTest
{
    public NetworkStartAttackMissionSerializationTest()
    {
        new SurrogateCollection();
    }

    [Fact]
    public void RoundTrip_PreservesRandomTerrainSeed()
    {
        var original = new NetworkStartAttackMission("map-event-1", 4242, new AtmosphereInfo
        {
            TimeInfo = new TimeInformation
            {
                TimeOfDay = 14.0f,
                NightTimeFactor = 0.0f,
            },
        });

        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            RuntimeTypeModel.Default.Serialize(ms, original);
            bytes = ms.ToArray();
        }

        Assert.NotEmpty(bytes);

        NetworkStartAttackMission result;
        using (var ms = new MemoryStream(bytes))
        {
            result = (NetworkStartAttackMission)RuntimeTypeModel.Default.Deserialize(ms, null, typeof(NetworkStartAttackMission));
        }

        Assert.Equal(original.MapEventId, result.MapEventId);
        Assert.Equal(original.RandomTerrainSeed, result.RandomTerrainSeed);
        Assert.Equal(original.AtmosphereOnCampaign.TimeInfo.TimeOfDay, result.AtmosphereOnCampaign.TimeInfo.TimeOfDay);
        Assert.Equal(original.AtmosphereOnCampaign.TimeInfo.NightTimeFactor, result.AtmosphereOnCampaign.TimeInfo.NightTimeFactor);
        Assert.Equal("map-event-1", result.MapEventId);
        Assert.Equal(4242, result.RandomTerrainSeed);
    }
}
