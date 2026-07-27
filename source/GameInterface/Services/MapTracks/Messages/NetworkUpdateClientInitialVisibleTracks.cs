using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.MapTracks.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkUpdateClientInitialVisibleTracks : ICommand
{
    [ProtoMember(1)]
    public readonly List<Track> VisibleTrackChanges;

    public NetworkUpdateClientInitialVisibleTracks(List<Track> visibleTrackChanges)
    {
        VisibleTrackChanges = visibleTrackChanges;
    }
}
