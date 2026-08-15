using Common.Logging;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
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

        if (victim.IsPlayerHero() && !isForced)
        {
            CampaignEventDispatcher.Instance.OnBeforeMainCharacterDied(victim, killer, actionDetail, showNotification);
            return false;
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
                if (victim.Clan.Kingdom != null && victim.Clan.Kingdom.RulingClan == victim.Clan)
                {
                    List<Clan> list = (from t in victim.Clan.Kingdom.Clans
                                       where !t.IsEliminated && t.Leader != victim && !t.IsUnderMercenaryService
                                       select t).ToList<Clan>();
                    if (list.IsEmpty<Clan>())
                    {
                        if (!victim.Clan.Kingdom.IsEliminated)
                        {
                            DestroyKingdomAction.ApplyByKingdomLeaderDeath(victim.Clan.Kingdom);
                        }
                    }
                    else if (!victim.Clan.Kingdom.IsEliminated)
                    {
                        if (list.Count > 1)
                        {
                            Clan clanToExclude = (victim.Clan.Leader == victim || victim.Clan.Leader == null) ? victim.Clan : null;
                            victim.Clan.Kingdom.AddDecision(new KingSelectionKingdomDecision(victim.Clan, clanToExclude), true);
                            if (clanToExclude != null)
                            {
                                Clan randomElementWithPredicate = victim.Clan.Kingdom.Clans.GetRandomElementWithPredicate((Clan t) => t != clanToExclude && Campaign.Current.Models.DiplomacyModel.IsClanEligibleToBecomeRuler(t));
                                ChangeRulingClanAction.Apply(victim.Clan.Kingdom, randomElementWithPredicate);
                            }
                        }
                        else
                        {
                            ChangeRulingClanAction.Apply(victim.Clan.Kingdom, list[0]);
                        }
                    }
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
        KillCharacterAction.MakeDead(victim, true);
        if (victim.GovernorOf != null)
        {
            ChangeGovernorAction.RemoveGovernorOf(victim);
        }

        // TODO: Traits aren't synced
        //if ((actionDetail == KillCharacterAction.KillCharacterActionDetail.Executed || actionDetail == KillCharacterAction.KillCharacterActionDetail.ExecutionAfterMapEvent) && killer == Hero.MainHero && victim.Clan != null && victim.GetTraitLevel(DefaultTraits.Honor) >= 0)
        //{
        //    TraitLevelingHelper.OnLordExecuted();
        //}

        if (victim.Clan != null && !victim.Clan.IsEliminated && !victim.Clan.IsBanditFaction && victim.Clan != Clan.PlayerClan)
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
        if (!victim.IsPlayerHero())
        {
            victim.OnDeath();
        }

        return false;
    }
}
