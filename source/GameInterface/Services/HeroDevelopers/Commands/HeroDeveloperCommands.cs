using Common.Commands;
using Common;
using Common.Logging;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.Commands;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using Serilog;
using System.Collections.Generic;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.HeroDevelopers.Commands;

internal class HeroDeveloperCommands
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    private static readonly ILogger Logger = LogManager.GetLogger<HeroDeveloperCommands>();

    /// <summary>
    /// Add skill xp to a hero with a skill object name.
    /// Examples:
    /// coop.debug.herodeveloper.addskillxp RandomPlayer OneHanded 3000
    /// </summary>
    public sealed class HeroDeveloperAddSkillXpCoopCommand : ICoopCommand
    {
        // Resolves hero arguments by name or id.
        private readonly ICommandObjectResolver resolver;

        public HeroDeveloperAddSkillXpCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.hero_developer";

        public string Name => "add_skill_xp";

        public string Description => "Runs the add skill xp debug operation.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_name_or_id", "Hero name or id; quote names with spaces.", isRequired: true),
            new ExpectedArgs("skill_name", "The skill id, such as OneHanded or Riding; skills from other mods work too.", isRequired: true),
            new ExpectedArgs("xp_amount", "The amount of skill experience.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            if (ModInformation.IsClient) return Failed("Command can only be run on the server.");


            SkillObject skillObject = GetSkillByName(strings[1]);
            if (skillObject == null) return Failed(UnknownSkillError(strings[1]));

            if (!int.TryParse(strings[2], out int xpGain)) return Failed("An integer amount of xp is required.");

            if (!resolver.TryResolveHero(strings[0], out var hero, out var error)) return Failed(error);

            hero.AddSkillXp(skillObject, xpGain);

            return Succeeded($"{strings[0]} was given {xpGain} xp for {skillObject.Name}");
        }
    }

    /// <summary>
    /// Add attribute points to a hero.
    /// Example:
    /// coop.debug.herodeveloper.addattributepoints RandomPlayer 10
    /// </summary>
    public sealed class HeroDeveloperAddAttributePointsCoopCommand : ICoopCommand
    {
        // Resolves hero arguments by name or id.
        private readonly ICommandObjectResolver resolver;

        public HeroDeveloperAddAttributePointsCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.hero_developer";

        public string Name => "add_attribute_points";

        public string Description => "Runs the add attribute points debug operation.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_name_or_id", "Hero name or id; quote names with spaces.", isRequired: true),
            new ExpectedArgs("point_count", "The number of attribute points.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            if (ModInformation.IsClient) return Failed("Command can only be run on the server.");


            if (!int.TryParse(strings[1], out int numPoints)) return Failed("An integer amount of attribute points is required.");

            if (!resolver.TryResolveHero(strings[0], out var hero, out var error)) return Failed(error);

            // Use same implementation as vanilla command
            hero.HeroDeveloper.UnspentAttributePoints = MBMath.ClampInt(hero.HeroDeveloper.UnspentAttributePoints + numPoints, 0, 10000);

            return Succeeded($"{strings[0]} was given {numPoints} attribute points.");
        }
    }

    /// <summary>
    /// Add focus points to a hero.
    /// Example:
    /// coop.debug.herodeveloper.addfocuspoints RandomPlayer 10
    /// </summary>
    public sealed class HeroDeveloperAddFocusPointsCoopCommand : ICoopCommand
    {
        // Resolves hero arguments by name or id.
        private readonly ICommandObjectResolver resolver;

        public HeroDeveloperAddFocusPointsCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.hero_developer";

        public string Name => "add_focus_points";

        public string Description => "Runs the add focus points debug operation.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_name_or_id", "Hero name or id; quote names with spaces.", isRequired: true),
            new ExpectedArgs("point_count", "The number of focus points.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            if (ModInformation.IsClient) return Failed("Command can only be run on the server.");


            if (!int.TryParse(strings[1], out int numPoints)) return Failed("An integer amount of focus points is required.");

            if (!resolver.TryResolveHero(strings[0], out var hero, out var error)) return Failed(error);

            // Use same implementation as vanilla command
            hero.HeroDeveloper.UnspentFocusPoints = MBMath.ClampInt(hero.HeroDeveloper.UnspentFocusPoints + numPoints, 0, 10000);

            return Succeeded($"{strings[0]} was given {numPoints} focus points.");
        }
    }

    /// <summary>
    /// Reset all skills of a hero and give focus/attribute points back based on level.
    /// Example:
    /// coop.debug.herodeveloper.resetskills
    /// </summary>
    public sealed class HeroDeveloperResetSkillsCoopCommand : ICoopCommand
    {
        // Resolves hero arguments by name or id.
        private readonly ICommandObjectResolver resolver;

        public HeroDeveloperResetSkillsCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.hero_developer";

        public string Name => "reset_skills";

        public string Description => "Runs the reset skills debug operation.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_name_or_id", "Hero name or id; quote names with spaces.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            if (ModInformation.IsClient) return Failed("Command can only be run on the server.");


            if (!resolver.TryResolveHero(strings[0], out var hero, out var error)) return Failed(error);

            hero.HeroDeveloper.ResetCharacterStats();

            return Succeeded($"{strings[0]}'s skills were reset.");
        }
    }

    /// <summary>
    /// Set one skill of a hero to a level, lower or higher, with its skill xp to match.
    /// Example:
    /// coop.debug.hero_developer.set_skill RandomPlayer Riding 50 120
    /// </summary>
    public sealed class HeroDeveloperSetSkillCoopCommand : ICoopCommand
    {
        // DefaultCharacterDevelopmentModel only has xp thresholds up to 1023
        private const int MaxSkillLevel = 1023;

        // Resolves hero arguments by name or id.
        private readonly ICommandObjectResolver resolver;
        private readonly IObjectManager objectManager;

        public HeroDeveloperSetSkillCoopCommand(ICommandObjectResolver resolver, IObjectManager objectManager)
        {
            this.resolver = resolver;
            this.objectManager = objectManager;
        }

        public string Prefix => "coop.debug.hero_developer";

        public string Name => "set_skill";

        public string Description => "Sets a hero's skill and its skill xp to a level if the current level still matches.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_name_or_id", "Hero name or id; quote names with spaces.", isRequired: true),
            new ExpectedArgs("skill_name", "The skill id, such as OneHanded or Riding; skills from other mods work too.", isRequired: true),
            new ExpectedArgs("level", $"The new skill level, 0 to {MaxSkillLevel}.", isRequired: true),
            new ExpectedArgs("expected_level", "The current skill level, as shown by coop.debug.hero_developer.stats.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            if (ModInformation.IsClient) return Failed("Command can only be run on the server.");

            SkillObject skillObject = GetSkillByName(strings[1]);
            if (skillObject == null) return Failed(UnknownSkillError(strings[1]));

            if (!int.TryParse(strings[2], out var level) || level < 0 || level > MaxSkillLevel)
                return Failed($"Use a level from 0 to {MaxSkillLevel}.");
            if (!int.TryParse(strings[3], out var expected)) return Failed("An integer expected level is required.");

            if (!resolver.TryResolveHero(strings[0], out var hero, out var error)) return Failed(error);
            if (!objectManager.TryGetId(hero, out var heroId)) return Failed($"Hero {hero.Name} is not registered.");
            if (hero.HeroDeveloper == null) return Failed($"Hero {hero.Name} has no hero developer.");

            var before = hero.GetSkillValue(skillObject);
            if (expected != before) return Failed($"Expected level {expected} but the current level is {before}.");

            // Same calls as vanilla campaign.set_skill_player, so leftover skill xp cant raise the level again.
            // Perks, hero level and total xp are left as they are, like vanilla.
            hero.HeroDeveloper.SetInitialSkillLevel(skillObject, level);

            return Succeeded(JsonConvert.SerializeObject(new { heroId, skill = skillObject.StringId, before, value = hero.GetSkillValue(skillObject) }));
        }
    }

    public sealed class HeroSocialParameterCoopCommand : ICoopCommand
    {
        // Resolves hero arguments by name or id.
        private readonly ICommandObjectResolver resolver;
        private readonly IObjectManager objectManager;

        public HeroSocialParameterCoopCommand(ICommandObjectResolver resolver, IObjectManager objectManager)
        {
            this.resolver = resolver;
            this.objectManager = objectManager;
        }

        public string Prefix => "coop.debug.hero_developer";
        public string Name => "social_parameter";
        public string Description => "Read a hero's social parameter, or set it on the server only if its previous value still matches.";
        public CoopCommandSide Side => CoopCommandSide.Both;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "Hero name or id; quote names with spaces.", isRequired: true),
            new ExpectedArgs("parameter", "charm, mercy, female, persona_curt, persona_ironic, in_bloom, young_and_respectful, good_natured or tribute.", isRequired: true),
            new ExpectedArgs("value", "Optional integer value; booleans use 0 or 1.", isRequired: false),
            new ExpectedArgs("expected_value", "The previously read value, required for every write and restore.", isRequired: false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (args.Count != 2 && args.Count != 4)
                return Failed("Read with hero id and parameter; write with value and expected previous value.");
            if (args.Count == 4 && !CommandHelpers.IsServerOnlyCommand(out var sideError, "coop.debug.hero_developer.social_parameter"))
                return Failed(sideError);
            if (!resolver.TryResolveHero(args[0], out var hero, out var error)) return Failed(error);
            if (!objectManager.TryGetId(hero, out var heroId)) return Failed($"Hero {hero.Name} is not registered.");
            var name = args[1];
            var trait = name switch
            {
                "mercy" => DefaultTraits.Mercy,
                "persona_curt" => DefaultTraits.PersonaCurt,
                "persona_ironic" => DefaultTraits.PersonaIronic,
                _ => null,
            };
            var perk = name switch
            {
                "in_bloom" => DefaultPerks.Charm.InBloom,
                "young_and_respectful" => DefaultPerks.Charm.YoungAndRespectful,
                "good_natured" => DefaultPerks.Charm.GoodNatured,
                "tribute" => DefaultPerks.Charm.Tribute,
                "oratory" => DefaultPerks.Charm.Oratory,
                _ => null,
            };
            if (trait == null && perk == null && name != "charm" && name != "female")
                return Failed("Unknown social parameter.");
            int ReadValue() => trait != null ? hero.GetTraitLevel(trait)
                : perk != null ? (hero.GetPerkValue(perk) ? 1 : 0)
                : name == "charm" ? hero.GetSkillValue(DefaultSkills.Charm) : (hero.IsFemale ? 1 : 0);
            var before = ReadValue();
            if (args.Count == 4)
            {
                var minimum = trait?.MinValue ?? 0;
                var maximum = trait?.MaxValue ?? (name == "charm" ? 1023 : 1);
                if (!int.TryParse(args[2], out var value) || value < minimum || value > maximum ||
                    !int.TryParse(args[3], out var expected) || expected != before)
                    return Failed($"Use a value from {minimum} to {maximum} and the current expected value {before}.");
                if (trait != null) hero.SetTraitLevel(trait, value);
                else if (perk != null) hero.SetPerkValueInternal(perk, value == 1);
                else if (name == "charm") hero.SetSkillValue(DefaultSkills.Charm, value);
                else hero.IsFemale = value == 1;
            }
            return Succeeded(JsonConvert.SerializeObject(new { heroId, parameter = name, before, value = ReadValue() }));
        }
    }

    // Every skill is in the object manager under its StringId, vanilla (OneHanded) and DLC or mod skills alike.
    private static MBReadOnlyList<SkillObject> AllSkills => MBObjectManager.Instance.GetObjectTypeList<SkillObject>();

    private static SkillObject GetSkillByName(string skillName) =>
        AllSkills.FirstOrDefault(skill => string.Equals(skill.StringId, skillName, StringComparison.OrdinalIgnoreCase));

    private static string UnknownSkillError(string skillName) =>
        $"Unknown skill {skillName}. Use one of {string.Join(", ", AllSkills.Select(skill => skill.StringId))}.";
}
