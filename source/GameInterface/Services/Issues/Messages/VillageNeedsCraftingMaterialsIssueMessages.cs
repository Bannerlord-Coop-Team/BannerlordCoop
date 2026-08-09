using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Messages;

public readonly struct VillageCraftingIssueCreated : IEvent
{
    public readonly VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue Issue;

    public VillageCraftingIssueCreated(VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue issue)
    {
        Issue = issue;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkVillageCraftingIssueCreated : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string RequestedItemId;
    [ProtoMember(3)]
    public readonly int Generation;

    public NetworkVillageCraftingIssueCreated(string ownerId, string requestedItemId, int generation)
    {
        OwnerId = ownerId;
        RequestedItemId = requestedItemId;
        Generation = generation;
    }
}

public readonly struct VillageCraftingIssueAlternativeSolutionCompletionRequested : IEvent
{
    public readonly Hero Owner;

    public VillageCraftingIssueAlternativeSolutionCompletionRequested(Hero owner)
    {
        Owner = owner;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestVillageCraftingIssueAlternativeSolutionCompletion : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public RequestVillageCraftingIssueAlternativeSolutionCompletion(string ownerId)
    {
        OwnerId = ownerId;
    }
}
