using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Kingdoms.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct ClientKingdomUnresolvedDecision : ICommand
{
    [ProtoMember(1)]
    public readonly UnresolvedDecisionResult Result;

    public ClientKingdomUnresolvedDecision(UnresolvedDecisionResult result)
    {
        Result = result;
    }
}
public enum UnresolvedDecisionResult
{
    Waiting,
    NoPeaceOffer,
    HasPeaceOffer
}