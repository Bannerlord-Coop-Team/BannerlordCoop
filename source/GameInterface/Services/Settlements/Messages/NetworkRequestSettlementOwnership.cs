using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// Raised locally on a client when its player gifts a settlement, so the handler can forward it.
/// </summary>
internal readonly struct SettlementGiftRequested : IEvent
{
    public readonly Settlement Settlement;
    public readonly Hero NewOwner;

    public SettlementGiftRequested(Settlement settlement, Hero newOwner)
    {
        Settlement = settlement;
        NewOwner = newOwner;
    }
}

/// <summary>
/// A client asking the server to hand a settlement to a new owner - the kingdom screen's
/// "Give Settlement" flow.
/// </summary>
/// <remarks>
/// Client-initiated ownership changes are blocked locally (ChangeOwnerOfSettlementPatch), so without
/// this request a client ruler's gift silently did nothing: the popup closed and no state moved.
/// The server re-validates authority; nothing here is trusted beyond the two ids.
/// </remarks>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkRequestSettlementOwnership : ICommand
{
    [ProtoMember(1)]
    public readonly string SettlementId;

    [ProtoMember(2)]
    public readonly string NewOwnerId;

    public NetworkRequestSettlementOwnership(string settlementId, string newOwnerId)
    {
        SettlementId = settlementId;
        NewOwnerId = newOwnerId;
    }
}
