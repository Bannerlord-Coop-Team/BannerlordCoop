using Common;
using Common.Messaging;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.SiegeEvents.Messages;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Announces siege preparation starting and a siege dissolving without a battle on the server, so a
/// player inside the settlement gets the vanilla menus. Vanilla switches the inside player from its
/// own campaign tick, which never runs the interrupt on a co-op client parked at the static town
/// menu, and the replicated SiegeEvent is built without its constructor so no client-side campaign
/// event ever fires.
/// </summary>
[HarmonyPatch]
internal class SiegePreparationPromptPatches
{
    private static readonly HashSet<SiegeEvent> FinalizingSieges = new();

    private sealed class SiegeTerminationState
    {
        public bool ShouldPublish { get; }
        public Settlement Settlement { get; }
        public MobileParty LeaderParty { get; }
        public MobileParty[] AttackerParties { get; }
        public MobileParty[] DefenderParties { get; }
        public bool InterruptedActiveAssault { get; }

        public SiegeTerminationState(
            Settlement settlement,
            MobileParty leaderParty,
            MobileParty[] attackerParties,
            MobileParty[] defenderParties,
            bool interruptedActiveAssault)
        {
            ShouldPublish = true;
            Settlement = settlement;
            LeaderParty = leaderParty;
            AttackerParties = attackerParties;
            DefenderParties = defenderParties;
            InterruptedActiveAssault = interruptedActiveAssault;
        }

        public SiegeTerminationState()
        {
        }
    }

    // StartSiegeEvent is the single funnel for player- and AI-started sieges; a postfix runs after
    // the whole SiegeEvent constructor, so every siege graph message precedes the prompt on the wire.
    [HarmonyPatch(typeof(SiegeEventManager), nameof(SiegeEventManager.StartSiegeEvent))]
    [HarmonyPostfix]
    private static void StartSiegeEventPostfix(Settlement settlement, MobileParty besiegerParty)
    {
        if (ModInformation.IsClient) return;
        if (settlement == null || besiegerParty == null) return;

        MessageBroker.Instance.Publish(null, new SiegePreparationStarted(besiegerParty, settlement));
    }

    [HarmonyPatch(typeof(SiegeEvent), nameof(SiegeEvent.FinalizeSiegeEvent))]
    [HarmonyPrefix]
    private static void FinalizeSiegeEventPrefix(SiegeEvent __instance, out SiegeTerminationState __state)
    {
        __state = null;
        if (ModInformation.IsClient) return;
        if (!FinalizingSieges.Add(__instance)) return;

        var settlement = __instance.BesiegedSettlement;
        var mapEvent = settlement?.Party?.MapEvent;
        bool interruptedActiveAssault = IsInterruptedActiveAssault(mapEvent);
        if (settlement?.Party == null || (mapEvent != null && !interruptedActiveAssault))
        {
            __state = new SiegeTerminationState();
            return;
        }

        var camp = __instance.BesiegerCamp;
        var leaderParty = camp?.LeaderParty;
        var attackerParties = GetMobileParties(camp?.GetInvolvedPartiesForEventType());
        var defenderParties = interruptedActiveAssault
            ? GetMapEventParties(mapEvent.DefenderSide)
            : GetDefenderParties(settlement);
        if (interruptedActiveAssault)
        {
            leaderParty = mapEvent.AttackerSide?.LeaderParty?.MobileParty ?? leaderParty;
            attackerParties = attackerParties
                .Concat(GetMapEventParties(mapEvent.AttackerSide))
                .Distinct()
                .ToArray();
        }

        __state = new SiegeTerminationState(
            settlement,
            leaderParty,
            attackerParties,
            defenderParties,
            interruptedActiveAssault);
    }

    // Vanilla assumes a local player exists while selecting its player-only end menu. Skip that
    // branch when MainParty is absent so the dedicated server can still run the authoritative teardown.
    [HarmonyPatch(typeof(SiegeEvent), nameof(SiegeEvent.FinalizeSiegeEvent))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> FinalizeSiegeEventTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var getMainParty = AccessTools.PropertyGetter(typeof(MobileParty), nameof(MobileParty.MainParty));
        var getCurrentSettlement = AccessTools.PropertyGetter(
            typeof(MobileParty),
            nameof(MobileParty.CurrentSettlement));
        var getPlayerSiegeEvent = AccessTools.PropertyGetter(
            typeof(PlayerSiege),
            nameof(PlayerSiege.PlayerSiegeEvent));
        var getMainPartyCurrentSettlement = AccessTools.Method(
            typeof(SiegePreparationPromptPatches),
            nameof(GetMainPartyCurrentSettlement));
        var getHeadlessSafePlayerSiegeEvent = AccessTools.Method(
            typeof(SiegePreparationPromptPatches),
            nameof(GetHeadlessSafePlayerSiegeEvent));

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(getPlayerSiegeEvent))
            {
                codes[i].operand = getHeadlessSafePlayerSiegeEvent;
            }

            if (i + 1 >= codes.Count ||
                !codes[i].Calls(getMainParty) ||
                !codes[i + 1].Calls(getCurrentSettlement))
            {
                continue;
            }

            var replacement = new CodeInstruction(OpCodes.Call, getMainPartyCurrentSettlement);
            replacement.labels.AddRange(codes[i].labels);
            replacement.blocks.AddRange(codes[i].blocks);
            codes[i] = replacement;
            codes[i + 1].opcode = OpCodes.Nop;
            codes[i + 1].operand = null;
        }

        if (codes.Any(code => code.Calls(getPlayerSiegeEvent) || code.Calls(getMainParty)))
        {
            throw new InvalidOperationException("Unable to guard SiegeEvent.FinalizeSiegeEvent for a headless campaign");
        }

        return codes;
    }

    private static Settlement GetMainPartyCurrentSettlement()
        => MobileParty.MainParty?.CurrentSettlement;

    private static SiegeEvent GetHeadlessSafePlayerSiegeEvent()
        => MobileParty.MainParty == null
            ? null
            : PlayerSiege.PlayerSiegeEvent;

    // Vanilla can re-enter finalization while removing the last camp party; only the outer call publishes.
    [HarmonyPatch(typeof(SiegeEvent), nameof(SiegeEvent.FinalizeSiegeEvent))]
    [HarmonyFinalizer]
    private static Exception FinalizeSiegeEventFinalizer(
        SiegeEvent __instance,
        SiegeTerminationState __state,
        Exception __exception)
    {
        if (__state == null) return __exception;

        FinalizingSieges.Remove(__instance);
        if (__exception == null && __state.ShouldPublish)
        {
            MessageBroker.Instance.Publish(null, new SiegeEndedWithoutBattle(
                __state.Settlement,
                __instance._isBesiegerDefeated,
                __state.LeaderParty,
                __state.AttackerParties,
                __state.DefenderParties,
                __state.InterruptedActiveAssault));
        }

        return __exception;
    }

    internal static bool IsInterruptedActiveAssault(MapEvent mapEvent)
        => mapEvent?.IsSiegeAssault == true &&
           !mapEvent.HasWinner;

    private static MobileParty[] GetMobileParties(System.Collections.Generic.IEnumerable<PartyBase> parties)
        => parties?
            .Where(party => party?.MobileParty != null)
            .Select(party => party.MobileParty)
            .Distinct()
            .ToArray()
            ?? Array.Empty<MobileParty>();

    private static MobileParty[] GetMapEventParties(MapEventSide side)
        => GetMobileParties(side?.Parties.Select(party => party?.Party));

    private static MobileParty[] GetDefenderParties(Settlement settlement)
        => GetMobileParties(settlement.GetInvolvedPartiesForEventType())
            .Concat(settlement.Parties?.Where(party => party?.IsPlayerParty() == true)
                ?? Enumerable.Empty<MobileParty>())
            .Distinct()
            .ToArray();
}
