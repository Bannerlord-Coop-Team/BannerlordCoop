using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.MapTracks.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkUpdateClientsMapTrackData : ICommand
{
    [ProtoMember(1)]
    public readonly Dictionary<string, List<MapTrackData>> VisibleTrackChange;

    [ProtoMember(2)]
    public readonly bool IsRemovingTracks;

    public NetworkUpdateClientsMapTrackData(
        Dictionary<string, List<MapTrackData>> visibleTrackChange,
        bool isRemovingTracks)
    {
        VisibleTrackChange = visibleTrackChange;
        IsRemovingTracks = isRemovingTracks;
    }
}
