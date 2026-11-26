using GameInterface.Configuration;
using Helpers;
using SandBox.AdvancedStartOptions;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;

namespace GameInterface.Services.AdvancedStart;

/// <summary>
/// Gives a joining player a number of adult brothers and sisters in their party. Bannerlord only counts heroes as
/// siblings when they share a parent, and a joining hero has none, so the player first gets a dead father and mother.
/// </summary>
internal sealed class CoopSiblingsModifier : IAdvancedStartModifier
{
    internal const int DefaultMaxSiblings = 3;

    // Both ids name this option's text in global_strings.xml.
    private const string SiblingsOptionId = "CoopSiblings";
    private const string CategoryId = "coop";

    // Years either side of the player's age.
    internal const int MaxSiblingAgeGap = 8;
    // Years older than the player.
    internal const int MinParentAgeGap = 28;
    internal const int MaxParentAgeGap = 36;

    public string OptionId => SiblingsOptionId;

    public int GetLimit(AdvancedStartConfigData config) => Math.Max(0, config?.MaxSiblings ?? DefaultMaxSiblings);

    public AdvancedStartOption CreateOption(int limit) =>
        new IntAdvancedStartOption(SiblingsOptionId, CategoryId, minValue: 0, maxValue: limit, onCondition: null);

    public void Run(Hero hero, int value)
    {
        // Both looked up before anything is created, so a culture missing either leaves the hero untouched.
        var maleTemplates = GetRelativeTemplates(hero.Culture, isFemale: false);
        var femaleTemplates = GetRelativeTemplates(hero.Culture, isFemale: true);

        GiveSiblings(hero, value, maleTemplates, femaleTemplates);
    }

    /// <summary>Gives <paramref name="hero"/> dead parents if they have none, then <paramref name="count"/> adult
    /// siblings in their party, made from the given templates.</summary>
    internal static void GiveSiblings(Hero hero, int count, List<CharacterObject> maleTemplates, List<CharacterObject> femaleTemplates)
    {
        if (hero.Father == null) hero.Father = CreateDeadParent(hero, maleTemplates);
        if (hero.Mother == null) hero.Mother = CreateDeadParent(hero, femaleTemplates);

        for (int i = 0; i < count; i++)
        {
            bool isFemale = MBRandom.RandomFloat < 0.5f;
            var sibling = CreateSibling(hero, isFemale ? femaleTemplates : maleTemplates);
            AddHeroToPartyAction.Apply(sibling, hero.PartyBelongedTo);
        }
    }

    /// <summary>The culture's templates for a relative of this sex, the way coop.debug.clan.add_heirs picks heirs.</summary>
    internal static List<CharacterObject> GetRelativeTemplates(CultureObject culture, bool isFemale)
    {
        List<CharacterObject> templates;
        // Culture lord templates are all male, so women use the culture's female wanderer templates.
        if (isFemale)
        {
            templates = CharacterObject.All
                .Where(character => character.IsTemplate && character.IsFemale &&
                                    character.Occupation == Occupation.Wanderer && character.Culture == culture)
                .ToList();
        }
        else
        {
            templates = culture.LordTemplates.Where(template => template != null).ToList();
        }

        if (templates.Count == 0)
        {
            string kind = isFemale ? "female wanderer" : "lord";
            throw new InvalidOperationException($"Culture {culture.StringId} has no {kind} templates to make a relative from");
        }

        return templates;
    }

    private static Hero CreateDeadParent(Hero hero, List<CharacterObject> templates)
    {
        int parentAge = (int)hero.Age + MBRandom.RandomInt(MinParentAgeGap, MaxParentAgeGap + 1);
        var parent = CreateRelative(hero, templates, parentAge);

        // Forced, since a hero the campaign options keep alive would otherwise refuse to die.
        KillCharacterAction.ApplyInternal(parent, null, KillCharacterAction.KillCharacterActionDetail.DiedOfOldAge,
            showNotification: false, isForced: true);
        return parent;
    }

    private static Hero CreateSibling(Hero hero, List<CharacterObject> templates)
    {
        int ageGap = MBRandom.RandomInt(-MaxSiblingAgeGap, MaxSiblingAgeGap + 1);
        int adultAge = Campaign.Current.Models.AgeModel.HeroComesOfAge;
        int siblingAge = Math.Max(adultAge, (int)hero.Age + ageGap);

        var sibling = CreateRelative(hero, templates, siblingAge);
        sibling.Father = hero.Father;
        sibling.Mother = hero.Mother;
        sibling.ChangeState(Hero.CharacterStates.Active);
        return sibling;
    }

    // A lord of the player's culture and clan, made from a random template.
    private static Hero CreateRelative(Hero hero, List<CharacterObject> templates, int age)
    {
        var template = templates[MBRandom.RandomInt(templates.Count)];
        var bornSettlement = hero.HomeSettlement ?? hero.Clan.HomeSettlement;

        var relative = HeroCreator.CreateSpecialHero(template, bornSettlement, hero.Clan, age: age);
        // Vanilla ignores the age for a wanderer template and uses the template's own, so set it again.
        relative.SetBirthDay(HeroHelper.GetRandomBirthDayForAge(age));
        relative.SetNewOccupation(Occupation.Lord);

        // Vanilla names a wanderer after its template, like "the Smith", so name them again as a lord.
        var (firstName, fullName) = Campaign.Current.Models.HeroCreationModel.GenerateFirstAndFullName(relative);
        relative.SetName(fullName, firstName);
        return relative;
    }
}
