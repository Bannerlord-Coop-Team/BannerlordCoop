using Common;
using GameInterface.Services.ObjectManager;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Heroes.Commands;

/// <summary>
/// TEST-ONLY cheat (not part of alley sync): maxes a hero's combat skills + attributes + focus so a
/// weak client character can solo the alley take-over fight (Test 1b). Run on the SERVER with the owning
/// client's hero registry id (from coop.debug.alley.my_hero_id or coop.debug.hero.list); skill/attribute
/// changes are server-authoritative (SetSkillXp / ChangeSkillLevelFromXpChange / CharacterDeveloper are
/// patched) and replicate to that client, so its alley-fight character is buffed.
/// </summary>
public class HeroBoostFighterCommand
{
    private static readonly CharacterAttribute[] Attributes =
    {
        DefaultCharacterAttributes.Vigor, DefaultCharacterAttributes.Control, DefaultCharacterAttributes.Endurance,
        DefaultCharacterAttributes.Cunning, DefaultCharacterAttributes.Social, DefaultCharacterAttributes.Intelligence,
    };

    private static readonly SkillObject[] CombatSkills =
    {
        DefaultSkills.OneHanded, DefaultSkills.TwoHanded, DefaultSkills.Polearm, DefaultSkills.Bow,
        DefaultSkills.Crossbow, DefaultSkills.Throwing, DefaultSkills.Riding, DefaultSkills.Athletics,
    };

    [CommandLineArgumentFunction("boost_fighter", "coop.debug.hero")]
    public static string BoostFighter(List<string> args)
    {
        if (ModInformation.IsClient) return "Run coop.debug.hero.boost_fighter on the server (host) only";
        if (args.Count != 1) return "Usage: coop.debug.hero.boost_fighter <heroRegistryId>";

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return "Unable to resolve IObjectManager";
        if (!objectManager.TryGetObject<Hero>(args[0], out var hero))
            return $"Hero '{args[0]}' not found (use coop.debug.alley.my_hero_id on the owning client)";

        var dev = hero.HeroDeveloper;

        // Raise attributes + focus first so the skill soft-cap doesn't hold the skills down, then max
        // each combat skill. checkUnspentPoints/checkUnspentFocusPoints = false bypasses the point gates.
        foreach (var attribute in Attributes) dev.AddAttribute(attribute, 10, false);
        foreach (var skill in CombatSkills)
        {
            dev.AddFocus(skill, 5, false);
            dev.ChangeSkillLevel(skill, 300, false);
        }

        return $"Boosted {hero.Name}: combat skills + attributes + focus maxed (replicating to the owning client)";
    }
}
