using Common.Messaging;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Missions;
using GameInterface.Services.Players;
using GameInterface.Services.UI.Notifications.Messages;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

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
                    Hero bornChild = DeliverOffSpring(mother, pregnancy.Father, isOffspringFemale);
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
            || (!hero.Clan.IsPlayerClan() && MBRandom.RandomFloat < 0.2f);
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

    private Hero DeliverOffSpring(Hero mother, Hero father, bool isOffspringFemale)
    {
        RepairMissingParentCharacterData(mother.CharacterObject);
        RepairMissingParentCharacterData(father.CharacterObject);

        CharacterObject characterTemplateForOffspring = Campaign.Current.Models.HeroCreationModel.GetCharacterTemplateForOffspring(mother, father, isOffspringFemale);
        ValueTuple<CampaignTime, CampaignTime> birthAndDeathDay = Campaign.Current.Models.HeroCreationModel.GetBirthAndDeathDay(characterTemplateForOffspring, true, 0);
        CampaignTime birthDay = birthAndDeathDay.Item1;
        CampaignTime deathDay = birthAndDeathDay.Item2;
        Hero hero = HeroCreator.CreateHero(characterTemplateForOffspring, true, birthDay, deathDay);
        HeroCreator.HeroInitializationArgs heroInitializationArgs = new HeroCreator.HeroInitializationArgs(hero, true)
            .SetMother(mother)
            .SetFather(father)
            .SetIsFemale(isOffspringFemale)
            .SetOccupation(isOffspringFemale ? mother.Occupation : father.Occupation)
            .SetLevel(1)
            .SetGenerateFirstAndFullName(true);

        // Replace Hero.MainHero usage to match closer to vanilla's culture assignment when only one parent is a player
        if (mother.IsPlayerHero() && !father.IsPlayerHero())
        {
            heroInitializationArgs.SetClan(mother.Clan).SetCulture(mother.Culture);
        }
        else if (!mother.IsPlayerHero() && father.IsPlayerHero())
        {
            heroInitializationArgs.SetClan(father.Clan).SetCulture(father.Culture);
        }
        else
        {
            // Children with only NPC parents or only player parents choose culture randomly
            // Married players are always in the same clan so using the father's clan is fine
            CultureObject culture = (MBRandom.RandomFloat < 0.5f) ? father.Culture : mother.Culture;
            heroInitializationArgs.SetClan(father.Clan).SetCulture(culture);
        }

        HeroCreator.InitializeHeroFromSettings(heroInitializationArgs.Hero, heroInitializationArgs);
        return hero;
    }

    private static void RepairMissingParentCharacterData(CharacterObject parentCharacter)
    {
        if (parentCharacter._culture == null)
        {
            // Use patched getter to pick up the hero culture fallback
            var culture = parentCharacter.Culture;
            if (culture != null)
            {
                parentCharacter.Culture = culture;
            }
        }

        if (parentCharacter.BodyPropertyRange == null)
        {
            // Vanilla player characters use the main_hero range
            var bodyPropertyRange = parentCharacter.OriginalCharacter?.BodyPropertyRange
                ?? MBObjectManager.Instance.GetObject<CharacterObject>("main_hero")?.BodyPropertyRange;
            if (bodyPropertyRange != null)
            {
                // Repair template's properties for later reads during hero creation
                parentCharacter.BodyPropertyRange = bodyPropertyRange;
            }
        }
    }
}