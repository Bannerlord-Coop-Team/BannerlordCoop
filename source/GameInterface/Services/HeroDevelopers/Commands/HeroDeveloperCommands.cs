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
    private static readonly ILogger Logger = LogManager.GetLogger<HeroDeveloperCommands>();

    /// <summary>
    /// Add skill xp to a hero with a skill object name.
    /// Examples: 
    /// coop.debug.herodeveloper.addskillxp RandomPlayer OneHanded 3000
    /// </summary>
    [CommandLineArgumentFunction("addskillxp", "coop.debug.herodeveloper")]
    public static string AddSkillXpCommand(List<string> strings)
    {
        if (ModInformation.IsClient) return "Command can only be run on the server.";

        if (strings.Count != 3) return "Usage: addskillxp heroName skillName xp";

        SkillObject skillObject = GetSkillByName(strings[1]);
        if (skillObject == null) return "Unable to find SkillObject by provided name.";

        if (!int.TryParse(strings[2], out int xpGain)) return "An integer amount of xp is required.";

        StringBuilder stringBuilder = new StringBuilder();
        foreach (var hero in Hero.AllAliveHeroes)
        {
            if (hero.Name.ToString() == strings[0] || hero.StringId == strings[0])
            {
                hero.AddSkillXp(skillObject, xpGain);

                stringBuilder.AppendLine($"{strings[0]} was given {xpGain} xp for {skillObject.Name}");
            }
        }

        if (stringBuilder.Length > 0) return stringBuilder.ToString();
        else return $"Unable to find hero with name or id of {strings[0]}";
    }

    /// <summary>
    /// Add attribute points to a hero.
    /// Example:
    /// coop.debug.herodeveloper.addattributepoints RandomPlayer 10
    /// </summary>
    [CommandLineArgumentFunction("addattributepoints", "coop.debug.herodeveloper")]
    public static string AddAttributePointsCommand(List<string> strings)
    {
        if (ModInformation.IsClient) return "Command can only be run on the server.";

        if (strings.Count != 2) return "Usage: addattributepoints heroName numPoints";

        if (!int.TryParse(strings[1], out int numPoints)) return "An integer amount of attribute points is required.";

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

        if (stringBuilder.Length > 0) return stringBuilder.ToString();
        else return $"Unable to find hero with name or id of {strings[0]}";
    }

    /// <summary>
    /// Add focus points to a hero.
    /// Example:
    /// coop.debug.herodeveloper.addfocuspoints RandomPlayer 10
    /// </summary>
    [CommandLineArgumentFunction("addfocuspoints", "coop.debug.herodeveloper")]
    public static string AddFocusPointsCommand(List<string> strings)
    {
        if (ModInformation.IsClient) return "Command can only be run on the server.";

        if (strings.Count != 2) return "Usage: addfocuspoints heroName numPoints";

        if (!int.TryParse(strings[1], out int numPoints)) return "An integer amount of focus points is required.";

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

        if (stringBuilder.Length > 0) return stringBuilder.ToString();
        else return $"Unable to find hero with name or id of {strings[0]}";
    }

    /// <summary>
    /// Reset all skills of a hero and give focus/attribute points back based on level.
    /// Example:
    /// coop.debug.herodeveloper.resetskills
    /// </summary>
    [CommandLineArgumentFunction("resetskills", "coop.debug.herodeveloper")]
    public static string ResetSkillsCommand(List<string> strings)
    {
        if (ModInformation.IsClient) return "Command can only be run on the server.";

        if (strings.Count != 1) return "Usage: resetskills heroName";

        StringBuilder stringBuilder = new StringBuilder();
        foreach (var hero in Hero.AllAliveHeroes)
        {
            if (hero.Name.ToString() == strings[0] || hero.StringId == strings[0])
            {
                hero.HeroDeveloper.ResetCharacterStats();

                stringBuilder.AppendLine($"{strings[0]}'s skills were reset.");
            }
        }

        if (stringBuilder.Length > 0) return stringBuilder.ToString();
        else return $"Unable to find hero with name or id of {strings[0]}";
    }

    private static SkillObject GetSkillByName(string skillName)
    {
        var property = typeof(DefaultSkills).GetProperty(skillName, BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);

        if (property == null) return null;

        return property.GetValue(null) as SkillObject;
    }
}
