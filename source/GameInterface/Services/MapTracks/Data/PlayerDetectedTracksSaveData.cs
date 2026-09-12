using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace GameInterface.Services.MapTracks.Data;

internal sealed class PlayerDetectedTracksSaveData
{
    [SaveableField(1)]
    internal string PlayerId;

    // HashSets are not saveable. Use a list to avoid added another new saveable type just for HashSets
    [SaveableField(2)]
    internal List<Track> DetectedTracks;

    private PlayerDetectedTracksSaveData()
    {
    }

    internal PlayerDetectedTracksSaveData(string playerId, List<Track> detectedTracks)
    {
        PlayerId = playerId;
        DetectedTracks = detectedTracks;
    }
}

public sealed class DetectedTracksSaveableTypeDefiner : SaveableTypeDefiner
{
    private const int SaveBaseId = 44_186_001;

    public DetectedTracksSaveableTypeDefiner() : base(SaveBaseId)
    {
    }

    public override void DefineClassTypes()
    {
        AddClassDefinition(typeof(PlayerDetectedTracksSaveData), 1);
    }

    public override void DefineContainerDefinitions()
    {
        ConstructContainerDefinition(typeof(List<PlayerDetectedTracksSaveData>));
    }
}
