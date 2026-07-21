using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.MapEventParties.Messages;
using GameInterface.Services.ObjectManager;
using Missions.Messages;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;

namespace Missions.Battles;

/// <summary>
/// [Server] Applies battle casualties reported by agent owners to the authoritative map-event roster. The
/// host's vanilla mission casualty accounting is suppressed during a coop battle (see MapEventPartyPatches),
/// so the owner→server path is the single source. It applies the kill/wound to the server roster and
/// republishes the matching <c>OnTroop*Attempted</c> so the existing <c>MapEventPartyHandler</c> fans it out
/// to every client through the established troop-casualty sync.
/// </summary>
internal class BattleCasualtyHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleCasualtyHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly IBattleTroopLedger ledger;

    public BattleCasualtyHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        IBattleTroopLedger ledger)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.ledger = ledger;

        messageBroker.Subscribe<NetworkRequestBattleCasualty>(Handle_NetworkRequestBattleCasualty);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkRequestBattleCasualty>(Handle_NetworkRequestBattleCasualty);
    }

    private void Handle_NetworkRequestBattleCasualty(MessagePayload<NetworkRequestBattleCasualty> payload)
    {
        if (ModInformation.IsClient) return;

        var msg = payload.What;

        // Touches the map-event roster the game loop reads, so apply on the main thread.
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MapEventParty>(msg.MapEventPartyId, out var mapEventParty))
                return;

            var mapEvent = mapEventParty.Party?.MapEventSide?.MapEvent;
            if (mapEvent == null || mapEvent.IsFinalized)
            {
                Logger.Information("[BattleSync] Casualty for {Char} in party {Party} dropped: map event is no longer active",
                    msg.TroopCharacterId, msg.MapEventPartyId);
                return;
            }

            // The casualty is addressed by the troop character's coop object id (never a raw StringId);
            // resolve the character through the object manager.
            if (!objectManager.TryGetObjectWithLogging<CharacterObject>(msg.TroopCharacterId, out var troop))
                return;

            // Prefer the exact reserve descriptor so identical troops keep one identity. If setup churned that
            // descriptor, fall back to one live troop of the same character for equivalent roster accounting.
            var roster = mapEventParty.Troops;
            if (roster != null)
            {
                UniqueTroopDescriptor selectedDescriptor = default;
                bool found = false;
                foreach (var element in roster)
                {
                    if (element.IsKilled || element.IsWounded) continue;
                    if (element.Descriptor.UniqueSeed != msg.TroopSeed) continue;
                    if (element.Troop != troop) continue;
                    selectedDescriptor = element.Descriptor;
                    found = true;
                    break;
                }

                if (!found)
                {
                    foreach (var element in roster)
                    {
                        if (element.IsKilled || element.IsWounded) continue;
                        if (element.Troop != troop) continue;
                        selectedDescriptor = element.Descriptor;
                        found = true;
                        break;
                    }
                }

                if (found)
                {
                    if (msg.Wounded)
                    {
                        try
                        {
                            mapEventParty.OnTroopWounded(selectedDescriptor);
                        }
                        catch (IndexOutOfRangeException e)
                        {
                            Logger.Warning(e, "[BattleSync] Casualty for {Char} in party {Party} dropped: roster no longer has the troop",
                                msg.TroopCharacterId, msg.MapEventPartyId);
                            return;
                        }
                        messageBroker.Publish(this, new OnTroopWoundedAttempted(mapEventParty, selectedDescriptor.UniqueSeed));
                    }
                    else
                    {
                        try
                        {
                            mapEventParty.OnTroopKilled(selectedDescriptor);
                        }
                        catch (IndexOutOfRangeException e)
                        {
                            Logger.Warning(e, "[BattleSync] Casualty for {Char} in party {Party} dropped: roster no longer has the troop",
                                msg.TroopCharacterId, msg.MapEventPartyId);
                            return;
                        }
                        messageBroker.Publish(this, new OnTroopKilledAttempted(mapEventParty, selectedDescriptor.UniqueSeed));
                    }

                    if (objectManager.TryGetId(mapEvent, out var mapEventId))
                    {
                        int departedSeed = SelectLedgerDepartureSeed(
                            mapEventId,
                            msg.MapEventPartyId,
                            selectedDescriptor.UniqueSeed,
                            msg.TroopSeed);
                        ledger.ReportDeparted(mapEventId, msg.MapEventPartyId, departedSeed);
                    }
                    return;
                }
            }

            Logger.Warning("[BattleSync] Casualty for {Char} in party {Party} dropped: no live matching troop in the server roster",
                msg.TroopCharacterId, msg.MapEventPartyId);
        });
    }

    private int SelectLedgerDepartureSeed(
        string mapEventId,
        string partyId,
        int appliedSeed,
        int reportedSeed)
    {
        if (!ledger.TryGetReserve(mapEventId, partyId, out var entries, out _))
            return appliedSeed;

        bool containsReported = false;
        foreach (var entry in entries)
        {
            if (entry.Seed == appliedSeed) return appliedSeed;
            if (entry.Seed == reportedSeed) containsReported = true;
        }
        return containsReported ? reportedSeed : appliedSeed;
    }
}
