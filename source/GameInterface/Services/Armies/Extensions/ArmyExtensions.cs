using Common.Extensions;
using Common.Logging;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.Armies.Extensions;

/// <summary>
/// Extensions for the <see cref="Army"/> class
/// </summary>
internal static class ArmyExtensions
{
    private static readonly ILogger Logger = LogManager.GetLogger<Army>();

    private static Action<Army, MobileParty> Army_OnAddPartyInternal = typeof(Army).GetMethod("OnAddPartyInternal", BindingFlags.NonPublic | BindingFlags.Instance)
        .BuildDelegate<Action<Army, MobileParty>>();
    private static Action<Army, MobileParty> Army_OnRemovePartyInternal = typeof(Army).GetMethod("OnRemovePartyInternal", BindingFlags.NonPublic | BindingFlags.Instance)
        .BuildDelegate<Action<Army, MobileParty>>();
    private static Action<Army, Army.ArmyDispersionReason> DisbandArmyAction_ApplyInternal = typeof(DisbandArmyAction)
            .GetMethod("ApplyInternal", BindingFlags.NonPublic | BindingFlags.Static)
            .BuildDelegate<Action<Army, Army.ArmyDispersionReason>>();

    internal static void SetPartyList(this Army army, List<MobileParty> mobilePartyListToUpdate)
    {
        //Army_OnAddPartyInternal(army,mobileParty);
        
        // Get the _parties field using reflection
        FieldInfo partiesField = typeof(Army).GetField("_parties", BindingFlags.NonPublic | BindingFlags.Instance);

        
        MBList<MobileParty> newList = new MBList<MobileParty>();
        foreach (MobileParty party in mobilePartyListToUpdate)
        {
            newList.Add(party);
        }
        partiesField.SetValue(army, newList);
        
    }

    internal static void AddPartyInternal(this Army army, MobileParty mobileParty)
    {
        Army_OnAddPartyInternal(army, mobileParty);
    }
    internal static void RemovePartyInternal(this Army army, MobileParty mobileParty)
    {
        Army_OnRemovePartyInternal(army, mobileParty);
    }

    internal static void DisbandArmy(this Army army, Army.ArmyDispersionReason reason)
    {
        DisbandArmyAction_ApplyInternal(army, reason);
    }

    public static string GetStringId(this Army army)
    {
        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            Logger.Error("Unable to get {objectManager}", nameof(IObjectManager));
            return null;
        }

        if (objectManager.TryGetId(army, out var id) == false)
        {
            Logger.Error("{army} was not properly registered", army.Name);
        }

        return id;
    }
}