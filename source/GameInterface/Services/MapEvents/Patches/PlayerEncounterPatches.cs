using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.PlayerCaptivityService.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
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

        // The server runs RestartPlayerEncounter locally (authoritative), unless the targeted party is held in a
        // conversation by another player.
        if (ModInformation.IsServer) return ConversationPartyHold.CanHostRestartEncounter(attackerParty, defenderParty);

        // Client: gate the encounter restart behind server approval. The send is rate-limited in
        // ConversationRequestHandler (max 1 request / 500ms) so a retried restart does not spam the server. On
        // approval the handler re-runs RestartPlayerEncounter under an AllowedThread; rejected requests never re-run it.
        MessageBroker.Instance.Publish(null, new ConversationRequested(defenderParty, attackerParty, forcePlayerOutFromSettlement, ConversationRestartSource.PlayerEncounter));

        return false;
    }

    // After the host's encounter (re)starts, hold the encountered AI party so it stays in place and cannot be
    // interacted with by others while the host converses with it. Marking happens in the postfix because the
    // restart's internal Finish releases the host's previous engagement first.
    [HarmonyPatch(nameof(PlayerEncounter.RestartPlayerEncounter))]
    [HarmonyPostfix]
    public static void RestartPlayerEncounterPostfix()
    {
        if (!ModInformation.IsServer) return;

        ConversationPartyHold.EngageHostEncounteredParty();
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

    // When an open-map encounter finishes on a client (e.g. you close a conversation with a lord party), the
    // player party is usually still engaging that party. PlayerEncounter.Current then becomes null, so on the next
    // tick EncounterManager.HandleEncounterForMobileParty re-fires RestartPlayerEncounter, which we gate and the
    // server re-approves — reopening the conversation in a loop. Native's encounter-leave consequences all
    // SetMoveModeHold to disengage; do the same here so the party stops engaging and the loop ends.
    [HarmonyPatch(nameof(PlayerEncounter.Finish))]
    [HarmonyPostfix]
    private static void FinishPostfix()
    {
        if (ModInformation.IsServer)
        {
            // The host's encounter is over: release the AI party held for the host's conversation, if any.
            ConversationPartyHold.EndHostEngagement();
            return;
        }

        // Skip our own server-approved restart: RestartPlayerEncounter calls Finish internally, and we run that
        // under an AllowedThread. Holding there would fight the restart we just asked the server to authorize.
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        // The server holds the AI party this player was conversing with; tell it the encounter is over.
        MessageBroker.Instance.Publish(null, new ConversationEnded());

        // Don't interfere with battle flow.
        if (MobileParty.MainParty.MapEvent != null) return;

        MobileParty.MainParty.SetMoveModeHold();
    }
}
