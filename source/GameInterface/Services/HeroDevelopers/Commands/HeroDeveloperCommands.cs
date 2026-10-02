using Common.Commands;
using Common;
using Common.Logging;
using GameInterface.Utils.Commands;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using Serilog;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
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
        public string Prefix => "coop.debug.hero_developer";

        public string Name => "add_skill_xp";

        public string Description => "Runs the add skill xp debug operation.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_name_or_id", "The hero display name or StringId.", isRequired: true),
            new ExpectedArgs("skill_name", "The skill object name.", isRequired: true),
            new ExpectedArgs("xp_amount", "The amount of skill experience.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            if (ModInformation.IsClient) return Failed("Command can only be run on the server.");


            SkillObject skillObject = GetSkillByName(strings[1]);
            if (skillObject == null) return Failed("Unable to find SkillObject by provided name.");

            if (!int.TryParse(strings[2], out int xpGain)) return Failed("An integer amount of xp is required.");

            StringBuilder stringBuilder = new StringBuilder();
            foreach (var hero in Hero.AllAliveHeroes)
            {
                if (hero.Name.ToString() == strings[0] || hero.StringId == strings[0])
                {
                    hero.AddSkillXp(skillObject, xpGain);

                    stringBuilder.AppendLine($"{strings[0]} was given {xpGain} xp for {skillObject.Name}");
                }
            }

            if (stringBuilder.Length > 0) return Succeeded(stringBuilder.ToString());
            else return Failed($"Unable to find hero with name or id of {strings[0]}");
        }
    }

    /// <summary>
    /// Add attribute points to a hero.
    /// Example:
    /// coop.debug.herodeveloper.addattributepoints RandomPlayer 10
    /// </summary>
    public sealed class HeroDeveloperAddAttributePointsCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero_developer";

        public string Name => "add_attribute_points";

        public string Description => "Runs the add attribute points debug operation.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_name_or_id", "The hero display name or StringId.", isRequired: true),
            new ExpectedArgs("point_count", "The number of attribute points.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            if (ModInformation.IsClient) return Failed("Command can only be run on the server.");


            if (!int.TryParse(strings[1], out int numPoints)) return Failed("An integer amount of attribute points is required.");

            StringBuilder stringBuilder = new StringBuilder();
            foreach (var hero in Hero.AllAliveHeroes)
            {
                if (hero.Name.ToString() == strings[0] || hero.StringId == strings[0])
                {
                    // Use same implementation as vanilla command
                    hero.HeroDeveloper.UnspentAttributePoints = MBMath.ClampInt(hero.HeroDeveloper.UnspentAttributePoints + numPoints, 0, 10000);

                    stringBuilder.AppendLine($"{strings[0]} was given {numPoints} attribute points.");
                }
            }

            if (stringBuilder.Length > 0) return Succeeded(stringBuilder.ToString());
            else return Failed($"Unable to find hero with name or id of {strings[0]}");
        }
    }

    /// <summary>
    /// Add focus points to a hero.
    /// Example:
    /// coop.debug.herodeveloper.addfocuspoints RandomPlayer 10
    /// </summary>
    public sealed class HeroDeveloperAddFocusPointsCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero_developer";

        public string Name => "add_focus_points";

        public string Description => "Runs the add focus points debug operation.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_name_or_id", "The hero display name or StringId.", isRequired: true),
            new ExpectedArgs("point_count", "The number of focus points.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            if (ModInformation.IsClient) return Failed("Command can only be run on the server.");


            if (!int.TryParse(strings[1], out int numPoints)) return Failed("An integer amount of focus points is required.");

            StringBuilder stringBuilder = new StringBuilder();
            foreach (var hero in Hero.AllAliveHeroes)
            {
                if (hero.Name.ToString() == strings[0] || hero.StringId == strings[0])
                {
                    // Use same implementation as vanilla command
                    hero.HeroDeveloper.UnspentFocusPoints = MBMath.ClampInt(hero.HeroDeveloper.UnspentFocusPoints + numPoints, 0, 10000);

                    stringBuilder.AppendLine($"{strings[0]} was given {numPoints} focus points.");
                }
            }

            if (stringBuilder.Length > 0) return Succeeded(stringBuilder.ToString());
            else return Failed($"Unable to find hero with name or id of {strings[0]}");
        }
    }

    /// <summary>
    /// Reset all skills of a hero and give focus/attribute points back based on level.
    /// Example:
    /// coop.debug.herodeveloper.resetskills
    /// </summary>
    public sealed class HeroDeveloperResetSkillsCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero_developer";

        public string Name => "reset_skills";

        public string Description => "Runs the reset skills debug operation.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_name_or_id", "The hero display name or StringId.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            if (ModInformation.IsClient) return Failed("Command can only be run on the server.");


            StringBuilder stringBuilder = new StringBuilder();
            foreach (var hero in Hero.AllAliveHeroes)
            {
                if (hero.Name.ToString() == strings[0] || hero.StringId == strings[0])
                {
                    hero.HeroDeveloper.ResetCharacterStats();

                    stringBuilder.AppendLine($"{strings[0]}'s skills were reset.");
                }
            }

            if (stringBuilder.Length > 0) return Succeeded(stringBuilder.ToString());
            else return Failed($"Unable to find hero with name or id of {strings[0]}");
        }
    }

    public sealed class HeroSocialParameterCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hero_developer";
        public string Name => "social_parameter";
        public string Description => "Read a hero's social parameter, or set it on the server only if its previous value still matches.";
        public CoopCommandSide Side => CoopCommandSide.Both;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered hero id.", isRequired: true),
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
            if (!CommandHelpers.TryGetObjectManager(out var objects, out var error) ||
                !CommandHelpers.TryGetManagedObject<Hero>(objects, args[0], out var hero, out error))
                return Failed(error);
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
            return Succeeded(JsonConvert.SerializeObject(new { heroId = args[0], parameter = name, before, value = ReadValue() }));
        }
    }

    private static SkillObject GetSkillByName(string skillName)
    {
        var property = typeof(DefaultSkills).GetProperty(skillName, BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);

        if (property == null) return null;

        return property.GetValue(null) as SkillObject;
    }
}
