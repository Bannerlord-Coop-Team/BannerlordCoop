using Common.Messaging;
using ProtoBuf;
using SandBox.Issues;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Messages;

/// <summary>Published on the server after a genuine <c>IssueManager.CreateNewIssue</c> creates a
/// <see cref="TheSpyPartyIssueQuestBehavior.TheSpyPartyIssue"/>.</summary>
public readonly struct TheSpyPartyIssueCreated : IEvent
{
    public readonly TheSpyPartyIssueQuestBehavior.TheSpyPartyIssue Issue;

    public TheSpyPartyIssueCreated(TheSpyPartyIssueQuestBehavior.TheSpyPartyIssue issue)
    {
        Issue = issue;
    }
}

/// <summary>Server -&gt; all clients: the picked tournament-settlement for a newly created The Spy Party issue,
/// so every client replicates the exact same target instead of independently re-rolling.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkTheSpyPartyIssueCreated : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string SelectedSettlementId;

    public NetworkTheSpyPartyIssueCreated(string ownerId, string selectedSettlementId)
    {
        OwnerId = ownerId;
        SelectedSettlementId = selectedSettlementId;
    }
}

/// <summary>
/// Published locally the instant a genuine (non-replay) <c>IssueManager.StartIssueQuest</c> creates this hero's
/// <see cref="TheSpyPartyIssueQuestBehavior.TheSpyPartyIssueQuest"/>, carrying whichever accepting machine's own
/// <c>ControllerId</c> (real ownership, same shape as <c>VillageIssueQuestAcceptTriggered</c>) - deliberately
/// NOT the selected-spy index itself (a client's own locally-rolled index can't be trusted as authoritative; see
/// <see cref="Interfaces.ITheSpyPartyIssueInterface"/>'s type doc comment) unless the accepting machine IS the
/// server, in which case its own roll already IS authoritative.
/// </summary>
public readonly struct TheSpyPartyIssueQuestAcceptTriggered : IEvent
{
    public readonly Hero Owner;
    public readonly string ControllerId;

    public TheSpyPartyIssueQuestAcceptTriggered(Hero owner, string controllerId)
    {
        Owner = owner;
        ControllerId = controllerId;
    }
}

/// <summary>Client -&gt; server: "my own live conversation just accepted this hero's Spy Party quest solution" -
/// no locally-rolled spy index included (the server re-derives/reads back its own authoritative one via
/// <see cref="Interfaces.ITheSpyPartyIssueInterface.ReplayQuestAccepted"/>/<c>TryCaptureSelectedSpyIndex</c>),
/// and no ControllerId (derived server-side from the authenticated requester, never trusted from the client).
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct RequestTheSpyPartyIssueAcceptQuest : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public RequestTheSpyPartyIssueAcceptQuest(string ownerId)
    {
        OwnerId = ownerId;
    }
}

/// <summary>Server -&gt; all clients: the authoritative owner/ControllerId and selected-spy index for a genuine
/// Spy Party quest-solution accept, so every OTHER peer force-writes the exact same spy identity onto its own
/// mirrored quest object instead of trusting its own independent roll.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkTheSpyPartyIssueQuestAccepted : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string OwnerControllerId;
    [ProtoMember(3)]
    public readonly int SelectedSpyIndex;

    public NetworkTheSpyPartyIssueQuestAccepted(string ownerId, string ownerControllerId, int selectedSpyIndex)
    {
        OwnerId = ownerId;
        OwnerControllerId = ownerControllerId;
        SelectedSpyIndex = selectedSpyIndex;
    }
}

/// <summary>Server -&gt; the one losing requester in a same-issue double-accept race: roll your own already-
/// applied local accept back.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkTheSpyPartyIssueAcceptRejected : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public NetworkTheSpyPartyIssueAcceptRejected(string ownerId)
    {
        OwnerId = ownerId;
    }
}
