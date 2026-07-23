using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Companions.Messages;

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

    public DoCompanionJoinedPartyByRescue(
        string oneToOneConversationHeroId,
        string mainPartyId)
    {
        OneToOneConversationHeroId = oneToOneConversationHeroId;
        MainPartyId = mainPartyId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct DoPartyScreenClosedFromRescuing : IEvent
{
    [ProtoMember(1)]
    public readonly string LeftOwnerPartyId;

    [ProtoMember(2)]
    public readonly string LeftMemberRosterId;

    [ProtoMember(3)]
    public readonly string LeftPrisonRosterId;

    [ProtoMember(4)]
    public readonly string RightOwnerPartyId;

    [ProtoMember(5)]
    public readonly string RightMemberRosterId;

    [ProtoMember(6)]
    public readonly string RightPrisonRosterId;

    public DoPartyScreenClosedFromRescuing(
        string leftOwnerPartyId,
        string leftMemberRosterId,
        string leftPrisonRosterId,
        string rightOwnerPartyId,
        string rightMemberRosterId,
        string rightPrisonRosterId)
    {
        LeftOwnerPartyId = leftOwnerPartyId;
        LeftMemberRosterId = leftMemberRosterId;
        LeftPrisonRosterId = leftPrisonRosterId;
        RightOwnerPartyId = rightOwnerPartyId;
        RightMemberRosterId = rightMemberRosterId;
        RightPrisonRosterId = rightPrisonRosterId;
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
