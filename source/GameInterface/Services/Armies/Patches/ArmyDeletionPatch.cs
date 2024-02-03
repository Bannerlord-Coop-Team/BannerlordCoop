using Common;
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
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Armies.Patches
{
    [HarmonyPatch(typeof(DisbandArmyAction))]
    internal class ArmyDeletionPatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<ArmyDeletionPatch>();

        [HarmonyPatch("ApplyInternal")]
        [HarmonyPrefix]
        public static bool DisbandArmyApplyInternal(ref Army army, Army.ArmyDispersionReason reason)
        {
            if(AllowedThread.IsThisThreadAllowed()) return true;
            if (PolicyProvider.AllowOriginalCalls) return true;
            if (ModInformation.IsClient) return false;

            var data = new ArmyDeletionData(army, reason);
            var message = new ArmyDisbanded(data);

            MessageBroker.Instance.Publish(army, message);
            return true;
        }

        [HarmonyPatch("ApplyInternal")]
        [HarmonyPostfix]
        public static void DisbandArmyPostfix(ref Army army)
        {
            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                Logger.Error("Unable to resolve {name}", nameof(IObjectManager));
                return;
            }

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

        public static void DisbandArmy(Army army, Army.ArmyDispersionReason reason)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    DisbandArmyActionExtension.DisbandArmy(army, reason);
                }
            });
        }
    }
}
