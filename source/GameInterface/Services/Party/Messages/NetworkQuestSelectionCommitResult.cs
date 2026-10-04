using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Party.Messages;

[ProtoContract]
internal readonly struct NetworkQuestSelectionCommitResult : IEvent
{
    [ProtoMember(1)]
    public readonly string CommitId;

    [ProtoMember(2)]
    public readonly bool Accepted;

    public NetworkQuestSelectionCommitResult(string commitId, bool accepted)
    {
        CommitId = commitId;
        Accepted = accepted;
    }
}
