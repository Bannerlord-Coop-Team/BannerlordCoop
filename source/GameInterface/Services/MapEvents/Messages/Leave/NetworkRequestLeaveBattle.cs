using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Leave;

// [Client -> Server] Remove this party from its map event side, leaving the battle running.
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestLeaveBattle : ICommand
{
    [ProtoMember(1)]
    public readonly string PartyId;
    [ProtoMember(2)]
    public readonly bool FinishLocalMenus;

    public NetworkRequestLeaveBattle(string partyId, bool finishLocalMenus = true)
    {
        PartyId = partyId;
        FinishLocalMenus = finishLocalMenus;
    }
}
