using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.BattleWreckages.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestSelectWreckageConsequences : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    public NetworkRequestSelectWreckageConsequences(string mainHeroId)
    {
        MainHeroId = mainHeroId;
    }
}
