using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.MapEventSides.Messages;
using HarmonyLib;
using Serilog;
using Serilog.Core;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

[HarmonyPatch(typeof(MapEvent))]
internal class MapEventAssignmentPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEventAssignmentPatches>();

    private static readonly ConstructorInfo MapEventSideConstructor =
        AccessTools.Constructor(
            typeof(MapEventSide),
            new[]
            {
                typeof(MapEvent),
                typeof(BattleSideEnum),
                typeof(PartyBase)
            });

    private static readonly MethodInfo SideCreationInterceptMethod =
        AccessTools.Method(
            typeof(MapEventAssignmentPatches),
            nameof(SideCreationIntercept));

    [HarmonyPatch(nameof(MapEvent.Initialize))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Newobj &&
                instruction.operand as ConstructorInfo == MapEventSideConstructor)
            {
                yield return new CodeInstruction(OpCodes.Call, SideCreationInterceptMethod);
                continue;
            }

            yield return instruction;
        }
    }

    private static MapEventSide SideCreationIntercept(
        MapEvent mapEvent,
        BattleSideEnum side,
        PartyBase party)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) new MapEventSide(mapEvent, side, party);

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created managed MapEventSide");
            return new MapEventSide(mapEvent, side, party);
        }

        var mapEventSide = new MapEventSide(mapEvent, side, party);

        var message = new MapEventSideAssigned(mapEvent, mapEventSide, side);

        MessageBroker.Instance.Publish(mapEvent, message);

        return mapEventSide;
    }
}