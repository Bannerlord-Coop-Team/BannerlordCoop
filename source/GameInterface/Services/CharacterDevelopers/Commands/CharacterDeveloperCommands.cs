using Common.Commands;
using Common.Logging;
using GameInterface.Utils.Commands;
using GameInterface.Services.CharacterDevelopers.Handlers;
using Serilog;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.CharacterDevelopers.Commands;

internal class CharacterDeveloperCommands
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    private static readonly ILogger Logger = LogManager.GetLogger<CharacterDeveloperHandler>();

    /// <summary>
    /// Output attributes, focuses, skills and perks of a specific hero
    /// </summary>
    public sealed class HeroDeveloperStatsCoopCommand : ICoopCommand
    {
        // Resolves hero arguments by name or id.
        private readonly ICommandObjectResolver resolver;

        public HeroDeveloperStatsCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.hero_developer";

        public string Name => "stats";

        public string Description => "Reports stats.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_name", "Hero name or id; quote names with spaces.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {

            if (!resolver.TryResolveHero(strings[0], out var hero, out var error)) return Failed(error);

            string heroData = hero.Name + ":\n";

            heroData += " Level: " + hero.Level + "\n";

            heroData += " Total XP: " + hero.HeroDeveloper.TotalXp + "\n";

            heroData += " Unspent Focus Points: " + hero.HeroDeveloper.UnspentFocusPoints + "\n";

            heroData += " Unspent Attribute Points: " + hero.HeroDeveloper.UnspentAttributePoints + "\n";

            heroData += " Attributes: {";
            foreach (CharacterAttribute attribute in hero._characterAttributes._attributes.Keys)
            {
                heroData += attribute.Name + ": " + hero.GetAttributeValue(attribute) + ",";
            }

            heroData += "\n Skill XPs: {";
            foreach (var skillXp in hero.HeroDeveloper._skillXps)
            {
                heroData += skillXp.Key.Name + ": " + skillXp.Value + ",";
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

            return Succeeded(heroData);
        }
    }
}
