using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Party.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct ExecuteHero : ICommand
{
    [ProtoMember(1)]
    public readonly string ExecutedHeroId;

    [ProtoMember(2)]
    public readonly string ExecutorId;

    public ExecuteHero(
        string executedHeroId,
        string executorId)
    {
        ExecutedHeroId = executedHeroId;
        ExecutorId = executorId;
    }
}