using ProtoBuf;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct MissionInitializerRecordSurrogate
{
    [ProtoMember(1)]  public int TerrainType { get; set; }
    [ProtoMember(2)]  public float DamageToFriendsMultiplier { get; set; }
    [ProtoMember(3)]  public float DamageFromPlayerToFriendsMultiplier { get; set; }
    [ProtoMember(4)]  public bool NeedsRandomTerrain { get; set; }
    [ProtoMember(5)]  public int RandomTerrainSeed { get; set; }
    [ProtoMember(6)]  public string SceneName { get; set; }
    [ProtoMember(7)]  public string SceneLevels { get; set; }
    [ProtoMember(8)]  public bool PlayingInCampaignMode { get; set; }
    [ProtoMember(9)]  public bool EnableSceneRecording { get; set; }
    [ProtoMember(10)] public int SceneUpgradeLevel { get; set; }
    [ProtoMember(11)] public bool SceneHasMapPatch { get; set; }
    [ProtoMember(12)] public Vec2Surrogate PatchCoordinates { get; set; }
    [ProtoMember(13)] public Vec2Surrogate PatchEncounterDir { get; set; }
    [ProtoMember(14)] public bool DoNotUseLoadingScreen { get; set; }
    [ProtoMember(15)] public bool DisableDynamicPointlightShadows { get; set; }
    [ProtoMember(16)] public bool DisableCorpseFadeOut { get; set; }
    [ProtoMember(17)] public int DecalAtlasGroup { get; set; }
    [ProtoMember(18)] public AtmosphereInfoSurrogate AtmosphereOnCampaign { get; set; }

    public MissionInitializerRecordSurrogate(MissionInitializerRecord r)
    {
        TerrainType = r.TerrainType;
        DamageToFriendsMultiplier = r.DamageToFriendsMultiplier;
        DamageFromPlayerToFriendsMultiplier = r.DamageFromPlayerToFriendsMultiplier;
        NeedsRandomTerrain = r.NeedsRandomTerrain;
        RandomTerrainSeed = r.RandomTerrainSeed;
        SceneName = r.SceneName;
        SceneLevels = r.SceneLevels;
        PlayingInCampaignMode = r.PlayingInCampaignMode;
        EnableSceneRecording = r.EnableSceneRecording;
        SceneUpgradeLevel = r.SceneUpgradeLevel;
        SceneHasMapPatch = r.SceneHasMapPatch;
        PatchCoordinates = r.PatchCoordinates;
        PatchEncounterDir = r.PatchEncounterDir;
        DoNotUseLoadingScreen = r.DoNotUseLoadingScreen;
        DisableDynamicPointlightShadows = r.DisableDynamicPointlightShadows;
        DisableCorpseFadeOut = r.DisableCorpseFadeOut;
        DecalAtlasGroup = r.DecalAtlasGroup;
        AtmosphereOnCampaign = r.AtmosphereOnCampaign;
    }

    public static implicit operator MissionInitializerRecordSurrogate(MissionInitializerRecord r) =>
        new MissionInitializerRecordSurrogate(r);

    public static implicit operator MissionInitializerRecord(MissionInitializerRecordSurrogate s) =>
        new MissionInitializerRecord(s.SceneName)
        {
            TerrainType = s.TerrainType,
            DamageToFriendsMultiplier = s.DamageToFriendsMultiplier,
            DamageFromPlayerToFriendsMultiplier = s.DamageFromPlayerToFriendsMultiplier,
            NeedsRandomTerrain = s.NeedsRandomTerrain,
            RandomTerrainSeed = s.RandomTerrainSeed,
            SceneLevels = s.SceneLevels,
            PlayingInCampaignMode = s.PlayingInCampaignMode,
            EnableSceneRecording = s.EnableSceneRecording,
            SceneUpgradeLevel = s.SceneUpgradeLevel,
            SceneHasMapPatch = s.SceneHasMapPatch,
            PatchCoordinates = s.PatchCoordinates,
            PatchEncounterDir = s.PatchEncounterDir,
            DoNotUseLoadingScreen = s.DoNotUseLoadingScreen,
            DisableDynamicPointlightShadows = s.DisableDynamicPointlightShadows,
            DisableCorpseFadeOut = s.DisableCorpseFadeOut,
            DecalAtlasGroup = s.DecalAtlasGroup,
            AtmosphereOnCampaign = s.AtmosphereOnCampaign,
        };
}
