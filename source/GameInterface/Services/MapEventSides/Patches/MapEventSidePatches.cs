using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MapEventSides.Messages;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
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

    // A player joining an existing battle runs PartyBase.MapEventSide setter -> AddPartyInternal on their own client.
    // That local add is server-only-replicated (see AddIntercept) and would create a divergent, unbroadcast
    // MapEventParty, so other clients never see the join. Intercept it on the client: ask the server to perform the
    // join authoritatively (which replicates back to everyone, including this client) and skip the local mutation.
    // The joining party's PartyBase._mapEventSide back-ref is set by the setter *before* this call, so the local
    // player still knows it is in the battle while the authoritative add replicates.
    [HarmonyPatch(nameof(MapEventSide.AddPartyInternal))]
    [HarmonyPrefix]
    private static bool Prefix_AddPartyInternal(MapEventSide __instance, PartyBase party)
    {
        // Server-approved replicated re-run, or the authoritative server itself: run the native add.
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsServer) return true;

        // Client: route a player's join through the server. Non-player client-side adds keep their existing behavior.
        if (party?.MobileParty?.IsPlayerParty() == true && __instance.MapEvent != null)
        {
            MessageBroker.Instance.Publish(__instance, new PlayerJoinBattleAttempted(party, __instance.MapEvent, __instance.MissionSide));
            return false;
        }

        return true;
    }

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

    [HarmonyPatch(nameof(MapEventSide.HandleMapEventEndForPartyInternal))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var getMainParty = AccessTools.PropertyGetter(typeof(PartyBase), nameof(PartyBase.MainParty));
        var getMobileParty = AccessTools.PropertyGetter(typeof(PartyBase), nameof(PartyBase.MobileParty));

        var isPlayerParty = AccessTools.Method(
            typeof(MobilePartyExtensions),
            nameof(MobilePartyExtensions.IsPlayerParty),
            new[] { typeof(MobileParty) });

        if (getMainParty is null)
            throw new MissingMethodException("Failed to find PartyBase.MainParty getter");

        if (getMobileParty is null)
            throw new MissingMethodException("Failed to find PartyBase.MobileParty getter");

        if (isPlayerParty is null)
            throw new MissingMethodException("Failed to find MobilePartyExtensions.IsPlayerParty(MobileParty)");

        var matcher = new Queue<CodeInstruction>();
        var patched = false;

        foreach (var instruction in instructions)
        {
            matcher.Enqueue(instruction);

            if (matcher.Count > 3)
            {
                yield return matcher.Dequeue();
            }

            if (patched || matcher.Count != 3)
                continue;

            var window = matcher.ToArray();

            /*
             * Looking for:
             *
             * ldarg.1
             * call PartyBase::get_MainParty
             * beq IL_0160
             *
             * Then inserting:
             *
             * ldarg.1
             * callvirt PartyBase::get_MobileParty
             * call MobilePartyExtensions::IsPlayerParty
             * brtrue IL_0160
             */
            if (window[0].opcode != OpCodes.Ldarg_1)
                continue;

            if (!window[1].Calls(getMainParty))
                continue;

            if (window[2].opcode != OpCodes.Beq &&
                window[2].opcode != OpCodes.Beq_S)
                continue;

            var skipDestroyLabel = (Label)window[2].operand;

            while (matcher.Count > 0)
            {
                yield return matcher.Dequeue();
            }

            yield return new CodeInstruction(OpCodes.Ldarg_1);
            yield return new CodeInstruction(OpCodes.Callvirt, getMobileParty);
            yield return new CodeInstruction(OpCodes.Call, isPlayerParty);
            yield return new CodeInstruction(OpCodes.Brtrue, skipDestroyLabel);

            patched = true;
        }

        while (matcher.Count > 0)
        {
            yield return matcher.Dequeue();
        }

        if (!patched)
        {
            throw new Exception(
                "Failed to patch MapEventSide.HandleMapEventEndForPartyInternal: " +
                "could not find PartyBase.MainParty equality check.");
        }
    }
}
