using Common.Extensions;
using Common.Logging;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.Armies.Extensions;

/// <summary>
/// Extensions for the <see cref="Army"/> class
/// </summary>
internal static class ArmyExtensions
{
    private static readonly ILogger Logger = LogManager.GetLogger<Army>();

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
        Army_DisperseInternal(army, reason);
    }

    internal static void SetParties(this Army army, MBList<MobileParty> parties)
    {
        Army_SetParties.SetValue(army, parties);
    }

    public static string GetStringId(this Army army)
    {
        if (army == null) return null;

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

    private static Action<Army, MobileParty> Army_OnAddPartyInternal = typeof(Army).GetMethod("OnAddPartyInternal", BindingFlags.NonPublic | BindingFlags.Instance)
       .BuildDelegate<Action<Army, MobileParty>>();
    private static Action<Army, MobileParty> Army_OnRemovePartyInternal = typeof(Army).GetMethod("OnRemovePartyInternal", BindingFlags.NonPublic | BindingFlags.Instance)
        .BuildDelegate<Action<Army, MobileParty>>();
    private static Action<Army, Army.ArmyDispersionReason> Army_DisperseInternal = AccessTools
        .Method(typeof(Army), "DisperseInternal")
        .BuildDelegate<Action<Army, Army.ArmyDispersionReason>>();
    private static FieldInfo Army_SetParties = AccessTools.Field(typeof(Army), "_parties");
}