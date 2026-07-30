using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Leave;

// [Server -> All] Apply a party's authoritative removal from its battle participation.
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPartyLeftBattle : ICommand
{
    [ProtoMember(1)]
    public readonly string PartyId;
    [ProtoMember(2)]
    public readonly bool LeaveSiege;

    public NetworkPartyLeftBattle(string partyId, bool leaveSiege)
    {
        PartyId = partyId;
        LeaveSiege = leaveSiege;
    }
}
