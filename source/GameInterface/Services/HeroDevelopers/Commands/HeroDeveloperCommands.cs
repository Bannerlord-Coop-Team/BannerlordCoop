using Common;
using Common.Logging;
using Serilog;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.HeroDevelopers.Commands;

internal class HeroDeveloperCommands
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeroDeveloperCommands>();

    /// <summary>
    /// Add troops to a hero's party
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
            if (hero.Name.ToString() == strings[0])
            {
                hero.AddSkillXp(skillObject, xpGain);

                stringBuilder.AppendLine($"{strings[0]} was given {xpGain} xp for {skillObject.Name}");
            }
        }

        return stringBuilder.ToString();
    }

    private static SkillObject GetSkillByName(string skillName)
    {
        var property = typeof(DefaultSkills).GetProperty(skillName, BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);

        if (property == null) return null;

        return property.GetValue(null) as SkillObject;
    }
}
