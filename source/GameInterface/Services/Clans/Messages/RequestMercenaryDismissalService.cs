using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Clans.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct RequestMercenaryDismissalService : ICommand
{
    [ProtoMember(1)]
    public readonly string KingdomId;

    public RequestMercenaryDismissalService(string kingdomId)
    {
        KingdomId = kingdomId;
    }
}
