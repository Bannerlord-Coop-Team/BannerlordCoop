using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Settlements.Messages;

/// <summary>
/// Notify clients to Change CurrentSeigeState
/// </summary>

[ProtoContract(SkipConstructor = true)]
public record NetworkChangeSettlementCurrentSiegeState : IEvent
{
    [ProtoMember(1)]
    public string SettlementId { get; }
    [ProtoMember(2)]
    public short CurrentSiegeState { get; }

    public NetworkChangeSettlementCurrentSiegeState(string settlementId, short currentSiegeState)
    {
        SettlementId = settlementId;
        CurrentSiegeState = currentSiegeState;
    }
}

