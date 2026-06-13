using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Settlements.Messages;


/// <summary>
/// Notifies clients of settlement addherowithout party change
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkChangeSettlementAddHeroWithoutParty : IEvent
{
    [ProtoMember(1)]
    public readonly string SettlementId;
    [ProtoMember(2)]
    public readonly string HeroId;

    public NetworkChangeSettlementAddHeroWithoutParty(string settlementId, string heroId)
    {
        SettlementId = settlementId;
        HeroId = heroId;
    }
}
