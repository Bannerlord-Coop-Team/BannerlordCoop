using Common.Messaging;
using GameInterface.Services.MapTracks.Data;

namespace GameInterface.Services.MapTracks.Messages;

public readonly struct UpdateClientsMapTrackData : IEvent
{
    public readonly PlayerMapTracksData PlayerMapTracksData;

    public UpdateClientsMapTrackData(PlayerMapTracksData playerMapTracksData)
    {
        PlayerMapTracksData = playerMapTracksData;
    }
}
