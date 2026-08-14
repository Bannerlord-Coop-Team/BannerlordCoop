using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Kingdoms.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct KingdomUnresolvedDecision : ICommand
{
    [ProtoMember(1)]
    public readonly string PlayerKingdomId;
    [ProtoMember(2)]
    public readonly string TargetKingdomId;

    public KingdomUnresolvedDecision(string playerKingdomId, string targetKingdomId)
    {
        PlayerKingdomId = playerKingdomId;
        TargetKingdomId = targetKingdomId;
    }
}
