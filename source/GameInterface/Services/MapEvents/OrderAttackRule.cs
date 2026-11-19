using System.Linq;
using GameInterface.Services.MapEvents.Extensions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents;

public interface IOrderAttackRule
{
    bool CanOrderAttack(MapEvent mapEvent, MobileParty requesterParty, CharacterObject requesterCharacter);
    int CountOrderableTroops(PartyBase party, CharacterObject requesterCharacter);
}

public class OrderAttackRule : IOrderAttackRule
{
    public bool CanOrderAttack(MapEvent mapEvent, MobileParty requesterParty, CharacterObject requesterCharacter)
    {
        var eventParty = mapEvent.FindMapEventParty(requesterParty.Party, out var side);
        if (eventParty == null)
            return false;

        var opponentParty = mapEvent.PartiesOnSide(side.MissionSide.GetOppositeSide()).FirstOrDefault();
        if (opponentParty?.Party.MobileParty?.IsInRaftState == true)
            return false;

        var orderableTroops = side.Parties
            .Where(party => !party.Party.IsMobile || party.Party.MobileParty.IsInRaftState == false)
            .Sum(party => CountOrderableTroops(party.Party, requesterCharacter));

        return mapEvent.HasTroopsOnBothSides()
            && mapEvent.GetLeaderParty(side.MissionSide.GetOppositeSide()) != null
            && orderableTroops > 0;
    }

    public int CountOrderableTroops(PartyBase party, CharacterObject requesterCharacter)
    {
        var count = 0;
        foreach (var element in party.MemberRoster.GetTroopRoster())
        {
            if (element.Character.IsHero)
            {
                if (element.Character != requesterCharacter && !element.Character.HeroObject.IsWounded)
                    count++;
            }
            else
            {
                count += element.Number - element.WoundedNumber;
            }
        }

        return count;
    }
}
