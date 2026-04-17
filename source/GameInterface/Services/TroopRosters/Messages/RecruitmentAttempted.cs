using Common.Messaging;
using ProtoBuf;
using System.Reflection.Metadata;

namespace GameInterface.Services.TroopRosters.Messages;
public readonly struct RecruitmentAttempted : IEvent
{
    public readonly string MobilePartyId;

    public readonly TroopInfo[] TroopsInCart;

    public RecruitmentAttempted(string mobilePartyId, TroopInfo[] troopsInCart)
    {
        MobilePartyId = mobilePartyId;
        TroopsInCart = troopsInCart;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct TroopInfo
{
    [ProtoMember(1)]
    public readonly string RecruiterHeroId;
    [ProtoMember(2)]
    public readonly string CharacterObjectId;
    [ProtoMember(3)]
    public readonly int TroopIndex;

    public TroopInfo(string recruiterHeroId, string characterObjectId, int troopIndex)
    {
        RecruiterHeroId = recruiterHeroId;
        CharacterObjectId = characterObjectId;
        TroopIndex = troopIndex;
    }
}