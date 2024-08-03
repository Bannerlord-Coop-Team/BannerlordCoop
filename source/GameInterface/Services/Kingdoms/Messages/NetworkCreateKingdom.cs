using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Kingdoms.Messages;

[ProtoContract(SkipConstructor = true)]
internal class NetworkCreateKingdom : ICommand
{
    [ProtoMember(1)]
    public string KindgomId { get; }

    public NetworkCreateKingdom(string kingdomId)
    {
        KindgomId = kingdomId;
    }
}