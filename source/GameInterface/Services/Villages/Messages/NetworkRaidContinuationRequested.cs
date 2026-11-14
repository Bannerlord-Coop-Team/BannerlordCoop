using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Villages.Messages;

/// <summary>Ask the raid leader to request the next raid after closing its battle results.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkRaidContinuationRequested : ICommand
{
    [ProtoMember(1)]
    public readonly string SettlementId;

    public NetworkRaidContinuationRequested(string settlementId)
    {
        SettlementId = settlementId;
    }
}
