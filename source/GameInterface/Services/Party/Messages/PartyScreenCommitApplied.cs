using Common.Messaging;
using GameInterface.Services.TroopRosters.Data;
using LiteNetLib;

namespace GameInterface.Services.Party.Messages;

/// <summary>
/// The server applied a party screen commit. MemberDelta is how the left member side changed.
/// </summary>
public readonly struct PartyScreenCommitApplied : IEvent
{
    public NetPeer Requester { get; }
    public string MainHeroId { get; }
    public string IssueOwnerId { get; }
    public string IssueId { get; }
    public TroopRosterData MemberDelta { get; }

    public PartyScreenCommitApplied(
        NetPeer requester,
        string mainHeroId,
        string issueOwnerId,
        string issueId,
        TroopRosterData memberDelta)
    {
        Requester = requester;
        MainHeroId = mainHeroId;
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        MemberDelta = memberDelta;
    }
}
