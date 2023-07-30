using Common.Extensions;
using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Movement;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Patches
{
    [HarmonyPatch(typeof(MobileParty))]
    static class PartyMovementPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch("TargetPosition", MethodType.Setter)]
        private static bool SetTargetPositionPrefix(ref MobileParty __instance, ref Vec2 value)
        {
            if (value == get_targetPosition(__instance)) return false;

            MessageBroker.Instance.Publish(__instance, new TargetPositionUpdateAttempted(__instance.StringId, value));
            
            return false;
        }

        public static void SetTargetPosition(MobileParty party, Vec2 newVal)
        {
            set_targetPosition(party, newVal);
            //party.Ai.DefaultBehaviorNeedsUpdate = true;
        }

        static readonly Action<MobileParty, Vec2> set_targetPosition = typeof(MobileParty)
            .GetField("_targetPosition", BindingFlags.Instance | BindingFlags.NonPublic)
            .BuildUntypedSetter<MobileParty, Vec2>();

        static readonly Func<MobileParty, Vec2> get_targetPosition = typeof(MobileParty)
            .GetField("_targetPosition", BindingFlags.Instance | BindingFlags.NonPublic)
            .BuildUntypedGetter<MobileParty, Vec2>();

        static readonly Action<MobileParty, Settlement> set_targetSettlement = typeof(MobileParty)
            .GetField("_targetSettlement", BindingFlags.Instance | BindingFlags.NonPublic)
            .BuildUntypedSetter<MobileParty, Settlement>();

        static readonly Func<MobileParty, Settlement> get_targetSettlement = typeof(MobileParty)
            .GetField("_targetSettlement", BindingFlags.Instance | BindingFlags.NonPublic)
            .BuildUntypedGetter<MobileParty, Settlement>();

        static readonly Action<MobileParty, MobileParty> set_targetParty = typeof(MobileParty)
            .GetField("_targetParty", BindingFlags.Instance | BindingFlags.NonPublic)
            .BuildUntypedSetter<MobileParty, MobileParty>();

        static readonly Func<MobileParty, MobileParty> get_targetParty = typeof(MobileParty)
            .GetField("_targetParty", BindingFlags.Instance | BindingFlags.NonPublic)
            .BuildUntypedGetter<MobileParty, MobileParty>();
    }
}
