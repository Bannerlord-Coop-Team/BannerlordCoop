using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.UI.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyRelationDecreasedByExecutionSummary : ICommand
{
    public readonly string KillerId;
    public readonly int NumberOfClans;

    public NetworkNotifyRelationDecreasedByExecutionSummary(
        string killerId,
        int numberOfClans)
    {
        KillerId = killerId;
        NumberOfClans = numberOfClans;
    }
}
