using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.BesiegerCamps.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Library;

namespace GameInterface.Services.BesiegerCamps.Patches
{
    [HarmonyPatch]
    internal class BesiegerCampCollectionPatches
    {
        private static readonly ILogger Logger = LogManager.GetLogger<BesiegerCampCollectionPatches>();

        private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredMethods(typeof(BesiegerCamp));

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> BesiegerPartiesTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var listAddMethod = AccessTools.Method(typeof(List<MobileParty>), "Add");
            var listAddOverrideMethod = AccessTools.Method(typeof(BesiegerCampCollectionPatches), nameof(ListAddOverride));

            var listRemoveMethod = AccessTools.Method(typeof(List<MobileParty>), "Remove");
            var listRemoveOverrideMethod = AccessTools.Method(typeof(BesiegerCampCollectionPatches), nameof(ListRemoveOverride));

            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Callvirt && instruction.operand as MethodInfo == listAddMethod)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, listAddOverrideMethod);
                }
                else if (instruction.opcode == OpCodes.Callvirt && instruction.operand as MethodInfo == listRemoveMethod)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, listRemoveOverrideMethod);
                }
                else
                {
                    yield return instruction;
                }
            }
        }

        public static void ListAddOverride(MBList<MobileParty> _mobileParties, MobileParty mobileParty, BesiegerCamp instance)
        {
            // Allows original method call if this thread is allowed
            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                _mobileParties.Add(mobileParty);
                return;
            }

            // Skip method if called from client and allow origin
            if (ModInformation.IsClient)
            {
                Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
                _mobileParties.Add(mobileParty);
                return;
            }

            MessageBroker.Instance.Publish(instance, new BesiegerPartyAdded(instance, mobileParty));

            _mobileParties.Add(mobileParty);
        }

        public static bool ListRemoveOverride(MBList<MobileParty> _mobileParties, MobileParty mobileParty, BesiegerCamp instance)
        {
            // Allows original method call if this thread is allowed
            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                return _mobileParties.Remove(mobileParty);
            }

            // Skip method if called from client and allow origin
            if (ModInformation.IsClient)
            {
                Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
                return _mobileParties.Remove(mobileParty);
            }

            MessageBroker.Instance.Publish(instance, new BesiegerPartyRemoved(instance, mobileParty));

            return _mobileParties.Remove(mobileParty);
        }
    }
}