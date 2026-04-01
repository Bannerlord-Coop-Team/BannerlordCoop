using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEventSides.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEventSides.Patches
{
    internal class MapEventSideCollectionPatches
    {
        private static readonly ILogger Logger = LogManager.GetLogger<MapEventSideCollectionPatches>();

        private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredMethods(typeof(MapEventSide));

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> InvolvedPartiesTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var listAddMethod = AccessTools.Method(typeof(List<MapEventParty>), "Add");
            var listAddOverrideMethod = AccessTools.Method(typeof(MapEventSideCollectionPatches), nameof(ListAddOverride));

            var listRemoveMethod = AccessTools.Method(typeof(List<MapEventParty>), "Remove");
            var listRemoveOverrideMethod = AccessTools.Method(typeof(MapEventSideCollectionPatches), nameof(ListRemoveOverride));

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

        public static void ListAddOverride(MBList<MapEventParty> _battleParties, MapEventParty mapEventParty, MapEventSide instance)
        {
            // Allows original method call if this thread is allowed
            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                _battleParties.Add(mapEventParty);
                return;
            }

            // Skip method if called from client and allow origin
            if (ModInformation.IsClient)
            {
                Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
                _battleParties.Add(mapEventParty);
                return;
            }

            MessageBroker.Instance.Publish(instance, new MapEventPartyAdded(instance, mapEventParty));

            _battleParties.Add(mapEventParty);
        }

        public static bool ListRemoveOverride(MBList<MapEventParty> _battleParties, MapEventParty mapEventParty, MapEventSide instance)
        {
            // Allows original method call if this thread is allowed
            if (CallOriginalPolicy.IsOriginalAllowed())
            {
                return _battleParties.Remove(mapEventParty);
            }

            // Skip method if called from client and allow origin
            if (ModInformation.IsClient)
            {
                Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
                return _battleParties.Remove(mapEventParty);
            }

            MessageBroker.Instance.Publish(instance, new MapEventPartyRemoved(instance, mapEventParty));

            return _battleParties.Remove(mapEventParty);
        }
    }
} // TODO , MAYBE: Instead of patching the addition/removal of the collection. Patch the Add/RemovePartyInternal to include other methods also being called