using Autofac;
using GameInterface.Services.ObjectManager;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Armies.Commands;

public class ArmyDebugCommand
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



    // coop.debug.army.list
    /// <summary>
    /// Lists all the current Army
    /// </summary>
    /// <param name="args">actually none are being used..</param>
    /// <returns>strings of all the army</returns>
    [CommandLineArgumentFunction("list_army", "coop.debug.army")]
    public static string ListArmy(List<string> args)
    {
        StringBuilder stringBuilder = new StringBuilder();

        //List<Settlement> settlements = Campaign.Current.CampaignObjectManager.Settlements
        //  .Where(settlement => settlement.IsTown).ToList();
        List<Army> armies = Campaign.Current.CampaignObjectManager.Kingdoms.SelectMany(kingdom => kingdom.Armies).ToList();

        armies.ForEach((army) =>
        {
            stringBuilder.Append(string.Format("Name: '{0}'\nLeader: '{1}'\n", army.Name, army.LeaderParty.StringId));
        });

        return stringBuilder.ToString();
    }
}
