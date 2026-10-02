using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Messages;

public readonly struct VillageCraftingProgressChanged : IEvent
{
    public readonly VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest Quest;

    public VillageCraftingProgressChanged(VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest quest)
    {
        Quest = quest;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkVillageCraftingProgressChanged : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly int Generation;
    [ProtoMember(3)]
    public readonly int Progress;

    public NetworkVillageCraftingProgressChanged(string ownerId, int generation, int progress)
    {
        OwnerId = ownerId;
        Generation = generation;
        Progress = progress;
    }
}

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
