using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Heroes.Messages;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(HeroCreator))]
internal class HeroCreatorPatches
{
    internal const string ConfiguredWandererPrefix = "coop_wanderer_";

    [HarmonyPatch(nameof(HeroCreator.InitializeHeroFromSettings))]
    [HarmonyPostfix]
    public static void InitializeHeroFromSettingsPostfix(Hero hero, HeroCreator.HeroInitializationArgs initializationArgs)
    {
        if (ModInformation.IsClient) return;

        ApplyConfiguredWandererName(hero);

        var message = new InitializeNewHero(hero);
        MessageBroker.Instance.Publish(null, message);
    }

    internal static void ApplyConfiguredWandererName(Hero hero)
    {
        var template = hero?.Template;
        if (template?.StringId?.StartsWith(ConfiguredWandererPrefix, StringComparison.Ordinal) != true)
            return;
        if (template.Occupation != Occupation.Wanderer || template.Name == null)
            return;

        var fullName = template.Name.CopyTextObject();
        HeroDataPatches.SetNameOverride(hero, fullName, fullName.CopyTextObject());
    }

    [HarmonyPatch(nameof(HeroCreator.DeliverOffSpring))]
    [HarmonyPrefix]
    public static bool DeliverOffSpringPrefix(ref Hero __result, Hero mother, Hero father, bool isOffspringFemale)
    {
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
        __result = hero;
        return false;
    }
}