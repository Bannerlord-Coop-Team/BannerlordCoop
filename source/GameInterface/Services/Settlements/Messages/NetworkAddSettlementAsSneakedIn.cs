using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Settlements.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkAddSettlementAsSneakedIn : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string CurrentSettlementId;

    public NetworkAddSettlementAsSneakedIn(
        string mainHeroId,
        string currentSettlementId)
    {
        MainHeroId = mainHeroId;
        CurrentSettlementId = currentSettlementId;
    }
}
