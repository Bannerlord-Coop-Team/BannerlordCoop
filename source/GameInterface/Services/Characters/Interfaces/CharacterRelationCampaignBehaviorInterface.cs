using Common.Messaging;
using Common.Network;
using GameInterface.Extentions;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.UI.Notifications.Messages;
using Helpers;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Characters.Interfaces;

public interface ICharacterRelationCampaignBehaviorInterface : IGameAbstraction
{
    void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification = true);
    void OnPrisonerDonatedToSettlement(MobileParty donatingParty, FlattenedTroopRoster donatedPrisoners, Settlement donatedSettlement);
    void DailyTick();
    void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail);
    void OnRaidCompleted(BattleSideEnum winnerSide, RaidEventComponent raidEvent);
}

public class CharacterRelationCampaignBehaviorInterface : ICharacterRelationCampaignBehaviorInterface
{
    private readonly IMessageBroker messageBroker;

    public CharacterRelationCampaignBehaviorInterface(
        IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
    }

    public void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification = true)
    {
        // Replace Hero.MainHero check
        if ((detail != KillCharacterAction.KillCharacterActionDetail.Executed
            && detail != KillCharacterAction.KillCharacterActionDetail.ExecutionAfterMapEvent)
            || !killer.IsPlayerHero() || victim.Clan == null) return;
        
        int numberOfClansWithHurtRelations = 0;
        foreach (Clan clan in Clan.All)
        {
            if (!clan.IsEliminated && !clan.IsBanditFaction && clan != killer.Clan)
            {
                int relationChangeForExecutingHero = Campaign.Current.Models.ExecutionRelationModel.GetRelationChangeForExecutingHero(victim, clan.Leader, out bool showQuickNotification);
                if (relationChangeForExecutingHero != 0)
                {
                    Hero leader = clan.Leader;
                    ResolvedMainHeroContext.ResolvedMainHero = killer;
                    ChangeRelationAction.ApplyPlayerRelation(leader, relationChangeForExecutingHero, true, false);
                    if (showQuickNotification)
                    {
                        numberOfClansWithHurtRelations++;

                        // Notify relation decreased with clan
                        var message = new NotifyRelationDecreasedByExecution(killer, clan, leader.GetRelation(killer), MathF.Abs(relationChangeForExecutingHero));
                        messageBroker.Publish(this, message);
                    }
                }
            }
        }
        if (numberOfClansWithHurtRelations > 0)
        {
            // Notify execution hurt relations summary
            var message = new NotifyRelationDecreasedByExecutionSummary(killer, numberOfClansWithHurtRelations);
            messageBroker.Publish(this, message);
        }
    }

    public void OnPrisonerDonatedToSettlement(MobileParty donatingParty, FlattenedTroopRoster donatedPrisoners, Settlement donatedSettlement)
    {
        // Replace IsMainParty check
        if (!donatingParty.IsPlayerParty()) return;

        foreach (FlattenedTroopRosterElement flattenedTroopRosterElement in donatedPrisoners)
        {
            if (!flattenedTroopRosterElement.Troop.IsHero) continue;

            float relationGain = Campaign.Current.Models.PrisonerDonationModel.CalculateRelationGainAfterHeroPrisonerDonate(donatingParty.Party, flattenedTroopRosterElement.Troop.HeroObject, donatedSettlement);
            if (relationGain != 0f)
            {
                // Change to work for any donating player, not just Hero.MainHero
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(donatingParty.ActualClan.Leader, donatedSettlement.OwnerClan.Leader, (int)relationGain, true);
            }
        }
    }

    public void DailyTick()
    {
        var playerSettlementOwnerRelationChanges = new Dictionary<Hero, (bool, bool)>();

        // Iterate over all player heroes to handle multiple players
        foreach (var playerHero in Campaign.Current.CampaignObjectManager.GetPlayerHeroes())
        {
            playerSettlementOwnerRelationChanges[playerHero] = (false, false);

            if (playerHero.CurrentSettlement != null && playerHero.GetPerkValue(DefaultPerks.Charm.ForgivableGrievances) && MBRandom.RandomFloat < DefaultPerks.Charm.ForgivableGrievances.SecondaryBonus)
            {
                MBList<Hero> heroesToIncreaseRelations = new MBList<Hero>();
                foreach (Hero hero in SettlementHelper.GetAllHeroesOfSettlement(playerHero.CurrentSettlement, true))
                {
                    if (!hero.IsPlayerHero() && hero.GetRelationWithPlayer() < 0f)
                    {
                        heroesToIncreaseRelations.Add(hero);
                    }
                }
                if (heroesToIncreaseRelations.Count > 0)
                {
                    var heroGainedRelationWith = heroesToIncreaseRelations.GetRandomElement<Hero>();
                    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(playerHero, heroGainedRelationWith, 1, true);
                }
            }
        }

        // Iterate over settlements applying power & relation changes. Also compute which players need to be notified of relation changes
        SettlementLoyaltyModel settlementLoyaltyModel = Campaign.Current.Models.SettlementLoyaltyModel;
        SettlementSecurityModel settlementSecurityModel = Campaign.Current.Models.SettlementSecurityModel;
        bool shouldNotifyPlayers = false;
        foreach (Settlement settlement in Settlement.All)
        {
            if (settlement.IsTown)
            {
                if (settlement.Town.Security >= (float)settlementSecurityModel.ThresholdForNotableRelationBonus)
                {
                    foreach (var notable in settlement.Notables)
                    {
                        if ((notable.IsArtisan || notable.IsMerchant) && MBRandom.RandomFloat < 0.05f)
                        {
                            var owner = settlement.OwnerClan.Leader;
                            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(owner, notable, settlementSecurityModel.DailyNotableRelationBonus, false);

                            if (playerSettlementOwnerRelationChanges.ContainsKey(owner))
                            {
                                playerSettlementOwnerRelationChanges[owner] = (playerSettlementOwnerRelationChanges[owner].Item1, true);
                                shouldNotifyPlayers = true;
                            }
                        }
                    }
                    continue;
                }
                if (settlement.Town.Security >= (float)settlementSecurityModel.ThresholdForNotableRelationPenalty)
                {
                    continue;
                }
                foreach (Hero notable in settlement.Notables)
                {
                    if ((notable.IsArtisan || notable.IsMerchant) && MBRandom.RandomFloat < 0.05f)
                    {
                        notable.AddPower((float)settlementSecurityModel.DailyNotablePowerPenalty);
                        ChangeRelationAction.ApplyRelationChangeBetweenHeroes(settlement.OwnerClan.Leader, notable, settlementSecurityModel.DailyNotableRelationPenalty, false);
                    }
                }
                foreach (var notable in settlement.Notables)
                {
                    if (notable.IsGangLeader && MBRandom.RandomFloat < 0.05f)
                    {
                        notable.AddPower((float)settlementSecurityModel.DailyNotablePowerBonus);
                    }
                }
                continue;
            }
            if (settlement.IsVillage && settlement.Village.Bound.Town.Loyalty >= settlementLoyaltyModel.ThresholdForNotableRelationBonus)
            {
                foreach (Hero notable in settlement.Notables)
                {
                    if ((notable.IsHeadman || notable.IsRuralNotable) && MBRandom.RandomFloat < 0.05f)
                    {
                        var owner = settlement.OwnerClan.Leader;
                        ChangeRelationAction.ApplyRelationChangeBetweenHeroes(settlement.OwnerClan.Leader, notable, settlementLoyaltyModel.DailyNotableRelationBonus, false);

                        if (playerSettlementOwnerRelationChanges.ContainsKey(owner))
                        {
                            playerSettlementOwnerRelationChanges[owner] = (true, playerSettlementOwnerRelationChanges[owner].Item2);
                            shouldNotifyPlayers = true;
                        }
                    }
                }
            }
        }

        // Notify players of changed relations in their settlements if there are any changes
        if (shouldNotifyPlayers)
        {
            var message = new NotifyRelationsIncreasedWithNotables(playerSettlementOwnerRelationChanges);
            messageBroker.Publish(this, message);
        }
    }

    public void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
    {
        if ((detail == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege 
            || detail == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByBarter)
            && oldOwner != null && oldOwner.MapFaction != null && oldOwner.MapFaction.Leader != oldOwner
            && oldOwner.IsAlive && !oldOwner.MapFaction.Leader.IsPlayerHero()) // Replace Hero.MainHero check
        {
            float settlementValue = settlement.GetValue(null, true);
            int relationChange = (int)((1f + MathF.Max(1f, MathF.Sqrt(settlementValue / 100000f))) * ((newOwner.MapFaction != oldOwner.MapFaction) ? 1f : 0.5f));
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(oldOwner, oldOwner.MapFaction.Leader, -relationChange, false);
            if (capturerHero != null && capturerHero.Clan != capturerHero.MapFaction.Leader.Clan)
            {
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(capturerHero, capturerHero.MapFaction.Leader, relationChange / 2, false);
            }
            if (oldOwner.Clan != null && settlement != null)
            {
                ChangeClanInfluenceAction.Apply(oldOwner.Clan, (float)(settlement.IsTown ? -50 : -25));
            }
        }
    }

    public void OnRaidCompleted(BattleSideEnum winnerSide, RaidEventComponent raidEvent)
    {
        MapEvent mapEvent = raidEvent.MapEvent;
        PartyBase attackerParty = mapEvent.AttackerSide.LeaderParty;
        Hero hero = attackerParty?.LeaderHero;
        PartyBase defenderLeaderParty = mapEvent.DefenderSide.LeaderParty;

        if (attackerParty == null || attackerParty.MapFaction == mapEvent.MapEventSettlement.MapFaction) return;

        if (winnerSide == BattleSideEnum.Attacker && hero != null && defenderLeaderParty != null && defenderLeaderParty.IsSettlement
            && defenderLeaderParty.Settlement.IsVillage && !defenderLeaderParty.Settlement.OwnerClan.IsPlayerClan()) // Replace IsPlayerClan check
        {
            int num = -MathF.Ceiling(6f * raidEvent.RaidDamage);
            int num2 = -MathF.Ceiling(6f * raidEvent.RaidDamage * 0.5f);
            if (num < 0)
            {
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(hero, defenderLeaderParty.Settlement.OwnerClan.Leader, num, true);
            }
            if (num2 < 0)
            {
                foreach (Hero gainedRelationWith in defenderLeaderParty.Settlement.Notables)
                {
                    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(hero, gainedRelationWith, num2, true);
                }
            }
        }
    }
}