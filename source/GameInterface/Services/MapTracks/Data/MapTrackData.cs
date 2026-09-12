using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.MapTracks.Data;

[ProtoContract(SkipConstructor = true)]
public class MapTrackData
{
    [ProtoMember(1)]
    public readonly Track Track;

    [ProtoMember(2)]
    public readonly string MapFactionId;

    public MapTrackData(Track track, string mapFactionId)
    {
        Track = track;
        MapFactionId = mapFactionId;
    }
}
