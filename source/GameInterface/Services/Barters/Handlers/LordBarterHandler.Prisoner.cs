using GameInterface.Services.Barters.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Barters.Handlers;

internal sealed partial class LordBarterHandler
{
    private const string PrisonerNotHeldReason = "You can only recruit prisoners you hold.";
    private const string PlayerPartyInBattleReason = "Your party is in a battle.";

    /// <summary>
    /// A talk with a prisoner the requester's party holds has nothing to lock; custody is the authority.
    /// </summary>
    private bool IsHeldByRequesterParty(
        NetworkRequestLordBarter request, MobileParty mobileParty, Hero targetHero, ref string reason)
    {
        if (!objectManager.TryGetObject(request.ContextId, out PartyBase captor) ||
            captor != mobileParty.Party ||
            !targetHero.IsPrisoner ||
            targetHero.PartyBelongedToAsPrisoner != captor)
        {
            return false;
        }

        reason = null;
        return true;
    }

    /// <summary>
    /// Mirrors conversation_player_start_defection_with_prisoner_on_condition, with the requester in
    /// place of the main hero.
    /// </summary>
    private static bool IsPrisonerRecruitableBy(
        Hero playerHero, MobileParty mobileParty, Hero targetHero, NetworkRequestLordBarter request, out string reason)
    {
        if ((LordBarterKind)request.Kind != LordBarterKind.JoinKingdomAsClan)
        {
            reason = "A prisoner can only be offered to join your kingdom.";
            return false;
        }

        if (!playerHero.IsKingdomLeader)
        {
            reason = "Only a kingdom leader can recruit a prisoner.";
            return false;
        }

        if (targetHero.Clan == targetHero.Clan.Kingdom?.RulingClan)
        {
            reason = "A lord of the ruling clan cannot be recruited.";
            return false;
        }

        // Vanilla gets co-location from the talk itself; the Location hold does not check it.
        if (targetHero.PartyBelongedToAsPrisoner != mobileParty.Party &&
            (mobileParty.CurrentSettlement == null ||
             targetHero.CurrentSettlement != mobileParty.CurrentSettlement ||
             targetHero.CurrentSettlement.OwnerClan != playerHero.Clan))
        {
            reason = PrisonerNotHeldReason;
            return false;
        }

        // Vanilla hides the offer in the post-battle CapturedLord conversation, and a siege assault can
        // start while a dungeon barter is open.
        if (mobileParty.MapEvent != null)
        {
            reason = PlayerPartyInBattleReason;
            return false;
        }

        reason = null;
        return true;
    }
}
