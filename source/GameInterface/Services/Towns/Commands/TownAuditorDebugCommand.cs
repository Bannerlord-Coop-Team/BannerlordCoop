using Autofac;
using Common.Extensions;
using Common.Messaging;
using GameInterface.Services.GameDebug.Commands;
using GameInterface.Services.Heroes.Commands;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.ObjectManager.Extensions;
using GameInterface.Services.Towns.Data;
using GameInterface.Services.Towns.Messages;
using GameInterface.Services.Towns.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using static TaleWorlds.Library.CommandLineFunctionality;
#nullable enable
namespace GameInterface.Services.Towns.Commands;

public class TownAuditorDebugCommand
{
    private static readonly Func<Town, Town.SellLog[]> getSoldItems = typeof(Town).GetField("_soldItems", BindingFlags.Instance | BindingFlags.NonPublic).BuildUntypedGetter<Town, Town.SellLog[]>();

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

    // coop.debug.town.auditor
    /// <summary>
    /// Send all the Town values that have been sync to the server to find mismatches values
    /// </summary>
    /// <param name="args">actually none are being used..</param>
    /// <returns>strings of all the towns</returns>
    [CommandLineArgumentFunction("auditor", "coop.debug.town")]
    public static string Auditor(List<string> args)
    {
        StringBuilder stringBuilder = new StringBuilder();

        if(!ModInformation.IsClient)
        {
            stringBuilder.Append("The town Auditor debug command can only be called by a Client.");
            //return stringBuilder.ToString();
        }
        if (TryGetObjectManager(out var objectManager) == false)
        {

            stringBuilder.Append("Unable to resolve ObjectManager");
            return stringBuilder.ToString();
        }
        List<Settlement> settlements = Campaign.Current.CampaignObjectManager.Settlements
            .Where(settlement => settlement.IsTown).ToList();

        List<TownAuditorData> auditorDatas = new List<TownAuditorData>();
        settlements.ForEach((settlement) =>
        {

            if (objectManager.TryGetObject(settlement.Town.StringId, out Town t) == false)
            {
                stringBuilder.Append($"ID: '{args[0]}' not found");
                
            }
            else
            {
                Fief fief = t.Settlement.SettlementComponent as Fief;

                TownAuditorData auditorData = new TownAuditorData(
                    t.StringId, t.Name.ToString(), (t.Governor != null) ? t.Governor.Name.ToString() : "null",
                    (t.LastCapturedBy != null) ? t.LastCapturedBy.Name.ToString() : "null",
                    t.Prosperity, t.Loyalty, t.Security, t.InRebelliousState, t.GarrisonAutoRecruitmentIsEnabled,
                   fief.FoodStocks, t.TradeTaxAccumulated, getSoldItems(t));

                auditorDatas.Add(auditorData);
            }
            
            
        });
        
        var message = new TownAuditorSent(auditorDatas);
        MessageBroker.Instance.Publish(settlements.First().Town, message);

        stringBuilder.Append("Found " + settlements.Count + " settlements\n");
        return stringBuilder.ToString();

    }

}
