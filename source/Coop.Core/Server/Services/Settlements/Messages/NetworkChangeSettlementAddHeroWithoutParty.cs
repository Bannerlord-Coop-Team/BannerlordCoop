using Common.Logging.Attributes;
using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Settlements.Messages;


/// <summary>
/// Notifies clients of settlement addherowithout party change
/// </summary>
[ProtoContract(SkipConstructor = true)]
[BatchLogMessage]
public record NetworkChangeSettlementAddHeroWithoutParty : IEvent
{
    [ProtoMember(1)]
    public string SettlementId { get; }
    [ProtoMember(2)]
    public string HeroId { get; }

    public NetworkChangeSettlementAddHeroWithoutParty(string settlementId, string heroId)
    {
        SettlementId = settlementId;
        HeroId = heroId;
    }
}
