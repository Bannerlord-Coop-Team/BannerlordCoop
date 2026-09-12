using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Kingdoms.Messages;

[ProtoContract(SkipConstructor = true )]
public readonly struct NetworkDestroyKingdom : ICommand
{
    [ProtoMember(1)]
    public readonly string KingdomId;

    public NetworkDestroyKingdom(string kingdomId)
    {
        KingdomId = kingdomId;
    }
}
