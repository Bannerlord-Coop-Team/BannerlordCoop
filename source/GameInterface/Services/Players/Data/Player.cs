using ProtoBuf;

namespace GameInterface.Services.Players.Data;

[ProtoContract(SkipConstructor = true)]
public class Player
{
    [ProtoMember(1)]
    public string PartyId { get; }

    public Player(string partyId)
    {
        PartyId = partyId;
    }
}
