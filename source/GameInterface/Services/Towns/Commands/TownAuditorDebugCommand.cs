using Autofac;
using Common.Extensions;
using Common.Messaging;
using GameInterface.Services.GameDebug.Commands;
using GameInterface.Services.Heroes.Commands;
using GameInterface.Services.ObjectManager;
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

        if (ModInformation.IsServer)
        {
            return "The town Auditor debug command can only be called by a Client";
        }
        if (TryGetObjectManager(out var objectManager) == false)
        {
            return "Unable to resolve ObjectManager";

        }
        Settlement first_settlement = Campaign.Current.CampaignObjectManager.Settlements
                    .First();

        var message = new TownAuditorSent(getAllTownInfo(objectManager, stringBuilder));
        MessageBroker.Instance.Publish(first_settlement, message);

        stringBuilder.Append("Debug command done.");
        return stringBuilder.ToString();

    }

    public static List<TownAuditorData> getAllTownInfo(IObjectManager objectManager, StringBuilder stringBuilder = null)
    {
        List<TownAuditorData> auditorDatas;
        IEnumerable<Settlement> settlements = Campaign.Current.CampaignObjectManager.Settlements
                    .Where(settlement => settlement.IsTown);
        auditorDatas = new List<TownAuditorData>();
        foreach (Settlement settlement in settlements)
        {
            if (objectManager.TryGetObject(settlement.Town.StringId, out Town town) == false)
            {
                stringBuilder.Append($"ID: '{settlement.Town.StringId}' not found");
            }
            else
            {
                Fief fief = town.Settlement.SettlementComponent as Fief;
                
                TownAuditorData auditorData = new TownAuditorData(
                    townStringId: town.StringId,
                    name: town.Name.ToString(),
                    governor: (town.Governor != null) ? town.Governor.Name.ToString() : "null",
                    lastCapturedBy: (town.LastCapturedBy != null) ? town.LastCapturedBy.Name.ToString() : "null",
                    prosperity: town.Prosperity,
                    loyalty: town.Loyalty,
                    security: town.Security,
                    inRebelliousState: town.InRebelliousState,
                    garrisonAutoRecruitmentIsEnabled: town.GarrisonAutoRecruitmentIsEnabled,
                    foodStocks: fief.FoodStocks,
                    tradeTaxAccumulated: town.TradeTaxAccumulated,
                    sellLogList: getSoldItems(town));

                auditorDatas.Add(auditorData);
            }
        }
        return auditorDatas;
    }
}
