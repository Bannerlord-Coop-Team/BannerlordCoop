using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Extensions;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.PlayerCaptivityService.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(PlayerEncounter))]
internal class PlayerEncounterPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerEncounterPatches>();

    [HarmonyPatch(nameof(PlayerEncounter.RestartPlayerEncounter))]
    [HarmonyPrefix]
    public static bool RestartPlayerEncounterPrefix(PartyBase defenderParty, PartyBase attackerParty, bool forcePlayerOutFromSettlement)
    {
        // Our own server-approved re-run (AllowedThread) runs the real RestartPlayerEncounter.
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        // The server runs RestartPlayerEncounter locally (authoritative).
        if (ModInformation.IsServer) return true;

        // Client: gate the encounter restart behind server approval. The send is rate-limited in
        // ConversationRequestHandler (max 1 request / 500ms) so a retried restart does not spam the server. On
        // approval the handler re-runs RestartPlayerEncounter under an AllowedThread; rejected requests never re-run it.
        MessageBroker.Instance.Publish(null, new ConversationRequested(defenderParty, attackerParty, forcePlayerOutFromSettlement, ConversationRestartSource.PlayerEncounter));

        return false;
    }

    [HarmonyPatch("StartBattleInternal")]
    [HarmonyPrefix]
    public static bool StartBattleInternalPrefix(PlayerEncounter __instance, ref MapEvent __result)
    {
        // Our own handler / replication path (AllowedThread) runs the real creation.
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        // The server is authoritative and creates the MapEvent locally.
        if (ModInformation.IsServer) return true;

        // Client: every participant must end up with the *same* server-authoritative MapEvent object (same
        // object-manager id). Instead of creating one locally (which would desync ids), block and ask the server
        // to create it, then adopt the synced MapEvent once it resolves on this client.

        // If a MapEvent is already attached (e.g. the player joined an existing battle), keep vanilla behavior.
        if (__instance._mapEvent != null)
        {
            __result = __instance._mapEvent;
            return false;
        }

        if (!ContainerProvider.TryResolve<MapEventCreationCoordinator>(out var coordinator))
        {
            Logger.Error("Unable to resolve {Coordinator}; aborting client battle start", nameof(MapEventCreationCoordinator));
            __result = null;
            return false;
        }

        var flags = new BattleCreationFlags(
            forceRaid: __instance.ForceRaid,
            forceSallyOut: __instance.ForceSallyOut,
            forceVolunteers: __instance.ForceVolunteers,
            forceSupplies: __instance.ForceSupplies,
            isSallyOutAmbush: __instance._isSallyOutAmbush,
            forceBlockadeAttack: __instance.ForceBlockadeAttack,
            forceBlockadeSallyOutAttack: __instance.ForceBlockadeSallyOutAttack,
            forceHideoutSendTroops: __instance.ForceHideoutSendTroops);

        var mapEvent = coordinator.RequestBlocking(__instance._attackerParty, __instance._defenderParty, flags);

        if (mapEvent == null)
        {
            // Abort: the server did not produce a MapEvent within the timeout. Do not fall back to a local create.
            Logger.Error("Aborting client battle start: server did not create a map event in time");
            __result = null;
            return false;
        }

        __instance._mapEvent = mapEvent;
        __result = mapEvent;
        return false;
    }

    [HarmonyPatch("PlayerSurrenderInternal")]
    [HarmonyPrefix]
    public static bool PlayerSurrenderInternalPrefix(ref PlayerEncounter __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsServer) return true;

        var message = new PlayerSurrendered(PlayerEncounter.Current._mapEvent, MobileParty.MainParty);

        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(PlayerEncounter.CheckNearbyPartiesToJoinPlayerMapEvent))]
    [HarmonyPrefix]
    private static bool PrefixCheckNearbyPartiesToJoinPlayerMapEvent()
    {
        return false;
    }

    // Vanilla looks up MainParty's MapEventParty via PartiesOnSide(PlayerSide), which indexes _sides by
    // (int)PlayerSide and throws once MapEventRegistry.OnClientDestroyed nulls MainParty's MapEventSide first
    // (PlayerSide becomes BattleSideEnum.None). Search both sides for MainParty directly instead; if teardown
    // already removed it from both, fall back to the snapshot OnClientDestroyed captured before nulling it.
    [HarmonyPatch(nameof(PlayerEncounter.GetBattleRewards))]
    [HarmonyPrefix]
    private static bool GetBattleRewardsPrefix(PlayerEncounter __instance, out ExplainedNumber renownChange,
        out ExplainedNumber influenceChange, out ExplainedNumber moraleChange, out float playerEarnedLootRate,
        out Figurehead playerEarnedFigurehead)
    {
        var mapEvent = __instance._mapEvent;
        playerEarnedFigurehead = __instance.PlayerLootedFigurehead;

        var mapEventParty = mapEvent.FindMapEventParty(PartyBase.MainParty);

        if (mapEventParty != null)
        {
            renownChange = mapEventParty.GainedRenownExplained;
            influenceChange = mapEventParty.GainedInfluenceExplained;
            moraleChange = mapEventParty.GainedMoraleExplained;
            playerEarnedLootRate = mapEvent.GetPlayerBattleContributionRate();
            return false;
        }

        if (!MainPartyBattleRewardsCache.TryGet(mapEvent, out renownChange, out influenceChange, out moraleChange, out playerEarnedLootRate))
        {
            Logger.Warning("GetBattleRewards: MainParty not found on either side of {MapEvent} and no cached snapshot; defaulting rewards to zero", mapEvent);
        }

        return false;
    }

    // When an open-map encounter finishes on a client (e.g. you close a conversation with a lord party), the
    // player party is usually still engaging that party. PlayerEncounter.Current then becomes null, so on the next
    // tick EncounterManager.HandleEncounterForMobileParty re-fires RestartPlayerEncounter, which we gate and the
    // server re-approves — reopening the conversation in a loop. Native's encounter-leave consequences all
    // SetMoveModeHold to disengage; do the same here so the party stops engaging and the loop ends.
    [HarmonyPatch(nameof(PlayerEncounter.Finish))]
    [HarmonyPostfix]
    private static void FinishPostfix()
    {
        if (ModInformation.IsServer) return;

        // Skip our own server-approved restart: RestartPlayerEncounter calls Finish internally, and we run that
        // under an AllowedThread. Holding there would fight the restart we just asked the server to authorize.
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        // The server holds the AI party this player was conversing with; tell it the encounter is over.
        MessageBroker.Instance.Publish(null, new ConversationEnded());

        var mainParty = MobileParty.MainParty;

        // Don't interfere with battle flow.
        if (mainParty.MapEvent != null) return;

        // Stops the party locally, but no further: dynamic sync is server-authoritative (a client-side write
        // is applied locally and dropped, never sent), so this hold cannot clear the engage order the server
        // still has from when the player targeted the AI party (DefaultBehavior = EngageParty). Left alone,
        // the owning client gets pulled back into following that party after the dialog closes while the
        // server and the other clients show it stationary (issue #1311).
        mainParty.SetMoveModeHold();

        // So also publish the hold through the gated AI-behavior channel — the one client-initiated path the
        // server applies and re-broadcasts (with its position snapshot) to every client, including this one.
        // That makes the hold authoritative everywhere and clears the stale engage order at its source.
        MessageBroker.Instance.Publish(mainParty.Ai, new PartyBehaviorChangeAttempted(mainParty.Ai, AiBehavior.Hold, null, mainParty.Position));
    }

    // Native blocks defender-side parties from leaving; allow a joiner (a non-leader of its side) to leave,
    // since it is neither the aggressor nor the attacked target.
    [HarmonyPatch(typeof(EncounterGameMenuBehavior), "game_menu_encounter_leave_on_condition")]
    [HarmonyPostfix]
    private static void EncounterLeaveConditionPostfix(MenuCallbackArgs args, ref bool __result)
    {
        if (__result) return;
        if (!IsBattleJoiner()) return;

        args.optionLeaveType = GameMenuOption.LeaveType.Leave;
        __result = true;
    }

    // Open-field "encounter" Leave runs local-only on the client (teleport out, local auto-sim, side leave), so the
    // server keeps the party engaged and pulls it back. A side leader leaving ends the event: route the authoritative
    // finalize round-trip. A joiner only removes itself (battle continues): replicate the removal, then let native do
    // the local teardown. Both clear the engage order. Settlements/sieges keep their own synced flows.
    [HarmonyPatch(typeof(EncounterGameMenuBehavior), "game_menu_encounter_leave_on_consequence")]
    [HarmonyPrefix]
    private static bool EncounterLeavePrefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        var mainParty = MobileParty.MainParty;
        if (mainParty.CurrentSettlement != null || mainParty.BesiegedSettlement != null)
            return true;

        var mapEvent = mainParty.MapEvent;
        if (mapEvent == null) return true;

        if (IsBattleJoiner())
        {
            // Block native: its LeaveBattle finalizes the whole event when the side has no NPC parties (an
            // all-player PvP side). Remove only this party authoritatively; the UI tears down on removal.
            MessageBroker.Instance.Publish(mainParty, new PlayerLeaveBattleAttempted(mainParty.Party));
            ClearEngageOrder(mainParty);
            return false;
        }

        // Side leader: the server is authoritative for ending the event.
        if (ModInformation.IsServer) return true;

        MessageBroker.Instance.Publish(mapEvent, new MapEventFinalizeAttempted(mapEvent));
        ClearEngageOrder(mainParty);
        return false;
    }

    private static bool IsBattleJoiner()
    {
        var side = MobileParty.MainParty?.Party?.MapEventSide;
        return side != null && side.LeaderParty != MobileParty.MainParty.Party;
    }

    private static void ClearEngageOrder(MobileParty party)
    {
        party.SetMoveModeHold();
        MessageBroker.Instance.Publish(party.Ai, new PartyBehaviorChangeAttempted(party.Ai, AiBehavior.Hold, null, party.Position));
    }
}
