using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Quests.GangLeaderNeedsToOffloadStolenGoods;

[ProtoContract(SkipConstructor = true)]
internal readonly struct RequestGangLeaderMerchantConversationEnded : ICommand
{
    [ProtoMember(1)] public readonly string IssueOwnerId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly string QuestId;

    public RequestGangLeaderMerchantConversationEnded(string issueOwnerId, string issueId, string questId)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        QuestId = questId;
    }
}
