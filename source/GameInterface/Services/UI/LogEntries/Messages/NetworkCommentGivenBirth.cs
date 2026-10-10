using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.UI.LogEntries.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkCommentGivenBirth : ICommand
{
    [ProtoMember(1)]
    public readonly string MotherId;

    [ProtoMember(2)]
    public readonly List<string> AliveChildrenIds;

    [ProtoMember(3)]
    public readonly int StillbornCount;

    public NetworkCommentGivenBirth(
        string motherId,
        List<string> aliveChildrenIds,
        int stillbornCount)
    {
        MotherId = motherId;
        AliveChildrenIds = aliveChildrenIds;
        StillbornCount = stillbornCount;
    }
}
