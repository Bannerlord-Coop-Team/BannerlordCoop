using ProtoBuf;

namespace GameInterface.Services.Players.Data;

[ProtoContract(SkipConstructor = true)]
public class Player
{
    [ProtoMember(1)]
    public string HeroId { get; }
    [ProtoMember(2)]
    public string MobilePartyId { get; }

    public Player(string heroId, string mobilePartyId)
    {
        HeroId = heroId;
        MobilePartyId = mobilePartyId;
    }
}
