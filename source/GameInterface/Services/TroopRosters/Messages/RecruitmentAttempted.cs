using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.TroopRosters.Messages;
public readonly struct RecruitmentAttempted : IEvent
{
    public readonly MobileParty MobileParty;

    public readonly (Hero, CharacterObject, int)[] TroopsInCart;

    public RecruitmentAttempted(MobileParty mobileParty, IEnumerable<(Hero, CharacterObject, int)> troopsInCart)
    {
        MobileParty = mobileParty;
        TroopsInCart = troopsInCart.ToArray();
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