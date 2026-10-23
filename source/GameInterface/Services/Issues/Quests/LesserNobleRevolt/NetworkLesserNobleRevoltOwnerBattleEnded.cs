using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Quests.LesserNobleRevolt;

/// <summary>
/// Tells the owner's client that the battle it started against the revolt party ended, which
/// vanilla's next talk reads from the client's own quest.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkLesserNobleRevoltOwnerBattleEnded : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string IssueOwnerId;

    [ProtoMember(2)]
    public readonly string IssueId;

    [ProtoMember(3)]
    public readonly string ControllerId;

    public NetworkLesserNobleRevoltOwnerBattleEnded(string issueOwnerId, string issueId, string controllerId)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        ControllerId = controllerId;
    }
}
