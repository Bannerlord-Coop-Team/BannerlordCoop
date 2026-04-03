using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Intercepts EncounterManager.StartPartyEncounter on the client and routes it through
/// the server (client fires StartPartyEncounterAttempted → server approves → client executes).
/// The original code fired an InformationMessage every tick and did nothing, so conversations
/// with NPCs/lords never started for the client player.
/// </summary>
[HarmonyPatch(typeof(EncounterManager))]
public class DisablePartyEncounterPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<DisablePartyEncounterPatch>();

    // Without deduplication, StartPartyEncounter fires every campaign tick (60-86/sec while in range),
    // flooding the server with requests and building a queue of NetworkStartPartyEncounter responses.
    private static (string, string)? _sentPair = null;

    // Tracks when an encounter becomes active so we detect the end-of-encounter transition.
    private static bool _wasInEncounter = false;

    // After goodbye the encounter ends but queued server responses are still in-flight.
    // Without this cooldown the first queued response immediately restarts the conversation,
    // causing the player to experience the dialogue twice in succession.
    private static DateTime? _encounterEndedAt = null;
    private static readonly TimeSpan PostEncounterCooldown = TimeSpan.FromSeconds(2);

    [HarmonyPatch("StartPartyEncounter")]
    [HarmonyPrefix]
    private static bool StartPartyEncounterPrefix(PartyBase attackerParty, PartyBase defenderParty)
    {
        // Server handles encounters normally
        if (ModInformation.IsServer) return true;

        // Only intercept encounters that involve the local player's party
        if (attackerParty != PartyBase.MainParty && defenderParty != PartyBase.MainParty) return false;

        // Both parties must be mobile parties (not settlement parties)
        if (attackerParty.MobileParty == null || defenderParty.MobileParty == null) return false;

        bool isInEncounter = PlayerEncounter.Current != null;

        // Detect encounter state transitions (start or end) to manage tracking state.
        if (_wasInEncounter != isInEncounter)
        {
            if (!isInEncounter)
            {
                // Encounter just ended: start cooldown and clear the pending pair so a fresh
                // request can be sent once the cooldown expires.
                Logger.Debug(
                    "Party encounter ended (pair={pair}): applying {sec}s post-encounter cooldown",
                    _sentPair, PostEncounterCooldown.TotalSeconds);
                _encounterEndedAt = DateTime.UtcNow;
            }
            else
            {
                // Encounter just started (server approved our request): clear the sent pair.
                Logger.Debug("Party encounter started, clearing sent pair {pair}", _sentPair);
            }

            _sentPair = null;
            _wasInEncounter = isInEncounter;
        }

        // Guard 1: Already in an encounter — nothing to do.
        if (isInEncounter) return false;

        var pair = (attackerParty.MobileParty.StringId, defenderParty.MobileParty.StringId);

        // Guard 2: Post-encounter cooldown — prevents queued server responses from
        // immediately restarting the conversation after goodbye.
        if (_encounterEndedAt.HasValue && (DateTime.UtcNow - _encounterEndedAt.Value) < PostEncounterCooldown)
        {
            return false;
        }

        // Guard 3: Deduplicate — only send one request per approach until the encounter
        // starts (or parties separate, causing a different pair to be attempted).
        if (_sentPair == pair)
        {
            return false;
        }

        Logger.Debug(
            "Sending StartPartyEncounterAttempted: attacker={attacker} defender={defender}",
            pair.Item1, pair.Item2);

        _sentPair = pair;
        var message = new StartPartyEncounterAttempted(pair.Item1, pair.Item2);
        MessageBroker.Instance.Publish(attackerParty, message);

        return false;
    }
}
