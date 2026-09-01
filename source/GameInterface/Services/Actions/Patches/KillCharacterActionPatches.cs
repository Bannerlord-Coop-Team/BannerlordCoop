using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;

namespace GameInterface.Services.Actions.Patches;

/// <summary>
/// Replace vanilla implementation entirely.
/// It is too problematic to keep with all of its references to MainHero and MainParty.
/// </summary>
[HarmonyPatch(typeof(KillCharacterAction))]
internal class KillCharacterActionPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<KillCharacterActionPatches>();

    [HarmonyPatch(nameof(KillCharacterAction.ApplyInternal))]
    [HarmonyPrefix]
    public static bool ApplyInternalPrefix(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail actionDetail, bool showNotification, bool isForced = false)
    {
        if (!victim.CanDie(actionDetail) && !isForced) return false;

        if (!victim.IsAlive) return false;

        MobileParty partyBelongedTo = victim.PartyBelongedTo;
        bool skipDeathMarkCheck = false;
        if ((partyBelongedTo?.MapEvent) == null)
        {
            if ((partyBelongedTo?.SiegeEvent) == null && actionDetail != KillCharacterAction.KillCharacterActionDetail.ExecutionAfterMapEvent)
            {
                skipDeathMarkCheck = true;
            }
        }

        if (!skipDeathMarkCheck && victim.DeathMark == KillCharacterAction.KillCharacterActionDetail.None)
        {
            victim.AddDeathMark(killer, actionDetail);
            return false;
        }

        bool isPlayerHero = victim.IsPlayerHero();
        if (isPlayerHero)
        {
            CampaignEventDispatcher.Instance.OnBeforeMainCharacterDied(victim, killer, actionDetail, showNotification);
        }

        CampaignEventDispatcher.Instance.OnBeforeHeroKilled(victim, killer, actionDetail, showNotification);
        victim.AddDeathMark(killer, actionDetail);
        victim.EncyclopediaText = KillCharacterAction.CreateObituary(victim, actionDetail);
        if (victim.Clan != null)
        {
            if (victim.Clan.Leader == victim || victim.IsPlayerHero())
            {
                if (!victim.Clan.IsEliminated && !victim.IsPlayerHero() && victim.Clan.Heroes.Any(x => !x.IsChild && x != victim && x.IsAlive && x.IsLord))
                {
                    ChangeClanLeaderAction.ApplyWithoutSelectedNewLeader(victim.Clan);
                }
                if (!isPlayerHero)
                {
                    HandleKingdomLeaderDeath(victim);
                }
            }
            else
            {
                GiveGoldAction.ApplyBetweenCharacters(victim, victim.Clan.Leader, victim.Gold, false);
            }
        }
        if (victim.PartyBelongedTo != null && (victim.PartyBelongedTo.LeaderHero == victim || victim.IsPlayerHero()))
        {
            MobileParty partyBelongedTo3 = victim.PartyBelongedTo;
            if (victim.PartyBelongedTo.Army != null)
            {
                if (victim.PartyBelongedTo.Army.LeaderParty == victim.PartyBelongedTo)
                {
                    DisbandArmyAction.ApplyByArmyLeaderIsDead(victim.PartyBelongedTo.Army);
                }
                else
                {
                    victim.PartyBelongedTo.Army = null;
                }
            }
            if (!partyBelongedTo3.IsPlayerParty())
            {
                partyBelongedTo3.SetMoveModeHold();
                if (victim.Clan != null && victim.Clan.IsRebelClan)
                {
                    DestroyPartyAction.Apply(null, partyBelongedTo3);
                }
            }
        }
        // Preserve party in an inactive state until heir selection is confirmed
        // This also prevents other parties interacting with a player's party
        // who still needs to select an heir
        if (isPlayerHero && partyBelongedTo != null)
        {
            partyBelongedTo.IsActive = false;
        }

        KillCharacterAction.MakeDead(victim, !isPlayerHero);
        if (victim.GovernorOf != null)
        {
            ChangeGovernorAction.RemoveGovernorOf(victim);
        }

        // TODO: Traits aren't synced
        //if ((actionDetail == KillCharacterAction.KillCharacterActionDetail.Executed || actionDetail == KillCharacterAction.KillCharacterActionDetail.ExecutionAfterMapEvent) && killer == Hero.MainHero && victim.Clan != null && victim.GetTraitLevel(DefaultTraits.Honor) >= 0)
        //{
        //    TraitLevelingHelper.OnLordExecuted();
        //}

        if (victim.Clan != null && !victim.Clan.IsEliminated && !victim.Clan.IsBanditFaction && !victim.Clan.IsPlayerClan())
        {
            if (victim.Clan.Leader == victim)
            {
                DestroyClanAction.ApplyByClanLeaderDeath(victim.Clan);
            }
            else if (victim.Clan.Leader == null)
            {
                DestroyClanAction.Apply(victim.Clan);
            }
        }
        CampaignEventDispatcher.Instance.OnHeroKilled(victim, killer, actionDetail, showNotification);
        if (victim.Spouse != null)
        {
            victim.Spouse = null;
        }
        if (victim.CompanionOf != null)
        {
            RemoveCompanionAction.ApplyByDeath(victim.CompanionOf, victim);
        }
        if (victim.CurrentSettlement != null)
        {
            if (victim.CurrentSettlement == Settlement.CurrentSettlement)
            {
                LocationComplex locationComplex = LocationComplex.Current;
                locationComplex?.RemoveCharacterIfExists(victim);
            }
            if (victim.StayingInSettlement != null)
            {
                victim.StayingInSettlement = null;
            }
        }
        if (isPlayerHero)
        {
            MessageBroker.Instance.Publish(victim, new PlayerHeirSelectionRequested(victim));
        }
        else
        {
            victim.OnDeath();
        }

        return false;
    }

    public static void HandleKingdomLeaderDeath(Hero victim)
    {
        Clan victimClan = victim?.Clan;
        Kingdom kingdom = victimClan?.Kingdom;
        if (kingdom?.RulingClan != victimClan) return;

        List<Clan> eligibleClans = (from clan in kingdom.Clans
                                    where !clan.IsEliminated && clan.Leader != victim && !clan.IsUnderMercenaryService
                                    select clan).ToList();
        if (eligibleClans.IsEmpty())
        {
            if (!kingdom.IsEliminated)
            {
                DestroyKingdomAction.ApplyByKingdomLeaderDeath(kingdom);
            }
        }
        else if (!kingdom.IsEliminated)
        {
            if (eligibleClans.Count > 1)
            {
                Clan clanToExclude = victimClan.Leader == victim || victimClan.Leader == null ? victimClan : null;
                Clan decisionProposerClan = victimClan;
                if (clanToExclude != null)
                {
                    Clan newRulingClan = kingdom.Clans.GetRandomElementWithPredicate(
                        clan => clan != clanToExclude && Campaign.Current.Models.DiplomacyModel.IsClanEligibleToBecomeRuler(clan));
                    ChangeRulingClanAction.Apply(kingdom, newRulingClan);

                    // Use new ruler clan as proposer. Using destroyed clan instantly resolves decision
                    decisionProposerClan = newRulingClan;
                }
                kingdom.AddDecision(new KingSelectionKingdomDecision(decisionProposerClan, clanToExclude), true);
            }
            else
            {
                ChangeRulingClanAction.Apply(kingdom, eligibleClans[0]);
            }
        }
    }
}
