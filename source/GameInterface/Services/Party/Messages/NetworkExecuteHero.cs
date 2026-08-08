using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Party.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkExecuteHero : ICommand
{
    [ProtoMember(1)]
    public readonly string ExecutedHeroId;

    [ProtoMember(2)]
    public readonly string ExecutorId;

    [ProtoMember(3)]
    public readonly KillCharacterAction.KillCharacterActionDetail Detail;

    [ProtoMember(4)]
    public readonly bool IsForced;

    public NetworkExecuteHero(
        string executedHeroId,
        string executorId,
        KillCharacterAction.KillCharacterActionDetail detail,
        bool isForced)
    {
        ExecutedHeroId = executedHeroId;
        ExecutorId = executorId;
        Detail = detail;
        IsForced = isForced;
    }
}