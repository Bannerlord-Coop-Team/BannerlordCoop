using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.MapTracks.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkUpdateClientInitialVisibleTracks : ICommand
{
    [ProtoMember(1)]
    public readonly List<MapTrackData> VisibleTrackChanges;

    public NetworkUpdateClientInitialVisibleTracks(List<MapTrackData> visibleTrackChanges)
    {
        VisibleTrackChanges = visibleTrackChanges;
    }
}
