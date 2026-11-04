using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Quests.EscortMerchantCaravan;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkEscortProgress : ICommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly string ControllerId;
    [ProtoMember(4)] public readonly string CaravanId;
    [ProtoMember(5)] public readonly string BanditId;
    [ProtoMember(6)] public readonly string OtherBanditId;
    [ProtoMember(7)] public readonly string[] VisitedIds;
    [ProtoMember(8)] public readonly int Days;
    [ProtoMember(9)] public readonly bool Warned;
    [ProtoMember(10)] public readonly byte[] Journal;
    [ProtoMember(11)] public readonly string[] TrackedParties;
    [ProtoMember(12)] public readonly string[] TrackedSettlements;
    [ProtoMember(13)] public readonly bool Alternative;
    [ProtoMember(14)] public readonly byte[] Notice;
    [ProtoMember(15)] public readonly int ExtraTime;
    [ProtoMember(16)] public readonly bool FollowCaravan;
    [ProtoMember(17)] public readonly long DueTimeTicks;

    public NetworkEscortProgress(string giverId = null,
        string issueId = null,
        string controllerId = null,
        string caravanId = null,
        string banditId = null,
        string otherBanditId = null,
        string[] visitedIds = null,
        int days = 0,
        bool warned = false,
        byte[] journal = null,
        string[] trackedParties = null,
        string[] trackedSettlements = null,
        bool alternative = false,
        byte[] notice = null,
        int extraTime = 0,
        bool followCaravan = false,
        long dueTimeTicks = 0)
    {
        GiverId = giverId;
        IssueId = issueId;
        ControllerId = controllerId;
        CaravanId = caravanId;
        BanditId = banditId;
        OtherBanditId = otherBanditId;
        VisitedIds = visitedIds;
        Days = days;
        Warned = warned;
        Journal = journal;
        TrackedParties = trackedParties;
        TrackedSettlements = trackedSettlements;
        Alternative = alternative;
        Notice = notice;
        ExtraTime = extraTime;
        FollowCaravan = followCaravan;
        DueTimeTicks = dueTimeTicks;
    }
}
