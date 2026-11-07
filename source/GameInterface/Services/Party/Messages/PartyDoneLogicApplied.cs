using Common.Messaging;
using GameInterface.Services.TroopRosters.Data;
using Helpers;
using LiteNetLib;

namespace GameInterface.Services.Party.Messages;

public readonly struct PartyDoneLogicApplied : IEvent
{
    public NetPeer Requester { get; }
    public PartyScreenHelper.PartyScreenMode Mode { get; }
    public TroopRosterData MemberDelta { get; }
    public string MainHeroId { get; }
    public string IssueOwnerId { get; }
    public string IssueId { get; }

    public PartyDoneLogicApplied(NetPeer requester, PartyScreenHelper.PartyScreenMode mode, TroopRosterData memberDelta,
        string mainHeroId = null, string issueOwnerId = null, string issueId = null)
    {
        Requester = requester;
        Mode = mode;
        MemberDelta = memberDelta;
        MainHeroId = mainHeroId;
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
    }
}
