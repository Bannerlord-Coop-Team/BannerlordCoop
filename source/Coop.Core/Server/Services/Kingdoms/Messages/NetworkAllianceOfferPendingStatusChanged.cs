using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Kingdoms.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkAllianceOfferPendingStatusChanged : ICommand
{
    [ProtoMember(1)]
    public readonly string RequestingKingdomId;
    [ProtoMember(2)]
    public readonly string TargetKingdomId;
    [ProtoMember(3)]
    public readonly bool IsPending;
    [ProtoMember(4)]
    public readonly string RequestingKingdomStringId;
    [ProtoMember(5)]
    public readonly string TargetKingdomStringId;

    public NetworkAllianceOfferPendingStatusChanged(
        string requestingKingdomId,
        string targetKingdomId,
        bool isPending,
        string requestingKingdomStringId = null,
        string targetKingdomStringId = null)
    {
        RequestingKingdomId = requestingKingdomId;
        TargetKingdomId = targetKingdomId;
        IsPending = isPending;
        RequestingKingdomStringId = requestingKingdomStringId;
        TargetKingdomStringId = targetKingdomStringId;
    }
}
