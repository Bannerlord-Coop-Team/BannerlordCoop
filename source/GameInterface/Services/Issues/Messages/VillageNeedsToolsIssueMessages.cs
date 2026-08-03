using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Messages;

// --- Local events ---

/// <summary>
/// Published on the server (from <see cref="Patches.IssueManagerCreateNewIssuePatches"/>) immediately
/// after <c>IssueManager.CreateNewIssue</c> creates a new <see cref="VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue"/>
/// for real. Carries the live issue so <see cref="Handlers.VillageNeedsToolsIssueHandler"/> can capture its
/// rolled fields (requested/exchange item, quantities, payment) and replicate them.
/// </summary>
public readonly struct VillageIssueCreated : IEvent
{
    public readonly VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue Issue;

    public VillageIssueCreated(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue issue)
    {
        Issue = issue;
    }
}

/// <summary>
/// Published (on whichever machine reaches it - the server for an ambient-tick timeout, or the one client
/// in the quest-turn-in conversation for a success) from <see cref="Patches.IssueFinalizedPatches"/>
/// whenever a <see cref="VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue"/>'s <c>IssueFinalized</c>
/// runs for real (success, timeout, cancel, betrayal, fail, ...) rather than as a mirrored replay of a
/// received network message. See <see cref="Handlers.VillageNeedsToolsIssueHandler"/> for the client/server
/// routing this drives.
/// </summary>
public readonly struct VillageIssueFinalizedTriggered : IEvent
{
    public readonly Hero Owner;

    public VillageIssueFinalizedTriggered(Hero owner)
    {
        Owner = owner;
    }
}

// --- Networked messages ---

/// <summary>Server -> all clients: the authoritatively-rolled terms of a newly created Village Needs
/// Tools issue, so every client constructs a byte-identical instance instead of re-rolling locally.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkVillageIssueCreated : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string RequestedItemId;
    /// <summary>Null when the village pays in gold instead of goods (see the ctor: Village.Hearth &gt;= 300).</summary>
    [ProtoMember(3)]
    public readonly string ExchangeItemId;
    [ProtoMember(4)]
    public readonly int NumberOfRequestedItem;
    [ProtoMember(5)]
    public readonly int NumberOfExchangeItem;
    [ProtoMember(6)]
    public readonly int Payment;

    public NetworkVillageIssueCreated(
        string ownerId,
        string requestedItemId,
        string exchangeItemId,
        int numberOfRequestedItem,
        int numberOfExchangeItem,
        int payment)
    {
        OwnerId = ownerId;
        RequestedItemId = requestedItemId;
        ExchangeItemId = exchangeItemId;
        NumberOfRequestedItem = numberOfRequestedItem;
        NumberOfExchangeItem = numberOfExchangeItem;
        Payment = payment;
    }
}

/// <summary>
/// Client -> server: this client's local copy of the issue finalized outside of a mirrored replay (e.g.
/// the accepting player just turned the quest in). A client's <c>SendAll</c> only reaches its one
/// connection - the server - which then replays the finalize on its own authoritative copy and broadcasts
/// <see cref="NetworkVillageIssueRemoved"/> to every peer.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct RequestVillageIssueRemoved : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public RequestVillageIssueRemoved(string ownerId)
    {
        OwnerId = ownerId;
    }
}

/// <summary>Server -> all clients: mirror the removal/teardown of this hero's Village Needs Tools issue.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkVillageIssueRemoved : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public NetworkVillageIssueRemoved(string ownerId)
    {
        OwnerId = ownerId;
    }
}
