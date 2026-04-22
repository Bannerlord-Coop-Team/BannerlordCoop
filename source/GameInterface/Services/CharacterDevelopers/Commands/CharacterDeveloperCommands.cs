using Common.Logging;
using GameInterface.Services.CharacterDevelopers.Handlers;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.CharacterDevelopers.Commands;

internal class CharacterDeveloperCommands
{
    private static readonly ILogger Logger = LogManager.GetLogger<CharacterDeveloperHandler>();

    /// <summary>
    /// Output attributes, focuses, skills and perks of a specific hero
    /// </summary>
    [CommandLineArgumentFunction("herostats", "coop.debug")]
    public static string HeroStatsCommand(List<string> strings)
    {
        if (strings.Count == 0)
        {
            return "Hero name argument required.";
        }

        foreach (var hero in Hero.AllAliveHeroes)
        {
            if (hero.Name.ToString() == strings[0])
            {
                string heroData = hero.Name + ":\n";

                heroData += "Attributes: {";
                foreach (CharacterAttribute attribute in hero._characterAttributes._attributes.Keys)
                {
                    heroData += attribute.Name + ": " + hero.GetAttributeValue(attribute) + ",";
                }

                heroData += "}\n Focuses: {";
                foreach (SkillObject skill in hero._heroSkills._attributes.Keys)
                {
                    heroData += skill.Name + ": " + hero.HeroDeveloper.GetFocus(skill) + ",";
                }

                heroData += "}\n Skills: {";
                foreach (SkillObject skill in hero._heroSkills._attributes.Keys)
                {
                    heroData += skill.Name + ": " + hero.GetSkillValue(skill) + ",";
                }

                heroData += "}\n Perks: {";
                foreach (PerkObject perk in hero._heroPerks._attributes.Keys)
                {
                    heroData += perk.Name + ",";
                }
                heroData += "}";

                return heroData;
            }
        }
        return "Hero not found";
    }
}
