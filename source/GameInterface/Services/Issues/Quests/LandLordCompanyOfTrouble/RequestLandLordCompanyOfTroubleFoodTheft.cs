using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Quests.LandLordCompanyOfTrouble;

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestLandLordCompanyOfTroubleFoodTheft : ICommand
{
    [ProtoMember(1)]
    public readonly string IssueOwnerId;

    [ProtoMember(2)]
    public readonly string IssueId;

    [ProtoMember(3)]
    public readonly string ItemId;

    public RequestLandLordCompanyOfTroubleFoodTheft(string issueOwnerId, string issueId, string itemId)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        ItemId = itemId;
    }
}
