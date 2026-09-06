using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace GameInterface.Services.MapTracks.Data;

internal sealed class MapTracksSourcePartySaveData
{
    [SaveableField(1)]
    internal Track Track;

    [SaveableField(2)]
    internal string SourcePartyId;

    private MapTracksSourcePartySaveData()
    {
    }

    internal MapTracksSourcePartySaveData(Track track, string sourcePartyId)
    {
        Track = track;
        SourcePartyId = sourcePartyId;
    }
}

public sealed class MapTracksSourcePartySaveableTypeDefiner : SaveableTypeDefiner
{
    private const int SaveBaseId = 44_186_002;

    public MapTracksSourcePartySaveableTypeDefiner() : base(SaveBaseId)
    {
    }

    public override void DefineClassTypes()
    {
        AddClassDefinition(typeof(MapTracksSourcePartySaveData), 1);
    }

    public override void DefineContainerDefinitions()
    {
        ConstructContainerDefinition(typeof(List<MapTracksSourcePartySaveData>));
    }
}
