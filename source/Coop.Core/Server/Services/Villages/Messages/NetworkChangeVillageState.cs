using Common.Logging.Attributes;
using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Villages.Messages;

/// <summary>
/// Server sends this data when a Village Changes State
/// </summary>
[ProtoContract(SkipConstructor = true)]
[BatchLogMessage]
public record NetworkChangeVillageState : IEvent
{
    [ProtoMember(1)]
    public string SettlementId { get; }
    [ProtoMember(2)]
    public int State { get; }

    public NetworkChangeVillageState(string settlementId, int state)
    {
        SettlementId = settlementId;
        State = state;
    }
}
