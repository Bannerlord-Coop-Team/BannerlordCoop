using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.BattleWreckages.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestApplyWreckageInvestigationResults : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    public NetworkRequestApplyWreckageInvestigationResults(string mainHeroId)
    {
        MainHeroId = mainHeroId;
    }
}
