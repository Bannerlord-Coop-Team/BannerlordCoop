using Common.Extensions;
using Common.Logging;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

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

    internal static void AddPartyInternal(this MobileParty mobileParty, Army army)
    {
        Army_OnAddPartyInternal(army,mobileParty);
    }
    internal static void RemovePartyInternal(this MobileParty mobileParty, Army army)
    {
        try
        {
            //Need to catch null reference exception because when an army is disbanded, it will remove all parties from it
            Army_OnRemovePartyInternal(army, mobileParty);
        }
        catch (System.NullReferenceException)
        {
            Logger.Error("NullReferenceException caught while removing {mobileParty} from {army}", mobileParty.Name, army.Name);
        }
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