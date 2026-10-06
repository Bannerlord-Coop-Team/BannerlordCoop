using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Messages;

public readonly struct ArtisanProductIssueOwnerChanged : IEvent
{
    public readonly ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue Issue;
    public readonly Hero PreviousOwner;

    public ArtisanProductIssueOwnerChanged(
        ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue issue, Hero previousOwner)
    {
        Issue = issue;
        PreviousOwner = previousOwner;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkArtisanProductIssueOwnerChanged : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string PreviousOwnerId;
    [ProtoMember(2)] public readonly int PreviousGeneration;
    [ProtoMember(3)] public readonly string OwnerId;
    [ProtoMember(4)] public readonly int Generation;
    [ProtoMember(5)] public readonly string ControllerId;

    public NetworkArtisanProductIssueOwnerChanged(string previousOwnerId, int previousGeneration,
        string ownerId, int generation, string controllerId)
    {
        PreviousOwnerId = previousOwnerId;
        PreviousGeneration = previousGeneration;
        OwnerId = ownerId;
        Generation = generation;
        ControllerId = controllerId;
    }
}
