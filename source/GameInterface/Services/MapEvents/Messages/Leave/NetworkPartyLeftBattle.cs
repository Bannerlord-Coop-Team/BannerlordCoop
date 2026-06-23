using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Leave;

// [Server -> All] Apply a party's authoritative removal from its map event side.
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPartyLeftBattle : ICommand
{
    [ProtoMember(1)]
    public readonly string PartyId;

    public NetworkPartyLeftBattle(string partyId)
    {
        PartyId = partyId;
    }
}
