using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.BattleWreckages.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestAcceptRecoverTroops : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    public NetworkRequestAcceptRecoverTroops(string mainHeroId)
    {
        MainHeroId = mainHeroId;
    }
}
