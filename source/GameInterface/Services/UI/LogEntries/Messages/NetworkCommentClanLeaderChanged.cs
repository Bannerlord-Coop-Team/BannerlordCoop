using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.UI.LogEntries.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkCommentClanLeaderChanged : ICommand
{
    [ProtoMember(1)]
    public readonly string OldLeaderId;

    [ProtoMember(2)]
    public readonly string NewLeaderId;

    public NetworkCommentClanLeaderChanged(string oldLeaderId, string newLeaderId)
    {
        OldLeaderId = oldLeaderId;
        NewLeaderId = newLeaderId;
    }
}
