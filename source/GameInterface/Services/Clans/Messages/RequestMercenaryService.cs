using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Clans.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct RequestMercenaryService : ICommand
{
    [ProtoMember(1)]
    public readonly string KingdomId;

    public RequestMercenaryService(string kingdomId)
    {
        KingdomId = kingdomId;
    }
}
