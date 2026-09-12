using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Alliances.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkCallToWarOfferDenied : ICommand
{
    [ProtoMember(1)]
    public readonly string CallingKingdomId;
    [ProtoMember(2)]
    public readonly string CalledKingdomId;

    public NetworkCallToWarOfferDenied(string callingKingdomId, string calledKingdomId)
    {
        CallingKingdomId = callingKingdomId;
        CalledKingdomId = calledKingdomId;
    }
}