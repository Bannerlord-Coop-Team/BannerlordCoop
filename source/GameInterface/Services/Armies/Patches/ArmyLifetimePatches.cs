using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Armies.Data;
using GameInterface.Services.Armies.Extensions;
using GameInterface.Services.Armies.Messages.Lifetime;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using static TaleWorlds.CampaignSystem.Army;

namespace GameInterface.Services.Armies.Patches;

/// <summary>
/// Patches required for creating an Army
/// </summary>
[HarmonyPatch]
internal class ArmyLifetimePatches
{
    private static ILogger Logger = LogManager.GetLogger<Kingdom>();

    [HarmonyPatch(typeof(Army), MethodType.Constructor, typeof(Kingdom), typeof(MobileParty), typeof(ArmyTypes))]
    [HarmonyPrefix]
    private static bool CreateArmyPrefix(ref Army __instance, Kingdom kingdom, MobileParty leaderParty, Army.ArmyTypes armyType)
    {
        // Skip if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}", typeof(Army));
            return true;
        }

        // Allow method if container is not setup
        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false) return true;
        if (ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker) == false) return true;
        if (ContainerProvider.TryResolve<INetworkConfiguration>(out var configuration) == false) return true;

        if (objectManager.AddNewObject(__instance, out var stringID) == false) return true;

        var data = new ArmyCreationData(__instance, kingdom, leaderParty, armyType);
        var message = new ArmyCreated(data);

        using (new MessageTransaction<NewArmySynced>(messageBroker, configuration.ObjectCreationTimeout))
        {
            MessageBroker.Instance.Publish(__instance, message);
        }

        return true;
    }

    private static ConstructorInfo ctor_Army = AccessTools.Constructor(typeof(Army), new Type[] { typeof(Kingdom), typeof(MobileParty), typeof(ArmyTypes) });
    public static void OverrideCreateArmy(ArmyCreationData creationData)
    {
        var armyId = creationData.StringId;
        

        var army = ObjectHelper.SkipConstructor<Army>();

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false) return;


        if (objectManager.AddExisting(armyId, army) == false) return;

        var message = new ArmyCreated(null);
        MessageBroker.Instance.Publish(army, message);

        // TODO sync fields instead of calling constructor
        var kingdomId = creationData.KingdomId;
        var leaderPartyId = creationData.LeaderPartyId;
        var armyType = creationData.ArmyType;

        if (objectManager.TryGetObject(kingdomId, out Kingdom kingdom) == false) return;
        if (objectManager.TryGetObject(leaderPartyId, out MobileParty leaderParty) == false) return;

        ctor_Army.Invoke(army, new object[] { kingdom, leaderParty, armyType });
    }

    [HarmonyPatch(typeof(Army), "DisperseInternal")]
    [HarmonyPrefix]
    public static bool DisperseInternal(ref Army __instance, Army.ArmyDispersionReason reason)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client destroyed unmanaged {name}", typeof(Army));
            return false;
        }

        var data = new ArmyDestructionData(__instance, reason);
        var message = new ArmyDestroyed(data);

        MessageBroker.Instance.Publish(__instance, message);
        return true;
    }

    [HarmonyPatch(typeof(DisbandArmyAction), "ApplyInternal")]
    [HarmonyPostfix]
    public static void DisbandArmyPostfix(ref Army army)
    {
        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false) return;

        if (objectManager.Contains(army) == false)
        {
            Logger.Error("{name} did not contain Army {army}", nameof(IObjectManager), army.Name);
            return;
        }

        if (objectManager.Remove(army) == false)
        {
            Logger.Error("Could not remove Army {army} from {name}", army.Name, nameof(IObjectManager));
            return;
        }
    }

    public static void OverrideDestroyArmy(Army army, ArmyDispersionReason reason)
    {
        using (new AllowedThread())
        {
            army.DisbandArmy(reason);
        }
    }
}
