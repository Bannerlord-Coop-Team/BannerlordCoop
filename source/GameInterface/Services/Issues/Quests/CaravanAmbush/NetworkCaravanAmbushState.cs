using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkCaravanAmbushState : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string IssueOwnerId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly string ControllerId;
    [ProtoMember(4)] public readonly string CaravanId;
    [ProtoMember(5)] public readonly string BanditId;
    [ProtoMember(6)] public readonly bool Saved;
    [ProtoMember(7)] public readonly int Warnings;
    [ProtoMember(8)] public readonly string[] Journal;

    public NetworkCaravanAmbushState(string ownerId, string issueId, string controllerId,
        string caravanId, string banditId, bool saved, int warnings, string[] journal)
    {
        IssueOwnerId = ownerId;
        IssueId = issueId;
        ControllerId = controllerId;
        CaravanId = caravanId;
        BanditId = banditId;
        Saved = saved;
        Warnings = warnings;
        Journal = journal;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkCaravanAmbushConversation : IServerToClientCommand
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
