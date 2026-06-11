using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MapEventSides.Patches;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(MapEvent))]
internal class MapEventPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEventPatches>();

    [HarmonyPatch(nameof(MapEvent.AddInvolvedPartyInternal))]
    [HarmonyPrefix]
    private static void Prefix_AddInvolvedPartyInternal(MapEvent __instance, MapEventParty mapEventParty)
    {
        // Parties not controlled by the server are player parties
        if (mapEventParty.Party.MobileParty?.IsPlayerParty() == true)
        {
            var partiesAdded = new List<MapEventParty>();

            __instance.TroopUpgradeTracker = new TroopUpgradeTracker();
            MapEventSide[] sides = __instance._sides;
            for (int i = 0; i < sides.Length; i++)
            {
                foreach (var existingParty in sides[i].Parties)
                {
                    __instance.TroopUpgradeTracker.AddParty(existingParty);
                    partiesAdded.Add(existingParty);
                }
            }

            var message = new MapEventInvolvedPartiesAdded(__instance, partiesAdded);
            MessageBroker.Instance.Publish(__instance, message);
        }
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

        var message = new MapEventBattleStateChangeAttempted(__instance, value);
        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    [HarmonyPatch(nameof(MapEvent.Update))]
    [HarmonyPrefix]
    // Disable update on clients
    private static bool PrefixUpdate() => ModInformation.IsServer;


    [HarmonyPatch(nameof(MapEvent.OnBattleWon))]
    [HarmonyPrefix]
    private static bool Prefix_OnBattleWon(MapEvent __instance)
    {
        var containsPlayer = __instance._sides.Any(side => side.Parties.Any(party => party.Party.MobileParty.IsPlayerParty()));

        // Skip on client
        if (ModInformation.IsClient)
            return false;

        if (__instance.ContainsPlayerParty())
        {
            __instance.CalculateAndCommitMapEventResults();
        }

        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch("Update")]
    static bool PrefixUpdate(MapEvent __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
            return true;

        if (ModInformation.IsClient)
            return false;

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

        if (__instance.MobileParty?.IsPlayerParty() == true && mobileParty?.IsPlayerParty() == true)
        {
            __result = false;
            return;
        }

        // A party held in a conversation with a player can only be interacted with by that player's party. This is
        // the hard stop that keeps other AI parties from starting an encounter or battle with it, since
        // OnPartyInteraction only runs when this check passes.
        if (ConversationPartyHold.IsInteractionBlocked(__instance, mobileParty))
        {
            __result = false;
        }
    }

    private static readonly ConditionalWeakTable<MapEvent, PlayerBattleAiJoinWindow> playerBattleAiJoinWindows = new();

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.CanPartyJoinBattle))]
    [HarmonyPostfix]
    private static void Postfix_CanPartyJoinBattle(
        MapEvent __instance,
        PartyBase party,
        ref bool __result)
    {
        if (!__result)
            return;

        if (ModInformation.IsClient)
            return;

        // Always allow players to join
        if (party.MobileParty?.IsPlayerParty() == true)
            return;

        // Allow AI to join if no players are involved
        if (!__instance.ContainsPlayerParty())
            return;

        // Prevent any AI party from joining a battle that involves a player
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