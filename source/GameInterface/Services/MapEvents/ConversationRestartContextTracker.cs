using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents;

internal enum ConversationRestartDecision
{
    Apply,
    Duplicate,
    Stale,
}

internal interface IConversationRestartContextTracker
{
    string Capture(PlayerEncounter encounter);
    ConversationRestartDecision Consume(string requestId, PlayerEncounter currentEncounter, PartyBase defender, PartyBase attacker);
    void Remove(string requestId);
}

internal class ConversationRestartContextTracker : IConversationRestartContextTracker
{
    private const int MaxPendingRequests = 32;

    private readonly object sync = new object();
    private readonly Dictionary<string, RestartContext> pendingEncounters = new Dictionary<string, RestartContext>();
    private readonly Queue<string> requestOrder = new Queue<string>();

    private readonly struct RestartContext
    {
        public readonly PlayerEncounter Encounter;
        public readonly PartyBase AttackerParty;
        public readonly PartyBase DefenderParty;
        public readonly PartyBase EncounteredParty;
        public readonly object MapEvent;
        public readonly object Settlement;
        public readonly object PlayerSide;

        public RestartContext(PlayerEncounter encounter)
        {
            Encounter = encounter;
            AttackerParty = encounter?._attackerParty;
            DefenderParty = encounter?._defenderParty;
            EncounteredParty = encounter?._encounteredParty;
            MapEvent = encounter?._mapEvent;
            Settlement = encounter?.EncounterSettlementAux;
            PlayerSide = encounter?.PlayerSide;
        }

        public bool StillMatches(PlayerEncounter encounter)
        {
            return ReferenceEquals(Encounter, encounter) &&
                ReferenceEquals(AttackerParty, encounter?._attackerParty) &&
                ReferenceEquals(DefenderParty, encounter?._defenderParty) &&
                ReferenceEquals(EncounteredParty, encounter?._encounteredParty) &&
                ReferenceEquals(MapEvent, encounter?._mapEvent) &&
                ReferenceEquals(Settlement, encounter?.EncounterSettlementAux) &&
                Equals(PlayerSide, encounter?.PlayerSide);
        }
    }

    public string Capture(PlayerEncounter encounter)
    {
        var requestId = Guid.NewGuid().ToString("N");

        lock (sync)
        {
            pendingEncounters[requestId] = new RestartContext(encounter);
            requestOrder.Enqueue(requestId);

            while (requestOrder.Count > MaxPendingRequests)
            {
                pendingEncounters.Remove(requestOrder.Dequeue());
            }
        }

        return requestId;
    }

    public ConversationRestartDecision Consume(
        string requestId,
        PlayerEncounter currentEncounter,
        PartyBase defender,
        PartyBase attacker)
    {
        if (string.IsNullOrEmpty(requestId))
        {
            return currentEncounter == null
                ? ConversationRestartDecision.Apply
                : GetOccupiedDecision(currentEncounter, defender, attacker);
        }

        RestartContext capturedContext;
        lock (sync)
        {
            if (!pendingEncounters.TryGetValue(requestId, out capturedContext))
                return GetOccupiedDecision(currentEncounter, defender, attacker);

            pendingEncounters.Remove(requestId);
        }

        if (capturedContext.StillMatches(currentEncounter))
            return ConversationRestartDecision.Apply;

        return GetOccupiedDecision(currentEncounter, defender, attacker);
    }

    public void Remove(string requestId)
    {
        if (string.IsNullOrEmpty(requestId)) return;

        lock (sync)
        {
            pendingEncounters.Remove(requestId);
        }
    }

    private static ConversationRestartDecision GetOccupiedDecision(
        PlayerEncounter currentEncounter,
        PartyBase defender,
        PartyBase attacker)
    {
        var encounteredParty = currentEncounter?._encounteredParty;
        return encounteredParty != null && (encounteredParty == defender || encounteredParty == attacker)
            ? ConversationRestartDecision.Duplicate
            : ConversationRestartDecision.Stale;
    }
}
