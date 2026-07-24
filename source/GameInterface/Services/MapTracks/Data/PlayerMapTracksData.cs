using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace GameInterface.Services.MapTracks.Data;

[ProtoContract(SkipConstructor = true)]
public readonly struct PlayerMapTracksData
{
    // Dictionary<playerPartyId, Track>
    [ProtoMember(1)]
    public readonly Dictionary<string, MBList<Track>> PlayerDetectedTracks;

    public PlayerMapTracksData(Dictionary<string, MBList<Track>> playerDetectedTracks)
    {
        PlayerDetectedTracks = playerDetectedTracks;
    }
}
