using Common.Commands;
using Common;
using Common.Logging;
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

    private static SkillObject GetSkillByName(string skillName)
    {
        var property = typeof(DefaultSkills).GetProperty(skillName, BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);

        if (property == null) return null;

        return property.GetValue(null) as SkillObject;
    }
}
