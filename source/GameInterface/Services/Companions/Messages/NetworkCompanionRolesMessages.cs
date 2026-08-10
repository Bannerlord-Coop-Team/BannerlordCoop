using Common.Messaging;
using GameInterface.Services.TroopRosters.Data;
using ProtoBuf;

namespace GameInterface.Services.Companions.Messages;

internal enum CompanionRescueRequestKind
{
    JoinParty = 1,
    LeadParty = 2,
}

internal enum CompanionRescueCompletionStatus
{
    Accepted = 1,
    AlreadyCompleted = 2,
    Rejected = 3,
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct DoClanNameSelection : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string OneToOneConversationHeroId;

    [ProtoMember(3)]
    public readonly string SelectedFiefId;

    [ProtoMember(4)]
    public readonly string MainPartyId;

    [ProtoMember(5)]
    public readonly string ClanName;

    public DoClanNameSelection(
        string mainHeroId,
        string oneToOneConversationHeroId,
        string selectedFiefId,
        string mainPartyId,
        string clanName)
    {
        MainHeroId = mainHeroId;
        OneToOneConversationHeroId = oneToOneConversationHeroId;
        SelectedFiefId = selectedFiefId;
        MainPartyId = mainPartyId;
        ClanName = clanName;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct FireCompanion : IEvent
{
    [ProtoMember(1)]
    public readonly string RequestId;

    [ProtoMember(2)]
    public readonly string OneToOneConversationHeroId;

    [ProtoMember(3)]
    public readonly string ExpectedClanId;

    [ProtoMember(4)]
    public readonly string ExpectedPartyId;

    public FireCompanion(string requestId, string oneToOneConversationHeroId,
        string expectedClanId, string expectedPartyId)
    {
        RequestId = requestId;
        OneToOneConversationHeroId = oneToOneConversationHeroId;
        ExpectedClanId = expectedClanId;
        ExpectedPartyId = expectedPartyId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct FireCompanionCompleted : ICommand
{
    [ProtoMember(1)]
    public readonly string RequestId;

    [ProtoMember(2)]
    public readonly string OneToOneConversationHeroId;

    [ProtoMember(3)]
    public readonly bool Success;

    [ProtoMember(4)]
    public readonly string Error;

    public FireCompanionCompleted(string requestId, string oneToOneConversationHeroId,
        bool success, string error)
    {
        RequestId = requestId;
        OneToOneConversationHeroId = oneToOneConversationHeroId;
        Success = success;
        Error = error;
    }
}

internal readonly struct CompanionDismissalCompleted : IEvent
{
    public readonly string RequestId;
    public readonly string OneToOneConversationHeroId;
    public readonly bool Success;
    public readonly string Error;

    public CompanionDismissalCompleted(string requestId, string oneToOneConversationHeroId,
        bool success, string error)
    {
        RequestId = requestId;
        OneToOneConversationHeroId = oneToOneConversationHeroId;
        Success = success;
        Error = error;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct DoCompanionRejoinAfterEmprisonment : IEvent
{
    [ProtoMember(1)]
    public readonly string OneToOneConversationHeroId;

    [ProtoMember(2)]
    public readonly string MainPartyId;

    public DoCompanionRejoinAfterEmprisonment(
        string oneToOneConversationHeroId,
        string mainPartyId)
    {
        OneToOneConversationHeroId = oneToOneConversationHeroId;
        MainPartyId = mainPartyId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct DoCompanionJoinedPartyByRescue : IEvent
{
    [ProtoMember(1)]
    public readonly string OneToOneConversationHeroId;

    [ProtoMember(2)]
    public readonly string MainPartyId;

    [ProtoMember(3)]
    public readonly string RequestId;

    [ProtoMember(4)]
    public readonly string ExpectedClanId;

    [ProtoMember(5)]
    public readonly string ExpectedCaptorPartyId;

    public DoCompanionJoinedPartyByRescue(
        string oneToOneConversationHeroId,
        string mainPartyId,
        string requestId,
        string expectedClanId,
        string expectedCaptorPartyId)
    {
        OneToOneConversationHeroId = oneToOneConversationHeroId;
        MainPartyId = mainPartyId;
        RequestId = requestId;
        ExpectedClanId = expectedClanId;
        ExpectedCaptorPartyId = expectedCaptorPartyId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct DoPartyScreenClosedFromRescuing : IEvent
{
    [ProtoMember(1)]
    public readonly TroopRosterData LeftMemberRosterData;

    [ProtoMember(2)]
    public readonly TroopRosterData LeftPrisonRosterData;

    [ProtoMember(3)]
    public readonly string RightOwnerPartyId;

    [ProtoMember(4)]
    public readonly string RequestId;

    [ProtoMember(5)]
    public readonly string CompanionHeroId;

    [ProtoMember(6)]
    public readonly string ExpectedClanId;

    [ProtoMember(7)]
    public readonly string ExpectedCaptorPartyId;

    public DoPartyScreenClosedFromRescuing(
        TroopRosterData leftMemberRosterData,
        TroopRosterData leftPrisonRosterData,
        string rightOwnerPartyId,
        string requestId,
        string companionHeroId,
        string expectedClanId,
        string expectedCaptorPartyId)
    {
        LeftMemberRosterData = leftMemberRosterData;
        LeftPrisonRosterData = leftPrisonRosterData;
        RightOwnerPartyId = rightOwnerPartyId;
        RequestId = requestId;
        CompanionHeroId = companionHeroId;
        ExpectedClanId = expectedClanId;
        ExpectedCaptorPartyId = expectedCaptorPartyId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct CompanionRescueCompleted : ICommand
{
    [ProtoMember(1)]
    public readonly string RequestId;

    [ProtoMember(2)]
    public readonly string CompanionHeroId;

    [ProtoMember(3)]
    public readonly CompanionRescueRequestKind Kind;

    [ProtoMember(4)]
    public readonly CompanionRescueCompletionStatus Status;

    [ProtoMember(5)]
    public readonly string Error;

    public CompanionRescueCompleted(
        string requestId,
        string companionHeroId,
        CompanionRescueRequestKind kind,
        CompanionRescueCompletionStatus status,
        string error)
    {
        RequestId = requestId;
        CompanionHeroId = companionHeroId;
        Kind = kind;
        Status = status;
        Error = error;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RescueCompanion : IEvent
{
    [ProtoMember(1)]
    public readonly string OneToOneConversationHeroId;

    public RescueCompanion(string oneToOneConversationHeroId)
    {
        OneToOneConversationHeroId = oneToOneConversationHeroId;
    }
}
