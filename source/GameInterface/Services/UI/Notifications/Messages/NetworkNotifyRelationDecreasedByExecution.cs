using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.UI.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyRelationDecreasedByExecution : ICommand
{
    [ProtoMember(1)]
    public readonly string KillerId;

    [ProtoMember(2)]
    public readonly string ClanId;

    [ProtoMember(3)]
    public readonly int Value;

    [ProtoMember(4)]
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
