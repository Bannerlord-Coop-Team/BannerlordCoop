using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEventParties.Messages;
using GameInterface.Services.ObjectManager;
using Missions.Messages;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;

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

    public BattleCasualtyHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

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

            // The casualty is addressed by the troop character's coop object id (never a raw StringId);
            // resolve the character through the object manager.
            if (!objectManager.TryGetObjectWithLogging<CharacterObject>(msg.TroopCharacterId, out var troop))
                return;

            // Find a live troop of this character in the party's flattened battle roster and apply the casualty
            // to its CURRENT descriptor. Matching by character rather than the owner's captured seed sidesteps
            // the descriptor churn — the engine re-flattens parties during setup, minting fresh seeds — that
            // otherwise threw KeyNotFoundException and dropped casualties. One of N identical troops is
            // interchangeable here. Applying to the server roster runs the original (the prefix lets the server
            // through), then we republish the attempt so MapEventPartyHandler broadcasts NetworkTroop* to clients.
            var roster = mapEventParty.Troops;
            if (roster != null)
            {
                foreach (var element in roster)
                {
                    // Skip already killed or wounded troops
                    if (element.IsKilled || element.IsWounded) continue;
                    if (element.Troop != troop) continue;

                    if (msg.Wounded)
                    {
                        mapEventParty.OnTroopWounded(element.Descriptor);
                        messageBroker.Publish(this, new OnTroopWoundedAttempted(mapEventParty, element.Descriptor.UniqueSeed));
                    }
                    else
                    {
                        mapEventParty.OnTroopKilled(element.Descriptor);
                        messageBroker.Publish(this, new OnTroopKilledAttempted(mapEventParty, element.Descriptor.UniqueSeed));
                    }
                    return;
                }
            }

            Logger.Warning("[BattleSync] Casualty for {Char} in party {Party} dropped: no live matching troop in the server roster",
                msg.TroopCharacterId, msg.MapEventPartyId);
        });
    }
}
