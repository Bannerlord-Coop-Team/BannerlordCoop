using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Arenas.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkAddMetArenaMasterAndKnowTournaments : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string CurrentSettlementId;

    public NetworkAddMetArenaMasterAndKnowTournaments(
        string mainHeroId,
        string currentSettlementId)
    {
        MainHeroId = mainHeroId;
        CurrentSettlementId = currentSettlementId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkAddMetArenaMaster : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string CurrentSettlementId;

    public NetworkAddMetArenaMaster(
        string mainHeroId,
        string currentSettlementId)
    {
        MainHeroId = mainHeroId;
        CurrentSettlementId = currentSettlementId;
    }
}