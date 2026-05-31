using GameInterface.Surrogates;
using ProtoBuf;
using ProtoBuf.Meta;
using System.IO;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Serialization;

public class MissionInitializerRecordSurrogateTest
{
    public MissionInitializerRecordSurrogateTest()
    {
        // Registers all surrogates (including MissionInitializerRecord) with RuntimeTypeModel.
        // The lock inside SurrogateCollection makes repeated calls safe across tests.
        new SurrogateCollection();
    }

    private static MissionInitializerRecord CreateRecord() => new MissionInitializerRecord("test_battle_scene")
    {
        TerrainType = 3,
        DamageToFriendsMultiplier = 1.5f,
        DamageFromPlayerToFriendsMultiplier = 0.75f,
        NeedsRandomTerrain = true,
        RandomTerrainSeed = 99,
        SceneLevels = "level_1",
        PlayingInCampaignMode = true,
        EnableSceneRecording = false,
        SceneUpgradeLevel = 2,
        SceneHasMapPatch = true,
        PatchCoordinates = new Vec2(0.25f, 0.75f),
        PatchEncounterDir = new Vec2(1.0f, 0.0f),
        DoNotUseLoadingScreen = false,
        DisableDynamicPointlightShadows = true,
        DisableCorpseFadeOut = false,
        DecalAtlasGroup = 2,
        AtmosphereOnCampaign = new AtmosphereInfo
        {
            Seed = 54321u,
            AtmosphereName = "semi_arid_rainfall_2",
            InterpolatedAtmosphereName = "semi_arid_hot_0",
            SunInfo = new SunInformation
            {
                Altitude = 60f,
                Angle = 180f,
                Color = new Vec3(1f, 0.95f, 0.85f),
                Brightness = 1.2f,
                MaxBrightness = 2.0f,
                Size = 0.5f,
                RayStrength = 0.3f,
            },
            RainInfo = new RainInformation { Density = 0.05f },
            SnowInfo = new SnowInformation { Density = 0.0f },
            AmbientInfo = new AmbientInformation
            {
                EnvironmentMultiplier = 1.0f,
                AmbientColor = new Vec3(0.4f, 0.4f, 0.5f),
                MieScatterStrength = 0.1f,
                RayleighConstant = 0.05f,
            },
            FogInfo = new FogInformation
            {
                Density = 0.002f,
                Color = new Vec3(0.8f, 0.8f, 0.9f),
                Falloff = 2.0f,
            },
            SkyInfo = new SkyInformation { Brightness = 1.0f },
            NauticalInfo = new NauticalInformation
            {
                WaveStrength = 0.3f,
                WindVector = new Vec2(0.5f, 0.5f),
                CanUseLowAltitudeAtmosphere = 1,
                UseSceneWindDirection = 0,
                IsRiverBattle = 0,
                IsInsideStorm = 0,
                UsesNavalSimulatedWater = 0,
            },
            TimeInfo = new TimeInformation
            {
                TimeOfDay = 14.0f,
                NightTimeFactor = 0.0f,
                DrynessFactor = 0.8f,
                WinterTimeFactor = 0.0f,
                Season = 1,
            },
            AreaInfo = new AreaInformation { Temperature = 28f, Humidity = 0.3f },
            PostProInfo = new PostProcessInformation
            {
                MinExposure = 0.1f,
                MaxExposure = 4.0f,
                BrightpassThreshold = 0.9f,
                MiddleGray = 0.5f,
            },
        },
    };

    [Fact]
    public void RoundTrip_Serialize_Deserialize()
    {
        var original = CreateRecord();
        var surrogate = (MissionInitializerRecordSurrogate)original;

        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            RuntimeTypeModel.Default.Serialize(ms, surrogate);
            bytes = ms.ToArray();
        }

        Assert.NotEmpty(bytes);

        MissionInitializerRecordSurrogate deserialized;
        using (var ms = new MemoryStream(bytes))
        {
            deserialized = (MissionInitializerRecordSurrogate)RuntimeTypeModel.Default.Deserialize(ms, null, typeof(MissionInitializerRecordSurrogate));
        }

        MissionInitializerRecord result = deserialized;

        Assert.Equal(original.SceneName, result.SceneName);
        Assert.Equal(original.TerrainType, result.TerrainType);
        Assert.Equal(original.DamageToFriendsMultiplier, result.DamageToFriendsMultiplier);
        Assert.Equal(original.DamageFromPlayerToFriendsMultiplier, result.DamageFromPlayerToFriendsMultiplier);
        Assert.Equal(original.NeedsRandomTerrain, result.NeedsRandomTerrain);
        Assert.Equal(original.RandomTerrainSeed, result.RandomTerrainSeed);
        Assert.Equal(original.SceneLevels, result.SceneLevels);
        Assert.Equal(original.PlayingInCampaignMode, result.PlayingInCampaignMode);
        Assert.Equal(original.EnableSceneRecording, result.EnableSceneRecording);
        Assert.Equal(original.SceneUpgradeLevel, result.SceneUpgradeLevel);
        Assert.Equal(original.SceneHasMapPatch, result.SceneHasMapPatch);
        Assert.Equal(original.PatchCoordinates.X, result.PatchCoordinates.X);
        Assert.Equal(original.PatchCoordinates.Y, result.PatchCoordinates.Y);
        Assert.Equal(original.PatchEncounterDir.X, result.PatchEncounterDir.X);
        Assert.Equal(original.PatchEncounterDir.Y, result.PatchEncounterDir.Y);
        Assert.Equal(original.DoNotUseLoadingScreen, result.DoNotUseLoadingScreen);
        Assert.Equal(original.DisableDynamicPointlightShadows, result.DisableDynamicPointlightShadows);
        Assert.Equal(original.DisableCorpseFadeOut, result.DisableCorpseFadeOut);
        Assert.Equal(original.DecalAtlasGroup, result.DecalAtlasGroup);

        var origAtmo = original.AtmosphereOnCampaign;
        var resultAtmo = result.AtmosphereOnCampaign;
        Assert.Equal(origAtmo.Seed, resultAtmo.Seed);
        Assert.Equal(origAtmo.AtmosphereName, resultAtmo.AtmosphereName);
        Assert.Equal(origAtmo.InterpolatedAtmosphereName, resultAtmo.InterpolatedAtmosphereName);
        Assert.Equal(origAtmo.SunInfo.Altitude, resultAtmo.SunInfo.Altitude);
        Assert.Equal(origAtmo.SunInfo.Angle, resultAtmo.SunInfo.Angle);
        Assert.Equal(origAtmo.SunInfo.Color.x, resultAtmo.SunInfo.Color.x);
        Assert.Equal(origAtmo.SunInfo.Color.y, resultAtmo.SunInfo.Color.y);
        Assert.Equal(origAtmo.SunInfo.Color.z, resultAtmo.SunInfo.Color.z);
        Assert.Equal(origAtmo.SunInfo.Brightness, resultAtmo.SunInfo.Brightness);
        Assert.Equal(origAtmo.SunInfo.MaxBrightness, resultAtmo.SunInfo.MaxBrightness);
        Assert.Equal(origAtmo.SunInfo.Size, resultAtmo.SunInfo.Size);
        Assert.Equal(origAtmo.SunInfo.RayStrength, resultAtmo.SunInfo.RayStrength);
        Assert.Equal(origAtmo.RainInfo.Density, resultAtmo.RainInfo.Density);
        Assert.Equal(origAtmo.SnowInfo.Density, resultAtmo.SnowInfo.Density);
        Assert.Equal(origAtmo.AmbientInfo.EnvironmentMultiplier, resultAtmo.AmbientInfo.EnvironmentMultiplier);
        Assert.Equal(origAtmo.AmbientInfo.AmbientColor.x, resultAtmo.AmbientInfo.AmbientColor.x);
        Assert.Equal(origAtmo.AmbientInfo.AmbientColor.y, resultAtmo.AmbientInfo.AmbientColor.y);
        Assert.Equal(origAtmo.AmbientInfo.AmbientColor.z, resultAtmo.AmbientInfo.AmbientColor.z);
        Assert.Equal(origAtmo.AmbientInfo.MieScatterStrength, resultAtmo.AmbientInfo.MieScatterStrength);
        Assert.Equal(origAtmo.AmbientInfo.RayleighConstant, resultAtmo.AmbientInfo.RayleighConstant);
        Assert.Equal(origAtmo.FogInfo.Density, resultAtmo.FogInfo.Density);
        Assert.Equal(origAtmo.FogInfo.Color.x, resultAtmo.FogInfo.Color.x);
        Assert.Equal(origAtmo.FogInfo.Color.y, resultAtmo.FogInfo.Color.y);
        Assert.Equal(origAtmo.FogInfo.Color.z, resultAtmo.FogInfo.Color.z);
        Assert.Equal(origAtmo.FogInfo.Falloff, resultAtmo.FogInfo.Falloff);
        Assert.Equal(origAtmo.SkyInfo.Brightness, resultAtmo.SkyInfo.Brightness);
        Assert.Equal(origAtmo.NauticalInfo.WaveStrength, resultAtmo.NauticalInfo.WaveStrength);
        Assert.Equal(origAtmo.NauticalInfo.WindVector.X, resultAtmo.NauticalInfo.WindVector.X);
        Assert.Equal(origAtmo.NauticalInfo.WindVector.Y, resultAtmo.NauticalInfo.WindVector.Y);
        Assert.Equal(origAtmo.NauticalInfo.CanUseLowAltitudeAtmosphere, resultAtmo.NauticalInfo.CanUseLowAltitudeAtmosphere);
        Assert.Equal(origAtmo.NauticalInfo.IsRiverBattle, resultAtmo.NauticalInfo.IsRiverBattle);
        Assert.Equal(origAtmo.TimeInfo.TimeOfDay, resultAtmo.TimeInfo.TimeOfDay);
        Assert.Equal(origAtmo.TimeInfo.NightTimeFactor, resultAtmo.TimeInfo.NightTimeFactor);
        Assert.Equal(origAtmo.TimeInfo.DrynessFactor, resultAtmo.TimeInfo.DrynessFactor);
        Assert.Equal(origAtmo.TimeInfo.WinterTimeFactor, resultAtmo.TimeInfo.WinterTimeFactor);
        Assert.Equal(origAtmo.TimeInfo.Season, resultAtmo.TimeInfo.Season);
        Assert.Equal(origAtmo.AreaInfo.Temperature, resultAtmo.AreaInfo.Temperature);
        Assert.Equal(origAtmo.AreaInfo.Humidity, resultAtmo.AreaInfo.Humidity);
        Assert.Equal(origAtmo.PostProInfo.MinExposure, resultAtmo.PostProInfo.MinExposure);
        Assert.Equal(origAtmo.PostProInfo.MaxExposure, resultAtmo.PostProInfo.MaxExposure);
        Assert.Equal(origAtmo.PostProInfo.BrightpassThreshold, resultAtmo.PostProInfo.BrightpassThreshold);
        Assert.Equal(origAtmo.PostProInfo.MiddleGray, resultAtmo.PostProInfo.MiddleGray);
    }
}
