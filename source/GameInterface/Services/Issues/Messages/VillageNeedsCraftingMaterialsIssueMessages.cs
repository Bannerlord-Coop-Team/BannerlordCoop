using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Messages;

// --- Local events ---

/// <summary>
/// Published on the server (from <see cref="Patches.VillageNeedsCraftingMaterialsIssueCreationPatch"/>)
/// immediately after <c>IssueManager.CreateNewIssue</c> creates a new
/// <see cref="VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue"/> for real. Carries
/// the live issue so <see cref="Handlers.VillageNeedsCraftingMaterialsIssueHandler"/> can capture its one rolled
/// field (the requested crafting material) and replicate it. Deliberately a much smaller payload than
/// <see cref="VillageIssueCreated"/>'s Tools equivalent: this issue type has no exchange-item/barter branch at
/// all (always pays gold - see <see cref="Interfaces.IVillageNeedsCraftingMaterialsIssueInterface"/>'s doc
/// comment), and its required-amount/reward are deliberately NOT captured here - they're re-derived at ACCEPT
/// time instead (see <see cref="VillageCraftingIssueQuestAcceptTriggered"/>).
/// </summary>
public readonly struct VillageCraftingIssueCreated : IEvent
{
    public readonly VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue Issue;

    public VillageCraftingIssueCreated(VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue issue)
    {
        Issue = issue;
    }
}

/// <summary>
/// Published from <see cref="Patches.VillageNeedsCraftingMaterialsAcceptancePatches"/> whenever
/// <c>IssueManager.StartIssueQuest</c> genuinely runs for a
/// <see cref="VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue"/> (the accepting
/// player's own live conversation, on whichever machine that is). <see cref="ControllerId"/> is that machine's
/// own <c>IControllerIdProvider.ControllerId</c> - see <see cref="Interfaces.VillageNeedsToolsIssueOwnership"/>
/// (reused unchanged for this issue type too).
///
/// Unlike the Tools equivalent, this ALSO carries <see cref="RequestedItemAmount"/>/<see cref="RewardGold"/>:
/// the central mechanism this issue type needs. <c>VillageNeedsCraftingMaterialsIssueQuest</c>'s required-item
/// count and reward gold are computed fresh at accept time from <c>IssueDifficultyMultiplier</c> and live
/// market data - both genuinely per-client-divergent - so whichever machine's accept is about to become
/// authoritative must capture what it actually baked, right here, so every other peer (and, critically, the
/// accepter's own client once the server's confirmation comes back) can be force-corrected to match instead of
/// each independently re-rolling its own terms. See
/// <see cref="Interfaces.IVillageNeedsCraftingMaterialsIssueInterface.MirrorQuestAccepted"/>.
/// </summary>
public readonly struct VillageCraftingIssueQuestAcceptTriggered : IEvent
{
    public readonly Hero Owner;
    public readonly string ControllerId;
    public readonly int RequestedItemAmount;
    public readonly int RewardGold;

    public VillageCraftingIssueQuestAcceptTriggered(Hero owner, string controllerId, int requestedItemAmount, int rewardGold)
    {
        Owner = owner;
        ControllerId = controllerId;
        RequestedItemAmount = requestedItemAmount;
        RewardGold = rewardGold;
    }
}

/// <summary>
/// Published from <see cref="Patches.VillageNeedsCraftingMaterialsAcceptancePatches"/> whenever
/// <c>IssueBase.StartIssueWithAlternativeSolution</c> genuinely runs for this issue type. No amount/reward
/// fields needed here (unlike <see cref="VillageCraftingIssueQuestAcceptTriggered"/>): the alternative-solution
/// path's own per-client-divergent payment (<c>_promisedPayment</c>) is only ever read on the one machine the
/// ownership gate allows to run the real payout consequence - see
/// <see cref="Patches.VillageNeedsCraftingMaterialsAlternativeSolutionCompletionPatches"/> - so there is no
/// cross-peer divergence to correct for this path at all.
/// </summary>
public readonly struct VillageCraftingIssueAlternativeAcceptTriggered : IEvent
{
    public readonly Hero Owner;
    public readonly string ControllerId;

    public VillageCraftingIssueAlternativeAcceptTriggered(Hero owner, string controllerId)
    {
        Owner = owner;
        ControllerId = controllerId;
    }
}

// --- Networked messages ---
//
// Deliberately their own new message types rather than reusing Tools' identically-shaped
// RequestVillageIssueAcceptQuest/NetworkVillageIssueQuestAccepted/NetworkVillageIssueAcceptRejected: both
// issue types' handlers would otherwise subscribe to the exact same wire message, and
// VillageNeedsToolsIssueHandler's own handlers branch on an `owner.Issue is VillageNeedsToolsIssue` type
// check with an `else` that sends a rejection - which would misfire a bogus rejection back to a client
// whose accept was actually for (and correctly handled by) the OTHER issue type's handler. Keeping the
// accept/reject wire types separate per issue type avoids that cross-handler ambiguity entirely.
//
// Teardown is different and IS safely reused unchanged (see the design note on
// Handlers.VillageNeedsCraftingMaterialsIssueHandler): VillageIssueFinalizeReason, VillageIssueFinalizedTriggered,
// RequestVillageIssueRemoved and NetworkVillageIssueRemoved (all in VillageNeedsToolsIssueMessages.cs) already
// carry no Tools-specific type constraint, and VillageNeedsToolsIssueHandler's own finalize-side handlers have
// no type guard at all - they operate generically on whichever IssueBase/QuestBase the owner Hero has. Adding a
// second, parallel subscription to those same messages here would just double the broadcast for every finalize
// (Tools' or this type's) without adding correctness.

/// <summary>Server -> all clients: the authoritatively-rolled requested material for a newly created Village
/// Needs Crafting Materials issue, so every client constructs a byte-identical instance instead of
/// independently re-rolling <c>SelectCraftingMaterial()</c> locally.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkVillageCraftingIssueCreated : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string RequestedItemId;

    public NetworkVillageCraftingIssueCreated(string ownerId, string requestedItemId)
    {
        OwnerId = ownerId;
        RequestedItemId = requestedItemId;
    }
}

/// <summary>Client -> server: "hero X's issue giver just accepted the quest solution in my live
/// conversation." Mirrors <see cref="RequestVillageIssueAcceptQuest"/>'s shape/reasoning exactly (a separate
/// type - see the file's top-of-section note for why) - the requesting client already applied the transition
/// locally with its own (not-yet-corrected) terms.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct RequestVillageCraftingIssueAcceptQuest : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public RequestVillageCraftingIssueAcceptQuest(string ownerId)
    {
        OwnerId = ownerId;
    }
}

/// <summary>Server -> all clients: confirms the quest-solution accept for this hero's issue, carrying the
/// authoritative <see cref="RequestedItemAmount"/>/<see cref="RewardGold"/> every peer (including the original
/// accepter) force-corrects its own quest object to - the larger accept-confirmation payload this issue type
/// needs, compared to Tools' equivalent. <see cref="OwnerControllerId"/> is resolved the same
/// never-trust-the-client way as <see cref="NetworkVillageIssueQuestAccepted"/> - see that type's doc
/// comment.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkVillageCraftingIssueQuestAccepted : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string OwnerControllerId;
    [ProtoMember(3)]
    public readonly int RequestedItemAmount;
    [ProtoMember(4)]
    public readonly int RewardGold;

    public NetworkVillageCraftingIssueQuestAccepted(string ownerId, string ownerControllerId, int requestedItemAmount, int rewardGold)
    {
        OwnerId = ownerId;
        OwnerControllerId = ownerControllerId;
        RequestedItemAmount = requestedItemAmount;
        RewardGold = rewardGold;
    }
}

/// <summary>Client -> server equivalent of <see cref="RequestVillageCraftingIssueAcceptQuest"/> for the
/// alternative-solution accept option.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct RequestVillageCraftingIssueAcceptAlternative : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public RequestVillageCraftingIssueAcceptAlternative(string ownerId)
    {
        OwnerId = ownerId;
    }
}

/// <summary>Server -> all clients equivalent of <see cref="NetworkVillageCraftingIssueQuestAccepted"/> for the
/// alternative-solution accept option. No amount/reward fields needed - see
/// <see cref="VillageCraftingIssueAlternativeAcceptTriggered"/>.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkVillageCraftingIssueAlternativeAccepted : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string OwnerControllerId;

    public NetworkVillageCraftingIssueAlternativeAccepted(string ownerId, string ownerControllerId)
    {
        OwnerId = ownerId;
        OwnerControllerId = ownerControllerId;
    }
}

/// <summary>
/// Server -> the one losing requester of a same-issue double-accept race for a Village Needs Crafting
/// Materials issue - see <see cref="NetworkVillageIssueAcceptRejected"/> for the shape/reasoning (a separate
/// type here for the same cross-handler-ambiguity reason as the request messages above).
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkVillageCraftingIssueAcceptRejected : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public NetworkVillageCraftingIssueAcceptRejected(string ownerId)
    {
        OwnerId = ownerId;
    }
}
