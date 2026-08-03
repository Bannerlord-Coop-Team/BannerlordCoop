using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Messages;

/// <summary>Published from <see cref="Patches.LandLordNeedsManualLaborersAcceptancePatches"/> whenever
/// <c>IssueManager.StartIssueQuest</c> genuinely runs for a Landowner Needs Manual Laborers issue. Carries the
/// accepting machine's captured <c>_requestedPrisonerCount</c>/<c>_maximumPrisonerCount</c> - see
/// <see cref="Interfaces.ILandLordNeedsManualLaborersIssueInterface"/>'s doc comment.</summary>
public readonly struct LandLordManualLaborersIssueQuestAcceptTriggered : IEvent
{
    public readonly Hero Owner;
    public readonly string ControllerId;
    public readonly int RequestedPrisonerCount;
    public readonly int MaximumPrisonerCount;

    public LandLordManualLaborersIssueQuestAcceptTriggered(Hero owner, string controllerId, int requestedPrisonerCount, int maximumPrisonerCount)
    {
        Owner = owner;
        ControllerId = controllerId;
        RequestedPrisonerCount = requestedPrisonerCount;
        MaximumPrisonerCount = maximumPrisonerCount;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestLandLordManualLaborersIssueAcceptQuest : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public RequestLandLordManualLaborersIssueAcceptQuest(string ownerId)
    {
        OwnerId = ownerId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkLandLordManualLaborersIssueQuestAccepted : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string OwnerControllerId;
    [ProtoMember(3)]
    public readonly int RequestedPrisonerCount;
    [ProtoMember(4)]
    public readonly int MaximumPrisonerCount;

    public NetworkLandLordManualLaborersIssueQuestAccepted(string ownerId, string ownerControllerId, int requestedPrisonerCount, int maximumPrisonerCount)
    {
        OwnerId = ownerId;
        OwnerControllerId = ownerControllerId;
        RequestedPrisonerCount = requestedPrisonerCount;
        MaximumPrisonerCount = maximumPrisonerCount;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkLandLordManualLaborersIssueAcceptRejected : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public NetworkLandLordManualLaborersIssueAcceptRejected(string ownerId)
    {
        OwnerId = ownerId;
    }
}
