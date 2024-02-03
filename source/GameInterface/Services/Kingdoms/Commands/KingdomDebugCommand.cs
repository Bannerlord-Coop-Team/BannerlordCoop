using Autofac;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Text;
using static TaleWorlds.Library.CommandLineFunctionality;
using TaleWorlds.CampaignSystem;
using System.Linq;
using TaleWorlds.CampaignSystem.Settlements;
using Common.Extensions;
using System.Threading.Tasks;
using Common.Messaging;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.Armies.Extensions;
using TaleWorlds.Library;

namespace GameInterface.Services.Kingdoms.Commands
{
    public class KingdomDebugCommand
    {

        /// <summary>
        /// Attempts to get the ObjectManager
        /// </summary>
        /// <param name="objectManager">Resolved ObjectManager, will be null if unable to resolve</param>
        /// <returns>True if ObjectManager was resolved, otherwise False</returns>
        private static bool TryGetObjectManager(out IObjectManager objectManager)
        {
            objectManager = null;
            if (ContainerProvider.TryGetContainer(out var container) == false) return false;

            return container.TryResolve(out objectManager);
        }



        // coop.debug.kingdom.list
        /// <summary>
        /// Lists all the current Kingdoms
        /// </summary>
        /// <param name="args">actually none are being used..</param>
        /// <returns>strings of all the kingdoms</returns>
        [CommandLineArgumentFunction("list", "coop.debug.kingdom")]
        public static string ListKingdoms(List<string> args)
        {
            StringBuilder stringBuilder = new StringBuilder();

            List<Kingdom> kingdoms = Campaign.Current.CampaignObjectManager.Kingdoms.ToList();
            kingdoms.ForEach((kingdom) =>
            {
                stringBuilder.Append(string.Format("Name: '{0}'\n Id : '{1}'\n", kingdom.Name, kingdom.StringId));
            });
            return stringBuilder.ToString();
        }
    }
}
