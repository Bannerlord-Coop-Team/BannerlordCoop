using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEventSides.Messages;
using GameInterface.Services.MobileParties.Messages.Fields.Events;
using GameInterface.Services.MobileParties.Patches;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.MapEventSides.Patches;

[HarmonyPatch]
internal class MapEventSideDataPatches
{
    static readonly ILogger Logger = LogManager.GetLogger<MapEventSideDataPatches>();

    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var method in AccessTools.GetDeclaredMethods(typeof(MapEventSide)))
        {
            yield return method;
        }
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> MapFactionTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var field = AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide._mapFaction));
        var fieldIntercept = AccessTools.Method(typeof(MapEventSideDataPatches), nameof(MapFactionIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(field))
            {
                yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void MapFactionIntercept(MapEventSide instance, IFaction newFaction)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance._mapFaction = newFaction;
            return;
        }
        if (ModInformation.IsClient)
        {
            Logger.Error("Client tried to update MapFaction: {callstack}", Environment.StackTrace);
            return;
        }

        MessageBroker.Instance.Publish(instance, new MapEventSideIFactionChanged(instance, newFaction));

        instance._mapFaction = newFaction;
    }
}
