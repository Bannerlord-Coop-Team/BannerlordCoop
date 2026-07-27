using Common.Messaging;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.MapTracks.Messages;

public readonly struct UpdateClientsMapTrackData : IEvent
{
    public readonly Dictionary<string, List<Track>> VisibleTrackChange;
    public readonly bool IsRemovingTracks;

    public UpdateClientsMapTrackData(
        Dictionary<string, List<Track>> visibleTrackChange,
        bool isRemovingTracks)
    {
        VisibleTrackChange = visibleTrackChange;
        IsRemovingTracks = isRemovingTracks;
    }
}
