using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEventParties.Messages;
using GameInterface.Services.ObjectManager;
using Missions.Messages;
using Serilog;
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
        if (!ModInformation.IsServer) return;

        var msg = payload.What;

        // Touches the map-event roster the game loop reads, so apply on the main thread.
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MapEventParty>(msg.MapEventPartyId, out var mapEventParty))
                return;

            var descriptor = new UniqueTroopDescriptor(msg.TroopSeed);

            // Apply to the authoritative server roster (the prefix runs the original on the server), then
            // republish the attempt so MapEventPartyHandler broadcasts NetworkTroop* to every client.
            if (msg.Wounded)
            {
                mapEventParty.OnTroopWounded(descriptor);
                messageBroker.Publish(this, new OnTroopWoundedAttempted(mapEventParty, msg.TroopSeed));
            }
            else
            {
                mapEventParty.OnTroopKilled(descriptor);
                messageBroker.Publish(this, new OnTroopKilledAttempted(mapEventParty, msg.TroopSeed));
            }
        });
    }
}
