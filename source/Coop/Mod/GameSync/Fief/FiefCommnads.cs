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
    class FiefCommnads
    {
        private const string sGroupName = "coop";
        private const string sTestGroupName = "test";

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

        [CommandLineFunctionality.CommandLineArgumentFunction("get_fief", sTestGroupName)]
        public static string GetFief(List<string> parameters)
        {
            return $"Fief \"{Town.AllFiefs.First().Name}\"";
        }

        //private static Invokable OnChangeFoodStockRPC;

        ///// <summary>
        /////     Initialize RPCs on client and server side.
        ///// </summary>
        //[PatchInitializer]
        //private static void InitRPC()
        //{
        //    OnChangeFoodStockRPC = new Invokable(typeof(FiefSync).GetMethod(nameof(FiefSync.OnChangeFoodStock),
        //        BindingFlags.NonPublic | BindingFlags.Static));
        //}

        //private static void OnChangeFoodStock(Guid fiefGuid, float foodStocks)
        //{
        //    if (Coop.IsServer)
        //    {
        //        return;
        //    }

        //    TaleWorlds.CampaignSystem.Fief fief = CoopObjectManager.GetObject<TaleWorlds.CampaignSystem.Fief>(fiefGuid);

        //    if (fief == null)
        //    {
        //        return;
        //    }

        //    InformationManager.DisplayMessage(new InformationMessage($"New food stocks {fief.Name}: {foodStocks}"));
        //    fief.FoodStocks = foodStocks;
        //}

        //public static void BroadcastChangeFoodStock(TaleWorlds.CampaignSystem.Fief fief)
        //{
        //    if (fief != null)
        //    {
        //        Guid fiefGuid = CoopObjectManager.GetGuid(fief);

        //        if (fiefGuid == Guid.Empty)
        //        {
        //            return;
        //        }

        //        CoopServer.Instance.Synchronization.Broadcast(OnChangeFoodStockRPC.Id, null, new object[] { fiefGuid, fief.FoodStocks });
        //    }
        //}
    }
}
