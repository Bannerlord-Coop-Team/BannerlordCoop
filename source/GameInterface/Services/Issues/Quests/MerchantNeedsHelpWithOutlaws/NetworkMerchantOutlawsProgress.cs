using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Quests.MerchantNeedsHelpWithOutlaws;

[ProtoContract(SkipConstructor = true)]
internal class NetworkMerchantOutlawsProgress : IMessage
{
    [ProtoMember(1)] public string GiverId { get; }
    [ProtoMember(2)] public string IssueId { get; }
    [ProtoMember(3)] public string ControllerId { get; }
    [ProtoMember(4)] public int Destroyed { get; }
    [ProtoMember(5)] public int Recruited { get; }
    [ProtoMember(6)] public string[] PartyIds { get; }
    [ProtoMember(7)] public string FinalLog { get; }
    [ProtoMember(8)] public string[] AlternativeLogs { get; }
    [ProtoMember(9)] public int AlternativeProgress { get; }
    [ProtoMember(10)] public int AlternativeRange { get; }

    public NetworkMerchantOutlawsProgress(string giverId, string issueId, string controllerId,
        int destroyed, int recruited, string[] partyIds, string finalLog,
        string[] alternativeLogs = null, int alternativeProgress = 0, int alternativeRange = 0)
    {
        GiverId = giverId;
        IssueId = issueId;
        ControllerId = controllerId;
        Destroyed = destroyed;
        Recruited = recruited;
        PartyIds = partyIds;
        FinalLog = finalLog;
        AlternativeLogs = alternativeLogs;
        AlternativeProgress = alternativeProgress;
        AlternativeRange = alternativeRange;
    }
}
