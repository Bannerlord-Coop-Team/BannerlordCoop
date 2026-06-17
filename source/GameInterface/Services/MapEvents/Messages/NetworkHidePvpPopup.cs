using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages;

/// <summary>
/// Server -&gt; all clients: the given player parties were just added to a map event (the battle started). The client
/// controlling one of them drops its "hold on" PvP popup — the battle menu blocks the player now. Driven from the
/// server's authoritative add because the client-side <see cref="MapEventInvolvedPartiesAdded"/> never fires for a
/// synced add (the client's own add is intercepted and routed to the server).
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkHidePvpPopup : ICommand
{
    [ProtoMember(1)]
    public readonly string[] PartyIds;

    public NetworkHidePvpPopup(string[] partyIds)
    {
        PartyIds = partyIds;
    }
}
