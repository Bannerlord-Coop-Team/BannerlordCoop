using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapTracks.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkInitializePlayerTracksKeys : ICommand
{
    [ProtoMember(1)]
    public readonly string PlayerPartyId;

    public NetworkInitializePlayerTracksKeys(string playerPartyId)
    {
        PlayerPartyId = playerPartyId;
    }
}
