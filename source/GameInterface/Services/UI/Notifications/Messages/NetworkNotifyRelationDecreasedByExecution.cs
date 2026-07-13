using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.UI.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyRelationDecreasedByExecution : ICommand
{
    public readonly string KillerId;
    public readonly string ClanId;
    public readonly int Value;
    public readonly int RelationChange;

    public NetworkNotifyRelationDecreasedByExecution(
        string killerId,
        string clanId,
        int value,
        int relationChange)
    {
        KillerId = killerId;
        ClanId = clanId;
        Value = value;
        RelationChange = relationChange;
    }
}
