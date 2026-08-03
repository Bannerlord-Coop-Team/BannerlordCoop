using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Messages;

/// <summary>Published from <see cref="Patches.BettingFraudAcceptancePatches"/> whenever
/// <c>IssueManager.StartIssueQuest</c> genuinely runs for a Betting Fraud issue. Carries the accepting
/// machine's captured <c>_thug</c> - see <see cref="Interfaces.IBettingFraudIssueInterface"/>'s doc comment.</summary>
public readonly struct BettingFraudIssueQuestAcceptTriggered : IEvent
{
    public readonly Hero Owner;
    public readonly string ControllerId;
    public readonly CharacterObject Thug;

    public BettingFraudIssueQuestAcceptTriggered(Hero owner, string controllerId, CharacterObject thug)
    {
        Owner = owner;
        ControllerId = controllerId;
        Thug = thug;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestBettingFraudIssueAcceptQuest : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public RequestBettingFraudIssueAcceptQuest(string ownerId)
    {
        OwnerId = ownerId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkBettingFraudIssueQuestAccepted : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string OwnerControllerId;
    [ProtoMember(3)]
    public readonly string ThugId;

    public NetworkBettingFraudIssueQuestAccepted(string ownerId, string ownerControllerId, string thugId)
    {
        OwnerId = ownerId;
        OwnerControllerId = ownerControllerId;
        ThugId = thugId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkBettingFraudIssueAcceptRejected : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public NetworkBettingFraudIssueAcceptRejected(string ownerId)
    {
        OwnerId = ownerId;
    }
}
