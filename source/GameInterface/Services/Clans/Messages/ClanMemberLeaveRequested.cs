using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages;

public readonly struct ClanMemberLeaveRequested : IEvent
{
    public readonly Hero Actor;
    public readonly Hero Member;

    public ClanMemberLeaveRequested(Hero actor, Hero member)
    {
        Actor = actor;
        Member = member;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestClanMemberLeave : ICommand
{
    [ProtoMember(1)]
    public readonly string ActorId;
    [ProtoMember(2)]
    public readonly string MemberId;

    public RequestClanMemberLeave(string actorId, string memberId)
    {
        ActorId = actorId;
        MemberId = memberId;
    }
}
