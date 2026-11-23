using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages;

/// <summary>
/// Server -&gt; all clients: the given player parties were just added to a map event (the battle started). The client
/// controlling one of them drops its "hold on" PvP popup — the battle menu blocks the player now. Driven from the
/// server's authoritative add because <c>BattleHandler</c> ignores <see cref="MapEventInvolvedPartiesAdded"/> on a
/// client, even when a replicated add raises it there.
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
