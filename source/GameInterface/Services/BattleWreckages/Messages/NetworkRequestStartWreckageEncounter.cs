using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.BattleWreckages.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestStartWreckageEncounter : ICommand
{
    [ProtoMember(1)]
    public readonly string BattleWreckageId;

    [ProtoMember(2)]
    public readonly string MainHeroId;

    public NetworkRequestStartWreckageEncounter(
        string battleWreckageId,
        string mainHeroId)
    {
        BattleWreckageId = battleWreckageId;
        MainHeroId = mainHeroId;
    }
}
