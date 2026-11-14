using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkCaravanAmbushConversation : ICommand
{
    [ProtoMember(1)] public readonly string IssueOwnerId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly string ControllerId;

    public NetworkCaravanAmbushConversation(string ownerId, string issueId, string controllerId)
    {
        IssueOwnerId = ownerId;
        IssueId = issueId;
        ControllerId = controllerId;
    }
}
