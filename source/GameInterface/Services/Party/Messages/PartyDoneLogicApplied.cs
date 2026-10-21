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

    public PartyDoneLogicApplied(NetPeer requester, PartyScreenHelper.PartyScreenMode mode, TroopRosterData memberDelta)
    {
        Requester = requester;
        Mode = mode;
        MemberDelta = memberDelta;
    }
}
