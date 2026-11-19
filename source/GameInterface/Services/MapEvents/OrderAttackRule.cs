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
    bool HasOrderableTroops(MapEventSide side, CharacterObject requesterCharacter);
    bool IsFirstOpponentInRaft(MapEvent mapEvent, BattleSideEnum opponentSide);
    int CountOrderableTroops(PartyBase party, CharacterObject requesterCharacter);
}

public class OrderAttackRule : IOrderAttackRule
{
    public bool CanOrderAttack(MapEvent mapEvent, MobileParty requesterParty, CharacterObject requesterCharacter)
    {
        return HasOrderAttackPreconditions(mapEvent, requesterParty, out var side, out var oppositeSide)
            && !IsFirstOpponentInRaft(mapEvent, oppositeSide)
            && HasOrderableTroops(side, requesterCharacter);
    }

    private static bool HasOrderAttackPreconditions(MapEvent mapEvent, MobileParty requesterParty,
        out MapEventSide side, out BattleSideEnum oppositeSide)
    {
        var eventParty = mapEvent.FindMapEventParty(requesterParty.Party, out side);
        oppositeSide = BattleSideEnum.None;
        if (eventParty == null)
            return false;

        oppositeSide = side.MissionSide.GetOppositeSide();
        return mapEvent.HasTroopsOnBothSides() && mapEvent.GetLeaderParty(oppositeSide) != null;
    }

    public bool HasOrderableTroops(MapEventSide side, CharacterObject requesterCharacter)
    {
        var orderableTroops = side.Parties
            .Where(party => !party.Party.IsMobile || !party.Party.MobileParty.IsInRaftState)
            .Sum(party => CountOrderableTroops(party.Party, requesterCharacter));

        return orderableTroops > 0;
    }

    public bool IsFirstOpponentInRaft(MapEvent mapEvent, BattleSideEnum opponentSide)
        => mapEvent.PartiesOnSide(opponentSide).FirstOrDefault()?.Party.MobileParty?.IsInRaftState == true;

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
