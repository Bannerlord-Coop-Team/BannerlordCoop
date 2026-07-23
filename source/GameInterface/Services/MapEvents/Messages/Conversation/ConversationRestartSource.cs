namespace GameInterface.Services.MapEvents.Messages.Conversation;

/// <summary>
/// Identifies which "restart player encounter" method a <see cref="ConversationRequested"/> originated from, so the
/// approving client re-runs the matching method (their parameter order and behavior differ).
/// </summary>
internal enum ConversationRestartSource : byte
{
    /// <summary><c>PlayerEncounter.RestartPlayerEncounter(defenderParty, attackerParty, forcePlayerOutFromSettlement)</c>.</summary>
    PlayerEncounter = 0,

    /// <summary><c>EncounterManager.RestartPlayerEncounter(attackerParty, defenderParty)</c> (opens the encounter menu via Init).</summary>
    EncounterManager = 1,
}
