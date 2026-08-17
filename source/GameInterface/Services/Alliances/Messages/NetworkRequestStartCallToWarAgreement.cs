using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Alliances.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkRequestStartCallToWarAgreement : ICommand
{
    [ProtoMember(1)]
    public readonly string CallingKingdomId;
    [ProtoMember(2)]
    public readonly string CalledKingdomId;
    [ProtoMember(3)]
    public readonly string KingdomToCallToWarAgainstId;
    [ProtoMember(4)]
    public readonly string PlayerId;
    [ProtoMember(5)]
    public readonly bool IsPlayerPaying;

    public NetworkRequestStartCallToWarAgreement(string callingKingdomId, string calledKingdomId, string kingdomToCallToWarAgainstId, string playerId, bool isPlayerPaying)
    {
        CallingKingdomId = callingKingdomId;
        CalledKingdomId = calledKingdomId;
        KingdomToCallToWarAgainstId = kingdomToCallToWarAgainstId;
        PlayerId = playerId;
        IsPlayerPaying = isPlayerPaying;
    }
}