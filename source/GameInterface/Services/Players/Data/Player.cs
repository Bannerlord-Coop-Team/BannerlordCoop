using ProtoBuf;

namespace GameInterface.Services.Players.Data;

/// <summary>Stores a player's controller identity, campaign objects and last known platform name.</summary>
[ProtoContract(SkipConstructor = true)]
public class Player
{
    [ProtoMember(1)]
    public readonly string ControllerId;
    [ProtoMember(2)]
    public readonly string HeroId;
    [ProtoMember(3)]
    public readonly string MobilePartyId;
    [ProtoMember(4)]
    public readonly string ClanId;
    [ProtoMember(5)]
    public readonly string CharacterObjectId;
    [ProtoMember(6)]
    public string PlatformName { get; set; }
    [ProtoMember(7)]
    public readonly string OriginalClanId;

    // Associates one controller with its registered campaign objects.
    public Player(string controllerId, string heroId, string mobilePartyId, string clanId, string characterObjectId, string originalClanId = null)
    {
        ControllerId = controllerId;
        HeroId = heroId;
        MobilePartyId = mobilePartyId;
        ClanId = clanId;
        CharacterObjectId = characterObjectId;
        OriginalClanId = originalClanId ?? clanId;
    }
}
