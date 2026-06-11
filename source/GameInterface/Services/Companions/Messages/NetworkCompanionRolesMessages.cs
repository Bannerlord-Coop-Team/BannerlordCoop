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
    public readonly string OneToOneConversationHeroId;

    public FireCompanion(string oneToOneConversationHeroId)
    {
        OneToOneConversationHeroId = oneToOneConversationHeroId;
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