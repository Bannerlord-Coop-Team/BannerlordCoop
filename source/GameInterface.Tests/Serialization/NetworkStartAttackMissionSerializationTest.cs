using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Surrogates;
using ProtoBuf.Meta;
using System.IO;
using TaleWorlds.Core;
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
    public void RoundTrip_PreservesMissionInitializer()
    {
        var initializer = new MissionInitializerRecord("battle_terrain_030")
        {
            TerrainType = 7,
            RandomTerrainSeed = 4242,
            SceneHasMapPatch = true,
            PatchCoordinates = new Vec2(0.25f, 0.75f),
            PatchEncounterDir = new Vec2(1f, 0f),
            AtmosphereOnCampaign = new AtmosphereInfo
            {
                TimeInfo = new TimeInformation
                {
                    TimeOfDay = 14.0f,
                    NightTimeFactor = 0.0f,
                },
            },
        };
        var original = new NetworkStartAttackMission("map-event-1", initializer, "player-party-1");

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
        Assert.Equal("player-party-1", result.InitiatingPartyId);
        Assert.Equal("map-event-1", result.MapEventId);
        Assert.Equal(4242, result.RandomTerrainSeed);
        Assert.Equal("battle_terrain_030", result.MissionInitializer.SceneName);
        Assert.Equal(7, result.MissionInitializer.TerrainType);
        Assert.Equal(4242, result.MissionInitializer.RandomTerrainSeed);
        Assert.True(result.MissionInitializer.SceneHasMapPatch);
        Assert.Equal(0.25f, result.MissionInitializer.PatchCoordinates.X);
        Assert.Equal(0.75f, result.MissionInitializer.PatchCoordinates.Y);
        Assert.Equal(1f, result.MissionInitializer.PatchEncounterDir.X);
        Assert.Equal(0f, result.MissionInitializer.PatchEncounterDir.Y);
        Assert.Equal(14f, result.MissionInitializer.AtmosphereOnCampaign.TimeInfo.TimeOfDay);
    }
}
