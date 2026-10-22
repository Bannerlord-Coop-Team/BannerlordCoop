using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Quests.LesserNobleRevolt;

/// <summary>
/// Gives the owner's client the server quest's journal, in order, before the server announces the ending,
/// because the owner's client replays a generic ending that never runs the branch's <c>AddLog</c>.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkLesserNobleRevoltEndingLog : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string IssueOwnerId;

    [ProtoMember(2)]
    public readonly string IssueId;

    [ProtoMember(3)]
    public readonly string ControllerId;

    [ProtoMember(4)]
    public readonly TextObject[] Logs;

    public NetworkLesserNobleRevoltEndingLog(string issueOwnerId, string issueId, string controllerId, TextObject[] logs)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        ControllerId = controllerId;
        Logs = logs;
    }
}
