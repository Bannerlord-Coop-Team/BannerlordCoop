using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace GameInterface.Services.MapTracks.Data;

internal sealed class TrackMapFactionSaveData
{
    [SaveableField(1)]
    internal Track Track;

    [SaveableField(2)]
    internal IFaction MapFaction;

    private TrackMapFactionSaveData()
    {
    }

    internal TrackMapFactionSaveData(Track track, IFaction mapFaction)
    {
        Track = track;
        MapFaction = mapFaction;
    }
}

public sealed class MapTracksSaveableTypeDefiner : SaveableTypeDefiner
{
    private const int SaveBaseId = 44_186_000;

    public MapTracksSaveableTypeDefiner() : base(SaveBaseId)
    {
    }

    public override void DefineClassTypes()
    {
        AddClassDefinition(typeof(TrackMapFactionSaveData), 1);
    }

    public override void DefineContainerDefinitions()
    {
        ConstructContainerDefinition(typeof(List<TrackMapFactionSaveData>));
    }
}
