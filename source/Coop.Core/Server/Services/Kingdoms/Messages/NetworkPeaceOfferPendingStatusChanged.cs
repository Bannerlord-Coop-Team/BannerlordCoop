using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Kingdoms.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkPeaceOfferPendingStatusChanged : ICommand
{
    [ProtoMember(1)]
    public readonly string RequestingKingdomId;
    [ProtoMember(2)]
    public readonly string TargetKingdomId;
    [ProtoMember(3)]
    public readonly bool IsPending;

    public NetworkPeaceOfferPendingStatusChanged(string requestingKingdomId, string targetKingdomId, bool isPending)
    {
        RequestingKingdomId = requestingKingdomId;
        TargetKingdomId = targetKingdomId;
        IsPending = isPending;
    }
}