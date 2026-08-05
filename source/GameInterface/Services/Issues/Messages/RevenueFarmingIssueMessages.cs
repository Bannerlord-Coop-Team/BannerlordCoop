using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Messages;

// --- Local events ---

/// <summary>
/// Published on the server (from <see cref="Patches.RevenueFarmingIssueCreationPatch"/>) immediately after a
/// genuine <c>IssueManager.CreateNewIssue</c> creates a new
/// <see cref="RevenueFarmingIssueBehavior.RevenueFarmingIssue"/>. Carries the live issue so
/// <see cref="Handlers.RevenueFarmingIssueHandler"/> can capture its one ctor-arg field (<c>_targetSettlement</c>)
/// and replicate it - see <see cref="Interfaces.IRevenueFarmingIssueInterface"/>'s type doc comment for why no
/// reflection is needed for this field at all (the public ctor takes it directly). Accept-quest is NOT handled
/// generically here (unlike e.g. Deliver the Herd) - see <see cref="RevenueFarmingQuestAcceptTriggered"/> for
/// why this type needs its own bespoke accept-time capture. This type has no alternative solution
/// (<c>IsThereAlternativeSolution == false</c>, confirmed against the decompiled source), so there is no
/// alternative-solution-accept message pair at all.
/// </summary>
public readonly struct RevenueFarmingIssueCreated : IEvent
{
    public readonly RevenueFarmingIssueBehavior.RevenueFarmingIssue Issue;

    public RevenueFarmingIssueCreated(RevenueFarmingIssueBehavior.RevenueFarmingIssue issue)
    {
        Issue = issue;
    }
}

/// <summary>
/// Published from <see cref="Patches.RevenueFarmingQuestAcceptancePatch"/> whenever
/// <c>IssueManager.StartIssueQuest</c> genuinely runs for a
/// <see cref="RevenueFarmingIssueBehavior.RevenueFarmingIssue"/> (the accepting player's own live conversation,
/// on whichever machine that is). <see cref="ControllerId"/> is that machine's own
/// <c>IControllerIdProvider.ControllerId</c> - see <see cref="Interfaces.VillageNeedsToolsIssueOwnership"/>
/// (reused unchanged for this issue type too).
///
/// This is the central mechanism this issue type needs (task item 3): <c>RevenueFarmingIssueQuest</c>'s
/// <c>_revenueVillages</c> list is re-derived LIVE at accept time from
/// <c>_targetSettlement.BoundVillages</c>, filtered by each village's CURRENT <c>IsUnderRaid</c>/<c>IsRaided</c>
/// state at that exact instant - genuinely per-client-divergent if raid timing differs even slightly across
/// peers. <c>_totalRequestedDenars</c> is ALSO genuinely independent from the Issue-level
/// <c>TotalRequestedDenars</c> preview property (confirmed against the decompiled source): the Issue sums
/// every village's <c>Hearth*4</c> first, then divides by 3 once; the Quest ctor instead divides each village's
/// own <c>TargetAmount</c> by 3 individually, THEN sums - integer-division-on-differing-groupings, a genuinely
/// different number whenever any village's TargetAmount isn't already a multiple of 3. Whichever machine's
/// accept is about to become authoritative must capture what it actually baked, right here, so every other peer
/// (and the accepter's own client once the server's confirmation comes back) can be force-corrected to match.
/// See <see cref="Interfaces.IRevenueFarmingIssueInterface.MirrorQuestAccepted"/>.
/// </summary>
public readonly struct RevenueFarmingQuestAcceptTriggered : IEvent
{
    public readonly Hero Owner;
    public readonly string ControllerId;
    public readonly int TotalRequestedDenars;
    public readonly IReadOnlyList<(Settlement Settlement, int TargetAmount)> Villages;

    public RevenueFarmingQuestAcceptTriggered(
        Hero owner, string controllerId, int totalRequestedDenars,
        IReadOnlyList<(Settlement Settlement, int TargetAmount)> villages)
    {
        Owner = owner;
        ControllerId = controllerId;
        TotalRequestedDenars = totalRequestedDenars;
        Villages = villages;
    }
}

// --- Networked messages ---

/// <summary>Server -> all clients: the authoritative <c>_targetSettlement</c> for a newly created Revenue
/// Farming issue, so every client constructs a byte-identical instance instead of trusting whatever the Issue's
/// own <c>OnCheckForIssue</c>/<c>ConditionsHold</c> rolled independently on that client. Carries a Settlement
/// StringId, NOT any Village-typed reference - <c>Village</c> is a <see cref="SettlementComponent"/>, not
/// independently registered with this project's <see cref="GameInterface.Services.ObjectManager.IObjectManager"/>
/// (same convention as <c>HeadmanNeedsToDeliverAHerdIssueMessages.NetworkHeadmanNeedsToDeliverAHerdIssueCreated.TargetSettlementId</c>).
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkRevenueFarmingIssueCreated : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string TargetSettlementId;

    public NetworkRevenueFarmingIssueCreated(string ownerId, string targetSettlementId)
    {
        OwnerId = ownerId;
        TargetSettlementId = targetSettlementId;
    }
}

/// <summary>
/// One wire-friendly entry of the authoritative <c>_revenueVillages</c> list (task item 3): carries the
/// specific village settlement's own StringId (re-derived via <c>settlement.Village</c> on receipt - NOT the
/// village's parent <c>Bound</c> town) plus its <c>TargetAmount</c>. Nothing else needs to travel: a receiving
/// peer reconstructs each <c>RevenueVillage</c> via its real public ctor
/// (<c>RevenueVillage(Village, int)</c>), which already zeroes <c>CollectedAmount</c>/<c>_isDone</c>/
/// <c>EventOccurred</c>/<c>IsRaided</c>/<c>_customProgress</c> for free - no reflection needed to reset those at
/// accept time.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct RevenueVillageWireEntry
{
    [ProtoMember(1)]
    public readonly string SettlementId;
    [ProtoMember(2)]
    public readonly int TargetAmount;

    public RevenueVillageWireEntry(string settlementId, int targetAmount)
    {
        SettlementId = settlementId;
        TargetAmount = targetAmount;
    }
}

/// <summary>Client -> server: "hero X's issue giver just accepted the quest solution in my live conversation."
/// The requesting client already applied the transition locally with its own (not-yet-corrected) terms - same
/// shape as <c>RequestVillageCraftingIssueAcceptQuest</c>. A separate type (not shared with any other issue
/// type's accept message) for the same cross-handler-ambiguity reason
/// <c>VillageNeedsCraftingMaterialsIssueMessages</c>'s file-level note documents.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct RequestRevenueFarmingIssueAcceptQuest : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public RequestRevenueFarmingIssueAcceptQuest(string ownerId)
    {
        OwnerId = ownerId;
    }
}

/// <summary>Server -> all clients: confirms the quest-solution accept for this hero's issue, carrying the
/// authoritative <see cref="TotalRequestedDenars"/>/<see cref="Villages"/> every peer (including the original
/// accepter) force-corrects its own quest object to. <see cref="OwnerControllerId"/> is resolved the same
/// never-trust-the-client way as every other type's equivalent - see that type's doc comment.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkRevenueFarmingIssueQuestAccepted : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string OwnerControllerId;
    [ProtoMember(3)]
    public readonly int TotalRequestedDenars;
    [ProtoMember(4)]
    public readonly RevenueVillageWireEntry[] Villages;

    public NetworkRevenueFarmingIssueQuestAccepted(
        string ownerId, string ownerControllerId, int totalRequestedDenars, RevenueVillageWireEntry[] villages)
    {
        OwnerId = ownerId;
        OwnerControllerId = ownerControllerId;
        TotalRequestedDenars = totalRequestedDenars;
        Villages = villages;
    }
}

/// <summary>
/// Server -> the one losing requester of a same-issue double-accept race for a Revenue Farming issue - same
/// shape/reasoning as <c>NetworkVillageIssueAcceptRejected</c>/<c>NetworkVillageCraftingIssueAcceptRejected</c>
/// (a separate type here for the same cross-handler-ambiguity reason as the request message above). Consumed by
/// the shared, already-fixed <see cref="Interfaces.IRevenueFarmingIssueInterface.RejectAcceptance"/> - no
/// bespoke reject logic of its own (task requirement: "delegate to the shared fixed RejectAcceptance
/// mechanism").
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkRevenueFarmingIssueAcceptRejected : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public NetworkRevenueFarmingIssueAcceptRejected(string ownerId)
    {
        OwnerId = ownerId;
    }
}
