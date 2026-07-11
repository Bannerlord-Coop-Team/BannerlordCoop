using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(MapEvent))]
internal class MapEventPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEventPatches>();

    [HarmonyPatch(nameof(MapEvent.AddInvolvedPartyInternal))]
    [HarmonyPrefix]
    private static void Prefix_AddInvolvedPartyInternal(MapEvent __instance, MapEventParty mapEventParty)
    {
        // Broadcast the involved parties to clients when a player joins, OR when an AI party joins as a
        // reinforcement while the join window is still open (InteractionPatches.IsWithinAiJoinWindow). Parties
        // not controlled by the server are player parties. The window is only ever populated on the server, so
        // an AI join broadcast stays server-driven; AI joins after the window simply aren't propagated.
        bool isPlayerJoin = mapEventParty.Party.MobileParty?.IsPlayerParty() == true;
        if (!isPlayerJoin && !InteractionPatches.IsWithinAiJoinWindow(__instance))
            return;

        var partiesAdded = new List<MapEventParty>();

        MapEventSide[] sides = __instance._sides;
        for (int i = 0; i < sides.Length; i++)
        {
            foreach (var existingParty in sides[i].Parties)
            {
                partiesAdded.Add(existingParty);
            }
        }

        var message = new MapEventInvolvedPartiesAdded(__instance, partiesAdded);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(MapEvent.FinalizeEventAux))]
    [HarmonyPrefix]
    private static bool Prefix_FinalizeEventAux(MapEvent __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
            return true;

        if (ModInformation.IsServer)
            return true;

        var message = new MapEventFinalizeAttempted(__instance);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(MapEvent.FinalizeEventAux))]
    [HarmonyPostfix]
    private static void Postfix_FinalizeEventAux(MapEvent __instance)
    {
        // By this point the event's parties have left it, so the server can
        // re-evaluate whether any player is still in a map event.
        if (ModInformation.IsClient)
            return;

        MessageBroker.Instance.Publish(__instance, new MapEventFinalized(__instance));
    }

    [HarmonyPatch(nameof(MapEvent.BattleState), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool Prefix_BattleState(MapEvent __instance, BattleState value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            return true;
        }

        if (ModInformation.IsServer)
        {
            return true;
        }

        // While the conversing client is accepting a bandit surrender, the victory state is set here
        // (by SetOverrideWinner) before the surrender is forwarded. Hold back the relay so the server
        // is not driven to capture before it knows the side surrendered; the forwarded surrender then
        // drives the authoritative victory and capture instead (at the full surrendered rate).
        if (BanditSurrenderPatch.InSurrenderConsequence)
        {
            return true;
        }

        // The same victory state can be assigned more than once in quick succession (e.g. accepting
        // a bandit surrender sets the override winner and then surrenders the enemy side, both
        // resolving to the same victory). The native setter ignores a no-op change, so only relay an
        // actual state change to avoid sending a redundant battle result to the server.
        if (value == __instance.BattleState)
        {
            return true;
        }

        var message = new MapEventBattleStateChangeAttempted(__instance, value);
        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    [HarmonyPatch(nameof(MapEvent.DoSurrender))]
    [HarmonyPrefix]
    private static bool Prefix_DoSurrender(MapEvent __instance, BattleSideEnum side)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            return true;
        }

        // The defeated troops are captured authoritatively on the server, and the capture rate is
        // full only when the defeated side is flagged surrendered (otherwise it is the reduced battle
        // rate). The surrender flag is set here by the native call, but that runs on the conversing
        // client, which never captures — so forward it for the server to apply before it captures.
        // This still runs locally too, so the conversing client's own encounter resolves as normal.
        if (ModInformation.IsClient)
        {
            MessageBroker.Instance.Publish(__instance, new MapEventSurrenderAttempted(__instance, side));
        }

        return true;
    }

    [HarmonyPatch(nameof(MapEvent.OnBattleWon))]
    [HarmonyPrefix]
    private static bool Prefix_OnBattleWon(MapEvent __instance)
    {
        // Skip on client
        if (ModInformation.IsClient)
            return false;

        // Need to calculate map event results before committing changes
        __instance.CalculateMapEventResults();

        if (__instance.ContainsPlayerParty())
        {
            // Run a custom implementation of MapEvent.CalculateAndCommitMapEventResults that broadcasts results to players
            var message = new CommitMapEventResults(__instance);
            MessageBroker.Instance.Publish(__instance, message);
        }
        else
        {
            // Mirror native OnBattleWon: a battle without players commits its results directly.
            __instance.CalculateAndCommitMapEventResults();
        }

        IBattleObserver battleObserver = __instance.BattleObserver;
        if (battleObserver == null)
        {
            return false;
        }
        battleObserver.BattleResultsReady();

        return false;
    }

    [HarmonyPatch("CommitCalculatedMapEventResults")]
    [HarmonyPrefix]
    private static bool Prefix_CommitCalculatedMapEventResults()
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
            return true;

        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(MapEvent.Update))]
    [HarmonyPrefix]
    private static bool PrefixUpdate(MapEvent __instance)
    {
        // Clients never tick a map event locally; the server is authoritative for battle resolution.
        if (ModInformation.IsClient)
            return false;

        // Receive path / setup: let the original run when patches are standing down.
        if (CallOriginalPolicy.IsOriginalAllowed())
            return true;

        if (__instance.IsRaidHostileAction())
        {
            RaidAiInterventionSuppression.SuppressJoinedDefenders(__instance);

            // Slow raids are the only village hostile action with a campaign-map progress loop. Non-raid hostile
            // actions still follow normal player battle gating so they do not resolve while a client is choosing mode.
            if (__instance.IsActiveSlowVillageRaid())
                return true;
        }

        // Skip if any parties are not set
        if (__instance.InvolvedParties.Any(x => x?.MobileParty is null))
            return false;

        // Don't update if a player is involved
        // Prevents server from instantly finishing the battle and waits for client finish request
        if (__instance.InvolvedParties.Any(x => !x.MobileParty.IsControlledByThisInstance()))
            return false;

        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(MapEvent.Initialize))]
    static bool PrefixInitialize(MapEvent __instance, PartyBase attackerParty, PartyBase defenderParty, MapEventComponent component, MapEvent.BattleTypes mapEventType)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        // Only run on server
        return ModInformation.IsServer;
    }
}

[HarmonyPatch]
internal class InteractionPatches
{
    private sealed class PlayerBattleAiJoinWindow
    {
        public CampaignTime ExpiresAt { get; }

        public PlayerBattleAiJoinWindow(int durationHours)
        {
            ExpiresAt = CampaignTime.HoursFromNow(durationHours);
        }

        public bool Expired => CampaignTime.Now > ExpiresAt;
    }


    private static readonly ConditionalWeakTable<MobileParty, PlayerBattleAiJoinWindow> interactionDebounce = new();

    [HarmonyPatch(typeof(PartyBase), "TaleWorlds.CampaignSystem.Map.IInteractablePoint.CanPartyInteract")]
    [HarmonyPostfix]
    private static void Postfix_CanPartyInteract(
        PartyBase __instance,
        MobileParty mobileParty,
        ref bool __result)
    {
        if (!__result)
            return;

        if (ModInformation.IsClient)
            return;

        // NOTE: the "both parties are players" block was intentionally removed to re-enable PvP — two player
        // parties must be able to interact to start/join a battle with each other.

        // A party held in a conversation with a player can only be interacted with by that player's party. This is
        // the hard stop that keeps other AI parties from starting an encounter or battle with it, since
        // OnPartyInteraction only runs when this check passes.
        if (ConversationPartyHold.IsInteractionBlocked(__instance, mobileParty))
        {
            __result = false;
        }
    }

    private static readonly ConditionalWeakTable<MapEvent, PlayerBattleAiJoinWindow> playerBattleAiJoinWindows = new();

    /// <summary>True while a player's battle is still within its post-start window for AI parties to join as
    /// reinforcements (<see cref="MapEventConfig.PlayerBattleAiJoinWindowHours"/>). The window is opened in
    /// <see cref="Postfix_Initialize"/>; only the server ever populates it, so this is a server-side query.</summary>
    internal static bool IsWithinAiJoinWindow(MapEvent mapEvent)
        => playerBattleAiJoinWindows.TryGetValue(mapEvent, out var window) && !window.Expired;

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.CanPartyJoinBattle))]
    [HarmonyPrefix]
    private static bool Prefix_CanPartyJoinBattle(MapEvent __instance, PartyBase party, ref bool __result)
    {
        if (CanEvaluateJoinBattle(__instance, party))
            return true;

        __result = true;
        return false;
    }

    // The vanilla check dereferences the passed party's MapFaction and, for every MapEventParty on both
    // sides, its Party and that Party's MapFaction. On the client those sync in over several messages, so
    // all must be present before the check can run without hitting un-ready state.
    internal static bool CanEvaluateJoinBattle(MapEvent mapEvent, PartyBase party)
    {
        if (mapEvent?.AttackerSide == null || mapEvent.DefenderSide == null)
            return false;

        if (party?.MapFaction == null)
            return false;

        return PartiesResolved(mapEvent.AttackerSide) && PartiesResolved(mapEvent.DefenderSide);
    }

    private static bool PartiesResolved(MapEventSide side)
    {
        foreach (var mapEventParty in side.Parties)
        {
            if (mapEventParty?.Party?.MapFaction == null)
                return false;
        }

        return true;
    }

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.CanPartyJoinBattle))]
    [HarmonyPostfix]
    private static void Postfix_CanPartyJoinBattle(
        MapEvent __instance,
        PartyBase party,
        ref bool __result)
    {
        // Always allow a player party to join, on both client and server. The joining client evaluates this when
        // building the encounter "join the battle" menu options; native can return false there (e.g. war state /
        // side expectations not matching on the client), which would hide the join option. Force it true so the
        // player can always join.
        if (party?.MobileParty?.IsPlayerParty() == true)
        {
            __result = true;
            return;
        }

        // AI gating below is server-authoritative only.
        if (ModInformation.IsClient)
            return;

        if (!__result)
            return;

        if (__instance.IsRaidAiInterventionSuppressed())
        {
            __result = false;
            return;
        }

        if (__instance.IsRaidHostileAction() && MapEventConfig.AllowRaidAiIntervention)
            return;

        // Allow AI to join if no players are involved
        if (!__instance.ContainsPlayerParty())
            return;

        // A player's battle stays open to AI reinforcements for a campaign day after it begins (the join
        // window is opened in Postfix_Initialize). While it is open, AI may keep joining; once it expires —
        // or if no window was opened for this event — no more AI may join a player's battle.
        if (IsWithinAiJoinWindow(__instance))
            return;

        __result = false;
    }

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.Initialize))]
    [HarmonyPostfix]
    private static void Postfix_Initialize(
        MapEvent __instance,
        PartyBase attackerParty,
        PartyBase defenderParty)
    {
        if (ModInformation.IsClient)
            return;

        var attackerIsPlayer = attackerParty.MobileParty?.IsPlayerParty() == true;
        var defenderIsPlayer = defenderParty.MobileParty?.IsPlayerParty() == true;

        // Only create a join window for battles that started with a player party.
        if (!attackerIsPlayer && !defenderIsPlayer)
            return;

        MessageBroker.Instance.Publish(__instance, new PlayerJoinedBattle());

        playerBattleAiJoinWindows.GetValue(
            __instance,
            _ => new PlayerBattleAiJoinWindow(MapEventConfig.PlayerBattleAiJoinWindowHours));
    }
}
