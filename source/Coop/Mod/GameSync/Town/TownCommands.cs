//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using TaleWorlds.CampaignSystem;
//using TaleWorlds.CampaignSystem.Settlements;
//using TaleWorlds.Library;

//namespace Coop.Mod.GameSync
//{
//    class TownCommands
//    {
//        private const string sGroupName = "coop";
//        private const string sTestGroupName = "test";


//        /// <summary>
//        /// Sets loyalty for a given Town
//        /// </summary>
//        /// <param name="parameters">Expects a Town and a value to assign loyalty</param>
//        /// <returns>Changed value or whether a town is not found.</returns>
//        [CommandLineFunctionality.CommandLineArgumentFunction("set_town_loyalty", sTestGroupName)]
//        public static string SetTownLoyalty(List<string> parameters)
//        {
//            if (parameters.Count != 2 || !float.TryParse(parameters[1], out float loyalty))
//            {
//                return $"Usage: \"{sTestGroupName}.set_town_loyalty [town_name] [loyalty].";
//            }

//            string townName = parameters[0];
//            var town = Town.AllTowns.FirstOrDefault(f => f.Name.ToString().Equals(townName, StringComparison.OrdinalIgnoreCase));

//            if (town == null)
//            {
//                return "Town not found";
//            }

//            var oldLoyalty = town.Loyalty;
//            town.Loyalty = loyalty;

//            return $"Town {town.Name} loyalty changed from {oldLoyalty} to {town.Loyalty}";
//        }

//        /// <summary>
//        /// Sets security for a given Town
//        /// </summary>
//        /// <param name="parameters">Expects a Town and a value to assign security</param>
//        /// <returns>Changed value or whether a town is not found.</returns>
//        [CommandLineFunctionality.CommandLineArgumentFunction("set_town_security", sTestGroupName)]
//        public static string SetTownSecurity(List<string> parameters)
//        {
//            if (parameters.Count != 2 || !float.TryParse(parameters[1], out float security))
//            {
//                return $"Usage: \"{sTestGroupName}.set_town_security [town_name] [security].";
//            }

//            string townName = parameters[0];
//            var town = Town.AllTowns.FirstOrDefault(f => f.Name.ToString().Equals(townName, StringComparison.OrdinalIgnoreCase));

//            if (town == null)
//            {
//                return "Town not found";
//            }

//            var oldSecurity = town.Security;
//            town.Security = security;

//            return $"Town {town.Name} security changed from {oldSecurity} to {town.Security}";
//        }

//        /// <summary>
//        /// Sets owner clan for a given Town
//        /// </summary>
//        /// <param name="parameters">Expects a Town and a clan name to assign owner clan</param>
//        /// <returns>Changed clan name or whether a town is not found.</returns>
//        [CommandLineFunctionality.CommandLineArgumentFunction("set_town_clan", sTestGroupName)]
//        public static string SetTownClan(List<string> parameters)
//        {
//            if (parameters.Count != 2)
//            {
//                return $"Usage: \"{sTestGroupName}.set_town_clan [town_name] [clan_name].";
//            }

//            string townName = parameters[0];
//            string clanName = parameters[1];
//            var town = Town.AllTowns.FirstOrDefault(f => f.Name.ToString().Equals(townName, StringComparison.OrdinalIgnoreCase));
//            var clan = Clan.All.FirstOrDefault(c => c.Name.ToString().Equals(clanName, StringComparison.OrdinalIgnoreCase));
//            if (town == null)
//            {
//                return "Town not found";
//            }
//            if (clan == null)
//            {
//                return "Clan not found";
//            }

//            var oldClan = town.OwnerClan;
//            town.OwnerClan = clan;

//            return $"Town {town.Name} clan changed from {oldClan.Name} to {town.OwnerClan.Name}";
//        }

//        /// <summary>
//        /// Sets TradeTaxAccumulated for a given Town
//        /// </summary>
//        /// <param name="parameters">Expects a Town and a value to assign Trade Tax Accumulated</param>
//        /// <returns>Changed value or whether a Town is not found.</returns>
//        [CommandLineFunctionality.CommandLineArgumentFunction("set_town_trade_tax_accumulated", sTestGroupName)]
//        public static string SetTownTradeTaxAccumulated(List<string> parameters)
//        {
//            if (parameters.Count != 2 || !int.TryParse(parameters[1], out int tradeTaxAccumulated))
//            {
//                return $"Usage: \"{sTestGroupName}.set_town_trade_tax_accumulated [town_name] [trade_tax_accumulated].";
//            }

//            string townName = parameters[0];
//            var town = Town.AllTowns.FirstOrDefault(f => f.Name.ToString().Equals(townName, StringComparison.OrdinalIgnoreCase));

//            if (town == null)
//            {
//                return "Town not found";
//            }

//            var oldTradeTaxAccumulated = town.TradeTaxAccumulated;
//            town.TradeTaxAccumulated = tradeTaxAccumulated;

//            return $"Town {town.Name} trade tax accumulated changed from {oldTradeTaxAccumulated} to {town.TradeTaxAccumulated}";
//        }

//        /// <summary>
//        /// Sets the governor for a given Town
//        /// </summary>
//        /// <param name="parameters">Expects a Town and a hero name to assign governor</param>
//        /// <returns>Changed value or whether a Town is not found.</returns>
//        [CommandLineFunctionality.CommandLineArgumentFunction("set_town_governor", sTestGroupName)]
//        public static string SetTownGovernor(List<string> parameters)
//        {
//            if (parameters.Count != 2)
//            {
//                return $"Usage: \"{sTestGroupName}.set_town_governor [town_name] [hero_name].";
//            }

//            string townName = parameters[0];
//            string heroName = parameters[1];
//            var town = Town.AllTowns.FirstOrDefault(f => f.Name.ToString().Equals(townName, StringComparison.OrdinalIgnoreCase));
//            var hero = Hero.AllAliveHeroes.FirstOrDefault(h => h.Name.ToString().Equals(heroName, StringComparison.OrdinalIgnoreCase));
//            if (town == null)
//            {
//                return "Town not found";
//            }
//            if (hero == null)
//            {
//                return "Hero not found";
//            }

//            var oldGovernor = town.Governor;
//            town.Governor = hero;

//            return $"Town {town.Name} Governor changed from {oldGovernor.Name} to {town.Governor.Name}";
//        }

//        /// <summary>
//        /// Gets loyalty for a given Town
//        /// </summary>
//        /// <param name="parameters">Expects a Town to retrieve loyalty</param>
//        /// <returns>loyalty value or whether a town is not found.</returns>
//        [CommandLineFunctionality.CommandLineArgumentFunction("get_town_loyalty", sTestGroupName)]
//        public static string GetTownLoyalty(List<string> parameters)
//        {
//            if (parameters.Count != 1)
//            {
//                return $"Usage: \"{sTestGroupName}.get_town_loyalty [town_name].";
//            }

//            string townName = parameters[0];
//            var town = Town.AllTowns.FirstOrDefault(f => f.Name.ToString().Equals(townName, StringComparison.OrdinalIgnoreCase));

//            if (town == null)
//            {
//                return "Town not found";
//            }

//            return $"Town {town.Name} loyalty is {town.Loyalty}";
//        }

//        /// <summary>
//        /// Gets security for a given Town
//        /// </summary>
//        /// <param name="parameters">Expects a Town to retrieve security</param>
//        /// <returns>security value or whether a town is not found.</returns>
//        [CommandLineFunctionality.CommandLineArgumentFunction("get_town_security", sTestGroupName)]
//        public static string GetTownSecurity(List<string> parameters)
//        {
//            if (parameters.Count != 1)
//            {
//                return $"Usage: \"{sTestGroupName}.get_town_security [town_name].";
//            }

//            string townName = parameters[0];
//            var town = Town.AllTowns.FirstOrDefault(f => f.Name.ToString().Equals(townName, StringComparison.OrdinalIgnoreCase));

//            if (town == null)
//            {
//                return "Town not found";
//            }

//            return $"Town {town.Name} security is {town.Security}";
//        }

//        /// <summary>
//        /// Gets owner clan for a given Town
//        /// </summary>
//        /// <param name="parameters">Expects a Town to retrieve owner clan</param>
//        /// <returns>clan name or whether a town is not found.</returns>
//        [CommandLineFunctionality.CommandLineArgumentFunction("get_town_owner_clan", sTestGroupName)]
//        public static string GetTownOwnerClan(List<string> parameters)
//        {
//            if (parameters.Count != 1)
//            {
//                return $"Usage: \"{sTestGroupName}.get_town_owner_clan [town_name].";
//            }

//            string townName = parameters[0];
//            var town = Town.AllTowns.FirstOrDefault(f => f.Name.ToString().Equals(townName, StringComparison.OrdinalIgnoreCase));

//            if (town == null)
//            {
//                return "Town not found";
//            }

//            return $"Town {town.Name} owner clan is {town.OwnerClan.Name}";
//        }


//        /// <summary>
//        /// Gets trade tax accumulated for a given Town
//        /// </summary>
//        /// <param name="parameters">Expects a Town to retrieve trade tax accumulated value</param>
//        /// <returns>trade tax accumulated value or whether a town is not found.</returns>
//        [CommandLineFunctionality.CommandLineArgumentFunction("get_town_trade_tax_accumulated", sTestGroupName)]
//        public static string GetTownTradeTaxAccumulated(List<string> parameters)
//        {
//            if (parameters.Count != 1)
//            {
//                return $"Usage: \"{sTestGroupName}.get_town_trade_tax_accumulated [town_name].";
//            }

//            string townName = parameters[0];
//            var town = Town.AllTowns.FirstOrDefault(f => f.Name.ToString().Equals(townName, StringComparison.OrdinalIgnoreCase));

//            if (town == null)
//            {
//                return "Town not found";
//            }

//            return $"Town {town.Name} trade tax accumulated is {town.TradeTaxAccumulated}";
//        }


//        /// <summary>
//        /// Gets governor for a given Town
//        /// </summary>
//        /// <param name="parameters">Expects a Town to retrieve governor name</param>
//        /// <returns>governor name or whether a town is not found.</returns>
//        [CommandLineFunctionality.CommandLineArgumentFunction("get_town_governor", sTestGroupName)]
//        public static string GetTownGovernor(List<string> parameters)
//        {
//            if (parameters.Count != 1)
//            {
//                return $"Usage: \"{sTestGroupName}.get_town_governor [town_name].";
//            }

//            string townName = parameters[0];
//            var town = Town.AllTowns.FirstOrDefault(f => f.Name.ToString().Equals(townName, StringComparison.OrdinalIgnoreCase));

//            if (town == null)
//            {
//                return "Town not found";
//            }

//            return $"Town {town.Name} governor is {town.Governor.Name}";
//        }

//        /// <summary>
//        /// Gets first Town from AllTowns
//        /// </summary>
//        /// <param name="parameters">Expects no parameters</param>
//        /// <returns>Name of Town</returns>
//        [CommandLineFunctionality.CommandLineArgumentFunction("get_town", sTestGroupName)]
//        public static string GetTown(List<string> parameters)
//        {
//            return $"Town \"{Town.AllTowns.First().Name}\"";
//        }
//    }
//}
