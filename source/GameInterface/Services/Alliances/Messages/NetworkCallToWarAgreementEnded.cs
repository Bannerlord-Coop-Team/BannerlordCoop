using Common.Messaging;
using ProtoBuf;
namespace GameInterface.Services.Alliances.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkCallToWarAgreementEnded : ICommand
{
    [ProtoMember(1)]
    public readonly string CallingKingdomId;
    [ProtoMember(2)]
    public readonly string CalledKingdomId;
    [ProtoMember(3)]
    public readonly string KingdomToCallToWarAgainstId;

    public NetworkCallToWarAgreementEnded(string callingKingdomId, string calledKingdomId, string kingdomToCallToWarAgainstId)
    {
        CallingKingdomId = callingKingdomId;
        CalledKingdomId = calledKingdomId;
        KingdomToCallToWarAgainstId = kingdomToCallToWarAgainstId;
    }
}
