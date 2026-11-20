using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Quests.ExtortionByDeserters.Messages;

internal enum ExtortionByDesertersNotice : byte
{
    None = 0,
    DesertersNoticedPlayer = 1,
    PlayerLeftVillage = 2,
    Ambush = 4,
}

/// <summary>Owner-only quest progress, sent after the referenced world objects.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkExtortionByDesertersState : ICommand
{
    [ProtoMember(1)] public readonly string IssueOwnerId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly string ControllerId;
    [ProtoMember(4)] public readonly string DeserterId;
    [ProtoMember(5)] public readonly string DefenderId;
    [ProtoMember(6)] public readonly int State;
    [ProtoMember(7)] public readonly TextObject Log;
    [ProtoMember(8)] public readonly byte Notice;

    public NetworkExtortionByDesertersState(string issueOwnerId, string issueId, string controllerId,
        string deserterId, string defenderId, int state, TextObject log, byte notice = 0)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        ControllerId = controllerId;
        DeserterId = deserterId;
        DefenderId = defenderId;
        State = state;
        Log = log;
        Notice = notice;
    }
}
