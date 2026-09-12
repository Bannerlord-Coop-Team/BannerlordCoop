using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.UI.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyRelationDecreasedByExecutionSummary : ICommand
{
    [ProtoMember(1)]
    public readonly string KillerId;

    [ProtoMember(2)]
    public readonly int NumberOfClans;

    public NetworkNotifyRelationDecreasedByExecutionSummary(
        string killerId,
        int numberOfClans)
    {
        KillerId = killerId;
        NumberOfClans = numberOfClans;
    }
}
