using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Heroes.Commands
{
    public class HeroDebugCommand
    {
        /// <summary>
        /// Finds a specific alive hero in game.
        /// </summary>
        /// <param name="heroId">string id of the hero to search</param>
        /// <returns>Hero or null.</returns>
        public static Hero findHero(string heroId)
        {
            List<Hero> heroes = Campaign.Current.CampaignObjectManager.AliveHeroes.ToList();
            Hero hero = heroes.Find(h => h.StringId == heroId);
            return hero;
        }

        // coop.debug.clan.list
        /// <summary>
        /// Lists all the heroes
        /// </summary>
        /// <param name="args">actually none are being used..</param>
        /// <returns>strings of all the heroes</returns>
        [CommandLineArgumentFunction("list", "coop.debug.hero")]
        public static string ListHeroes(List<string> args)
        {
            StringBuilder stringBuilder = new StringBuilder();

            List<Hero> heroes = Campaign.Current.CampaignObjectManager.AliveHeroes.ToList();

            heroes.ForEach((hero) =>
            {
                stringBuilder.Append(string.Format("ID: '{0}'\nName: '{1}'\n", hero.StringId, hero.Name));
            });

            return stringBuilder.ToString();
        }
    }
}
