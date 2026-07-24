using Common.Messaging;
using GameInterface.Services.MapTracks.Data;
using ProtoBuf;

namespace GameInterface.Services.MapTracks.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkUpdateClientsMapTrackData : ICommand
{
    [ProtoMember(1)]
    public readonly PlayerMapTracksData PlayerMapTracksData;

    public NetworkUpdateClientsMapTrackData(PlayerMapTracksData playerMapTracksData)
    {
        PlayerMapTracksData = playerMapTracksData;
    }
}
