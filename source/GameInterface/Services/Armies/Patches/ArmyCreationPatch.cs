using Common;
using Common.Extensions;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Armies.Data;
using GameInterface.Services.Armies.Extensions;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.CampaignSystem.Army;

namespace GameInterface.Services.Armies.Patches
{
    /// <summary>
    /// Patches required for creating an Army
    /// </summary>
    [HarmonyPatch]
    internal class ArmyCreationPatch
    {
        private static ILogger Logger = LogManager.GetLogger<Kingdom>();

        [HarmonyPatch(typeof(Kingdom), nameof(Kingdom.CreateArmy))]
        [HarmonyPrefix]
        private static bool CreateArmyPrefix()
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            return ModInformation.IsServer;
        }

        
        [HarmonyPatch(typeof(Kingdom), nameof(Kingdom.CreateArmy))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> OnArmyCreatedTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Newobj && instruction.operand as ConstructorInfo == ctor_Army)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_2);
                    yield return new CodeInstruction(OpCodes.Call, method_CreateArmyIntercept);
                    continue;
                }
                
                yield return instruction;
            }
        }

        private static MethodInfo method_CreateArmyIntercept => typeof(ArmyCreationPatch)
            .GetMethod(nameof(CreateArmyIntercept), BindingFlags.Static | BindingFlags.NonPublic);
        private static ConstructorInfo ctor_Army = typeof(Army).GetConstructors().Single();
        private static Army CreateArmyIntercept(Kingdom kingdom, MobileParty leaderParty, ArmyTypes armyType, Settlement targetSettlement)
        {
            var army = ObjectHelper.SkipConstructor<Army>();

            if (AllowedThread.IsThisThreadAllowed())
            {
                ClientRegisterNewArmy(army);

                return ConstructArmy(army, kingdom, leaderParty, armyType);
            }

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                Logger.Error("Unable to resolve {objectManager}", typeof(IObjectManager));

                return ConstructArmy(army, kingdom, leaderParty, armyType);
            }

            objectManager.AddNewObject(army, out string newArmyId);

            // Server functionality
            var data = new ArmyCreationData(kingdom, leaderParty.LeaderHero, targetSettlement, armyType, newArmyId);
            var message = new ArmyCreated(data);
            MessageBroker.Instance.Publish(army, message);

            return ConstructArmy(army, kingdom, leaderParty, armyType);
        }

        private static Army ConstructArmy(Army uninitializedArmy, Kingdom kingdom, MobileParty party, ArmyTypes armyType)
        {
            ctor_Army.Invoke(uninitializedArmy, new object[] { kingdom, party, armyType });
            return uninitializedArmy;
        }

        //[HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnArmyCreated))]
        //[HarmonyPrefix]
        //private static void OnArmyCreatedPrefix(ref Army army)
        //{
        //    // Client functionality
        //    if (AllowedThread.IsThisThreadAllowed())
        //    {
        //        ClientRegisterNewArmy(army);

        //        return;
        //    }

        //    if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        //    {
        //        Logger.Error("Unable to resolve {objectManager}", typeof(IObjectManager));
        //        return;
        //    }

        //    objectManager.AddNewObject(army, out string newArmyId);

        //    // Server functionality
        //    var kingdom = army.Kingdom;
        //    var leader = army.LeaderParty.LeaderHero;
        //    var targetSettlement = army.AiBehaviorObject as Settlement;
        //    var armyType = army.ArmyType;

        //    var data = new ArmyCreationData(kingdom, leader, targetSettlement, armyType, newArmyId);
        //    var message = new ArmyCreated(data);
        //    MessageBroker.Instance.Publish(army, message);
        //}

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
