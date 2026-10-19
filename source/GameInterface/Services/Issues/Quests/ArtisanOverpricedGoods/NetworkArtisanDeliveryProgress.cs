using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Quests.ArtisanOverpricedGoods;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkArtisanDeliveryProgress : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string IssueOwnerId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly int Delivered;

    public NetworkArtisanDeliveryProgress(string issueOwnerId, string issueId, int delivered)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        Delivered = delivered;
    }
}
