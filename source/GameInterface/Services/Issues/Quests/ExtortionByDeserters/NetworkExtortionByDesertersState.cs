using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Quests.ExtortionByDeserters;

/// <summary>Owner-only quest progress, sent after the referenced world objects.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkExtortionByDesertersState : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string IssueOwnerId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly string ControllerId;
    [ProtoMember(4)] public readonly string DeserterId;
    [ProtoMember(5)] public readonly string DefenderId;
    [ProtoMember(6)] public readonly int State;
    [ProtoMember(7)] public readonly string Log;
    [ProtoMember(8)] public readonly bool Ambush;

    public NetworkExtortionByDesertersState(string issueOwnerId, string issueId, string controllerId,
        string deserterId, string defenderId, int state, string log, bool ambush)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        ControllerId = controllerId;
        DeserterId = deserterId;
        DefenderId = defenderId;
        State = state;
        Log = log;
        Ambush = ambush;
    }
}
