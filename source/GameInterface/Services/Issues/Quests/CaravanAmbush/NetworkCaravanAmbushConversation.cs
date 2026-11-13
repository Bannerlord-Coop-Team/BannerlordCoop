using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkCaravanAmbushConversation : IMessage
{
    [ProtoMember(1)] public readonly string IssueOwnerId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly string ControllerId;
    [ProtoMember(4)] public readonly string CaravanId;

    public NetworkCaravanAmbushConversation(string ownerId, string issueId, string controllerId, string caravanId)
    {
        IssueOwnerId = ownerId;
        IssueId = issueId;
        ControllerId = controllerId;
        CaravanId = caravanId;
    }
}
