using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEventSides.Messages;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEventSides.Patches;

[HarmonyPatch(typeof(MapEventSide))]
internal class MapEventSidePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEventSidePatches>();

    private static readonly MethodInfo ListAddMethod =
        AccessTools.Method(
            typeof(List<MapEventParty>),
            nameof(List<MapEventParty>.Add),
            new[] { typeof(MapEventParty) });

    private static readonly MethodInfo AddInterceptMethod =
        AccessTools.Method(
            typeof(MapEventSidePatches),
            nameof(AddIntercept));

    [HarmonyPatch(nameof(MapEventSide.AddPartyInternal))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> TranspilerBattlePartiesAdd(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Callvirt &&
                instruction.Calls(ListAddMethod))
            {
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Call, AddInterceptMethod);
                continue;
            }

            yield return instruction;
        }
    }

    private static void AddIntercept(MBList<MapEventParty> battleParties, MapEventParty party, MapEventSide mapEventSide)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            battleParties.Add(party);
            return;
        }

        if (ModInformation.IsClient)
        {
            Logger.Error("Client attempted to add a managed battle party to a map event side");
            battleParties.Add(party);
            return;
        }

        battleParties.Add(party);

        var message = new MapEventPartyBattlePartyAdded(mapEventSide, party);

        MessageBroker.Instance.Publish(mapEventSide, message);
    }
}
