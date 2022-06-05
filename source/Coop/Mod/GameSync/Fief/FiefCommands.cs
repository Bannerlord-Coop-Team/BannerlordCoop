using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Common;
using Coop.Mod.GameSync.Hideout;
using Coop.Mod.Persistence.Party;
using CoopFramework;
using RailgunNet.System.Types;
using Sync.Call;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Coop.Mod.GameSync
{
    class FiefCommands
    {
        private const string sGroupName = "coop";
        private const string sTestGroupName = "test";


        /// <summary>
        /// Sets food stocks for a given Fief
        /// </summary>
        /// <param name="parameters">Expects a Fief and a value to assign food stocks</param>
        /// <returns>Changed value or whether a fief is not found.</returns>
        [CommandLineFunctionality.CommandLineArgumentFunction("set_food_stocks", sTestGroupName)]
        public static string SetFoodStocks(List<string> parameters)
        {
            if (parameters.Count != 2 || !int.TryParse(parameters[1], out int foodStocks))
            {
                return $"Usage: \"{sTestGroupName}.set_food_stocks [fief_name] [food_stock].";
            }

            string fiefName = parameters[0];
            var fief = Town.AllFiefs.FirstOrDefault(f => f.Name.ToString().Equals(fiefName, StringComparison.OrdinalIgnoreCase));

            if (fief == null)
            {
                return "Fief not found";
            }

            var oldFoodStocks = fief.FoodStocks;
            fief.FoodStocks = foodStocks;

            return $"Fief {fief.Name} changed from {oldFoodStocks} to {fief.FoodStocks}";
        }

        /// <summary>
        /// Gets food stocks for a given Fief
        /// </summary>
        /// <param name="parameters">Expects a Fief to retrieve food stocks</param>
        /// <returns>Food stocks value or whether a fief is not found.</returns>
        [CommandLineFunctionality.CommandLineArgumentFunction("get_food_stocks", sTestGroupName)]
        public static string GetFoodStocks(List<string> parameters)
        {
            if (parameters.Count != 1)
            {
                return $"Usage: \"{sTestGroupName}.get_food_stocks [fief_name].";
            }

            string fiefName = parameters[0];
            var fief = Town.AllFiefs.FirstOrDefault(f => f.Name.ToString().Equals(fiefName, StringComparison.OrdinalIgnoreCase));

            if (fief == null)
            {
                return "Fief not found";
            }

            return $"Fief {fief.Name} foodstocks is {fief.FoodStocks}";
        }

        /// <summary>
        /// Gets first Fief from AllFiefs
        /// </summary>
        /// <param name="parameters">Expects no parameters</param>
        /// <returns>Name of Fief</returns>
        [CommandLineFunctionality.CommandLineArgumentFunction("get_fief", sTestGroupName)]
        public static string GetFief(List<string> parameters)
        {
            return $"Fief \"{Town.AllFiefs.First().Name}\"";
        }
    }
}
