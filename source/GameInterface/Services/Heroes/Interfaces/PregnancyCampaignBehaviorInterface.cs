using Common.Messaging;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Missions;
using GameInterface.Services.Players;
using GameInterface.Services.UI.Notifications.Messages;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.Core;

namespace GameInterface.Services.Heroes.Interfaces;

public interface IPregnancyCampaignBehaviorInterface : IGameAbstraction
{
    void CheckOffspringToDeliver(PregnancyCampaignBehavior behavior, PregnancyCampaignBehavior.Pregnancy pregnancy);
    bool CheckAreNearby(PregnancyCampaignBehavior behavior, Hero hero, Hero spouse);
}

public class PregnancyCampaignBehaviorInterface : IPregnancyCampaignBehaviorInterface
{
    private readonly IMessageBroker messageBroker;
    private readonly IPlayerManager playerManager;
    private readonly IMissionMembershipRegistry missionMembershipRegistry;

    public PregnancyCampaignBehaviorInterface(
        IMessageBroker messageBroker,
        IPlayerManager playerManager,
        IMissionMembershipRegistry missionMembershipRegistry)
    {
        this.messageBroker = messageBroker;
        this.playerManager = playerManager;
        this.missionMembershipRegistry = missionMembershipRegistry;
    }

    public void CheckOffspringToDeliver(PregnancyCampaignBehavior behavior, PregnancyCampaignBehavior.Pregnancy pregnancy)
    {
        PregnancyModel pregnancyModel = Campaign.Current.Models.PregnancyModel;

        // Don't allow occupied mothers to give birth
        if (!pregnancy.DueDate.IsFuture && pregnancy.Mother.IsAlive && !IsPotentialPlayerParentOccupied(pregnancy.Mother))
        {
            var mother = pregnancy.Mother;
            var isDeliveringTwins = MBRandom.RandomFloat <= pregnancyModel.DeliveringTwinsProbability;
            var deliveredOffspring = new List<Hero>();
            int numberOfOffspringToDeliver = isDeliveringTwins ? 2 : 1;
            int numberOfStillbornOffspring = 0;
            for (int i = 0; i < numberOfOffspringToDeliver; i++)
            {
                if (MBRandom.RandomFloat > pregnancyModel.StillbirthProbability)
                {
                    bool isOffspringFemale = MBRandom.RandomFloat <= pregnancyModel.DeliveringFemaleOffspringProbability;
                    Hero bornChild = HeroCreator.DeliverOffSpring(mother, pregnancy.Father, isOffspringFemale);
                    deliveredOffspring.Add(bornChild);
                }
                else
                {
                    numberOfStillbornOffspring++;

                    // Publish message to show notification on clients
                    var message = new NotifyStillbornDelivery(mother.CharacterObject);
                    messageBroker.Publish(this, message);
                }
            }
            CampaignEventDispatcher.Instance.OnGivenBirth(mother, deliveredOffspring, numberOfStillbornOffspring);
            mother.IsPregnant = false;
            behavior._heroPregnancies.Remove(pregnancy);

            // Replace Hero.MainHero usage
            if (!mother.IsPlayerHero() && MBRandom.RandomFloat <= pregnancyModel.MaternalMortalityProbabilityInLabor)
            {
                KillCharacterAction.ApplyInLabor(mother, true);
            }
        }
    }

    public bool CheckAreNearby(PregnancyCampaignBehavior behavior, Hero hero, Hero spouse)
    {
        // Don't allow occupied players to create pregnancies
        if (IsPotentialPlayerParentOccupied(hero) || IsPotentialPlayerParentOccupied(spouse))
        {
            return false;
        }

        behavior.GetLocation(hero, out var heroSettlement, out var heroParty);
        behavior.GetLocation(spouse, out var spouseSettlement, out var spouseParty);

        return (heroSettlement != null && heroSettlement == spouseSettlement)
            || (heroParty != null && heroParty == spouseParty)
            || (!hero.Clan.IsPlayerClan() && MBRandom.RandomFloat < 1f);//0.2f);
    }

    private bool IsPotentialPlayerParentOccupied(Hero hero)
    {
        if (!hero.IsPlayerHero()) return false;

        if (!PlayerManager.TryGetControlledObjectInfo(hero, out var controlledObject)) return true;

        if (playerManager.IsOwnerOfHeroDisconnected(hero)) return true;

        if (hero.PartyBelongedTo?.MapEvent != null  || hero.PartyBelongedTo?.SiegeEvent != null)
        {
            return true;
        }

        return missionMembershipRegistry?.IsControllerInMission(controlledObject.ObjectControllerId) != false;
    }
}