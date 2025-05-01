using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEventSides.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;

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
        if (CallPolicy.IsOriginalAllowed())
        {
            instance._mapFaction = newFaction;
            return;
        }

        if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return;

        ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
messageBroker?.Publish(instance, new MapEventSideIFactionChanged(instance, newFaction));

        instance._mapFaction = newFaction;
    }
}
