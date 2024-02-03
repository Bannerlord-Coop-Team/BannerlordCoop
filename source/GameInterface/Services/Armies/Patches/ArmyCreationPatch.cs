using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Armies.Data;
using GameInterface.Services.Armies.Extensions;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System.Collections.Concurrent;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Armies.Patches
{
    [HarmonyPatch]
    internal class ArmyCreationPatch
    {
        private static ILogger Logger = LogManager.GetLogger<Kingdom>();

        [HarmonyPatch(typeof(Kingdom), nameof(Kingdom.CreateArmy))]
        [HarmonyPrefix]
        private static bool CreateArmyPrefix()
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;
            if (PolicyProvider.AllowOriginalCalls) return true;

            return ModInformation.IsServer;
        }

        [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnArmyCreated))]
        [HarmonyPrefix]
        private static void OnArmyCreatedPrefix(ref Army army)
        {
            // Client functionality
            if (AllowedThread.IsThisThreadAllowed())
            {
                ClientRegisterNewArmy(army);

                return;
            }

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                Logger.Error("Unable to resolve {objectManager}", typeof(IObjectManager));
                return;
            }

            objectManager.AddNewObject(army, out string newArmyId);

            // Server functionality
            var kingdom = army.Kingdom;
            var leader = army.LeaderParty.LeaderHero;
            var targetSettlement = army.AiBehaviorObject as Settlement;
            var armyType = army.ArmyType;

            var data = new ArmyCreationData(kingdom, leader, targetSettlement, armyType, newArmyId);
            var message = new ArmyInKingdomCreated(data);
            MessageBroker.Instance.Publish(army, message);
        }

        private static void ClientRegisterNewArmy(Army newArmy)
        {
            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                Logger.Error("Unable to resolve {objectManager}", typeof(IObjectManager));
                return;
            }

            if (SharedArmyIdData.TryGetValue(AllowedThread.CurrentThreadId, out var stringId) == false)
            {
                Logger.Error("Unable to resolve string id from {threadId}", AllowedThread.CurrentThreadId);
                return;
            }

            if (SharedArmyIdData.TryRemove(AllowedThread.CurrentThreadId, out var _) == false)
            {
                Logger.Error("Unable to remove {threadId}", AllowedThread.CurrentThreadId);
                return;
            }

            objectManager.AddExisting(stringId, newArmy);

            Logger.Debug("Created new army ({name}) with id: {id}", newArmy.Name, newArmy.GetStringId());
        }

        private static readonly ConcurrentDictionary<int, string> SharedArmyIdData = new ConcurrentDictionary<int, string>();
        public static void CreateArmyInKingdom(Kingdom kingdom, Hero armyLeader, Settlement targetSettlement, Army.ArmyTypes selectedArmyType, string armyId)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    if (SharedArmyIdData.TryAdd(AllowedThread.CurrentThreadId, armyId) == false)
                    {
                        Logger.Error("Unable to add {threadId} to shared data", AllowedThread.CurrentThreadId);
                        return;
                    }

                    kingdom.CreateArmy(armyLeader, targetSettlement, selectedArmyType);
                }
            });
        }
    }
}
