using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Messages;

/// <summary>
/// Published on the server (from <see cref="Patches.SimpleIssueCreationPatch"/>) immediately after
/// <c>IssueManager.CreateNewIssue</c> creates a new instance of one of
/// <see cref="Interfaces.SimpleIssueFactoryRegistry"/>'s registered "no fields to capture" issue types.
/// </summary>
public readonly struct SimpleIssueCreated : IEvent
{
    public readonly IssueBase Issue;

    public SimpleIssueCreated(IssueBase issue)
    {
        Issue = issue;
    }
}

/// <summary>Server -&gt; all clients: "construct issue type <see cref="IssueKey"/> for owner
/// <see cref="OwnerId"/>" - no other payload needed, since every type
/// <see cref="Interfaces.SimpleIssueFactoryRegistry"/> covers rolls nothing at creation time that a client
/// would need to replicate byte-identically (see that type's own doc comment).</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkSimpleIssueCreated : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string IssueKey;

    public NetworkSimpleIssueCreated(string ownerId, string issueKey)
    {
        OwnerId = ownerId;
        IssueKey = issueKey;
    }
}
