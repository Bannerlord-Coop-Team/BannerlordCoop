using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Quests.LesserNobleRevolt;

/// <summary>
/// Tells the owner's client that the revolt party made its <see cref="VisitCount"/>-th settlement
/// visit, or that the location notification for that visit is due.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkLesserNobleRevoltJournalUpdate : ICommand
{
    [ProtoMember(1)]
    public readonly string IssueOwnerId;

    [ProtoMember(2)]
    public readonly string IssueId;

    [ProtoMember(3)]
    public readonly string ControllerId;

    [ProtoMember(4)]
    public readonly string SettlementId;

    [ProtoMember(5)]
    public readonly int VisitCount;

    [ProtoMember(6)]
    public readonly bool Notification;

    public NetworkLesserNobleRevoltJournalUpdate(
        string issueOwnerId,
        string issueId,
        string controllerId,
        string settlementId,
        int visitCount,
        bool notification)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        ControllerId = controllerId;
        SettlementId = settlementId;
        VisitCount = visitCount;
        Notification = notification;
    }
}
