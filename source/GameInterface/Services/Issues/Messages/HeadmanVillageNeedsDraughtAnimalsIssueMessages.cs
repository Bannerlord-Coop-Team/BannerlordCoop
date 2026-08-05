using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Messages;

// --- Creation: server rolls once, broadcasts the resolved animal/amount/meat-offer terms ---

/// <summary>Published on the server after a genuine <c>IssueManager.CreateNewIssue</c> creates a
/// <see cref="HeadmanVillageNeedsDraughtAnimalsIssueBehavior.HeadmanVillageNeedsDraughtAnimalsIssue"/>, carrying
/// the live issue so <see cref="Handlers.HeadmanVillageNeedsDraughtAnimalsIssueHandler"/> can capture its three
/// creation-time-rolled fields.</summary>
public readonly struct HeadmanVillageNeedsDraughtAnimalsIssueCreated : IEvent
{
    public readonly HeadmanVillageNeedsDraughtAnimalsIssueBehavior.HeadmanVillageNeedsDraughtAnimalsIssue Issue;

    public HeadmanVillageNeedsDraughtAnimalsIssueCreated(HeadmanVillageNeedsDraughtAnimalsIssueBehavior.HeadmanVillageNeedsDraughtAnimalsIssue issue)
    {
        Issue = issue;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkHeadmanVillageNeedsDraughtAnimalsIssueCreated : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string SelectedAnimalId;
    [ProtoMember(3)]
    public readonly int RequestedAnimalAmount;
    [ProtoMember(4)]
    public readonly bool IsQuestWithMeatOffer;

    public NetworkHeadmanVillageNeedsDraughtAnimalsIssueCreated(string ownerId, string selectedAnimalId, int requestedAnimalAmount, bool isQuestWithMeatOffer)
    {
        OwnerId = ownerId;
        SelectedAnimalId = selectedAnimalId;
        RequestedAnimalAmount = requestedAnimalAmount;
        IsQuestWithMeatOffer = isQuestWithMeatOffer;
    }
}

// --- Acceptance: the accepting machine already applied it locally for real; the server arbitrates a
// same-issue double-accept race and confirms/rejects it, resolving and broadcasting the authoritative
// _discountValue every peer force-corrects to. ---

/// <summary>Published from <see cref="Patches.HeadmanVillageNeedsDraughtAnimalsAcceptancePatches"/> whenever
/// <c>IssueManager.StartIssueQuest</c> genuinely runs for this issue type. Carries the accepting machine's
/// captured <c>_discountValue</c> - see <see cref="Interfaces.IHeadmanVillageNeedsDraughtAnimalsIssueInterface"/>'s
/// doc comment.</summary>
public readonly struct HeadmanDraughtAnimalsIssueQuestAcceptTriggered : IEvent
{
    public readonly Hero Owner;
    public readonly string ControllerId;
    public readonly int DiscountValue;

    public HeadmanDraughtAnimalsIssueQuestAcceptTriggered(Hero owner, string controllerId, int discountValue)
    {
        Owner = owner;
        ControllerId = controllerId;
        DiscountValue = discountValue;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestHeadmanDraughtAnimalsIssueAcceptQuest : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public RequestHeadmanDraughtAnimalsIssueAcceptQuest(string ownerId)
    {
        OwnerId = ownerId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkHeadmanDraughtAnimalsIssueQuestAccepted : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string OwnerControllerId;
    [ProtoMember(3)]
    public readonly int DiscountValue;

    public NetworkHeadmanDraughtAnimalsIssueQuestAccepted(string ownerId, string ownerControllerId, int discountValue)
    {
        OwnerId = ownerId;
        OwnerControllerId = ownerControllerId;
        DiscountValue = discountValue;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkHeadmanDraughtAnimalsIssueAcceptRejected : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public NetworkHeadmanDraughtAnimalsIssueAcceptRejected(string ownerId)
    {
        OwnerId = ownerId;
    }
}
