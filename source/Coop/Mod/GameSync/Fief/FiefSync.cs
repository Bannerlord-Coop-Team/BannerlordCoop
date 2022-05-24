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

namespace Coop.Mod.GameSync.Fief
{
    class FiefSync
    {
        private static Invokable OnChangeFoodStockRPC;

        /// <summary>
        ///     Initialize RPCs on client and server side.
        /// </summary>
        [PatchInitializer]
        private static void InitRPC()
        {
            OnChangeFoodStockRPC = new Invokable(typeof(FiefSync).GetMethod(nameof(FiefSync.OnChangeFoodStock),
                BindingFlags.NonPublic | BindingFlags.Static));
        }

        private static void OnChangeFoodStock(Guid fiefGuid, float foodStocks)
        {
            if (Coop.IsServer)
            {
                return;
            }

            TaleWorlds.CampaignSystem.Fief fief = CoopObjectManager.GetObject<TaleWorlds.CampaignSystem.Fief>(fiefGuid);

            if (fief == null)
            {
                return;
            }

            InformationManager.DisplayMessage(new InformationMessage($"New food stocks {fief.Name}: {foodStocks}"));
            fief.FoodStocks = foodStocks;
        }

        public static void BroadcastChangeFoodStock(TaleWorlds.CampaignSystem.Fief fief)
        {
            if (fief != null)
            {
                Guid fiefGuid = CoopObjectManager.GetGuid(fief);

                if (fiefGuid == Guid.Empty)
                {
                    return;
                }

                CoopServer.Instance.Synchronization.Broadcast(OnChangeFoodStockRPC.Id, null, new object[] { fiefGuid, fief.FoodStocks });
            }
        }
    }
}
