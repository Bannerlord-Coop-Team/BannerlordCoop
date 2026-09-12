using Common.Messaging;
using GameInterface.Services.MapTracks.Data;
using System.Collections.Generic;

namespace GameInterface.Services.MapTracks.Messages;

public readonly struct UpdateClientsMapTrackData : IEvent
{
    public readonly Dictionary<string, List<MapTrackData>> VisibleTrackChange;
    public readonly bool IsRemovingTracks;

    public UpdateClientsMapTrackData(
        Dictionary<string, List<MapTrackData>> visibleTrackChange,
        bool isRemovingTracks)
    {
        VisibleTrackChange = visibleTrackChange;
        IsRemovingTracks = isRemovingTracks;
    }
}
