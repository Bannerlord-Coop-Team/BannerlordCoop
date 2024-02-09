using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;

namespace Coop.Core.Server.Services.Settlements.Messages;


/// <summary>
/// Message used to inform client of notable cache change
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkChangeSettlementNotablesCache : IEvent
{
    [ProtoMember(1)]
    public string SettlementId { get; }
    [ProtoMember(2)]
    public List<string> NotablesCache { get; }

    public NetworkChangeSettlementNotablesCache(string settlementId, List<string> notablesCache)
    {
        SettlementId = settlementId;
        NotablesCache = notablesCache;
    }
}
