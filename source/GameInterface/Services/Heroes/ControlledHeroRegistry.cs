using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes
{
    /// <summary>
    /// Registry that contains all controlled Heros
    /// </summary>
    internal class ControlledHeroRegistry
    {
        public static readonly HashSet<Hero> ControlledHeros = new HashSet<Hero>();

        /// <summary>
        /// Registers a hero as controlled
        /// </summary>
        /// <param name="hero">Hero to register</param>
        /// <returns><see langword="true"/> if successfully added, 
        /// otherwise <see langword="false"/></returns>
        public static bool RegisterControlledHero(Hero hero)
        {
            if (ControlledHeros.Contains(hero)) return false;

            return ControlledHeros.Add(hero);
        }

        /// <summary>
        /// Removes a hero from the registry
        /// </summary>
        /// <param name="hero">Hero to remove</param>
        /// <returns><see langword="true"/> if successfully removed, 
        /// otherwise <see langword="false"/></returns>
        public static bool RemoveControlledHero(Hero hero)
        {
            if (ControlledHeros.Contains(hero) == false) return false;

            return ControlledHeros.Remove(hero);
        }
    }
}
