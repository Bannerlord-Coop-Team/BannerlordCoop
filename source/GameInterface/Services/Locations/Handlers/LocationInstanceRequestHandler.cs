using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network.Instances.Messages;
using GameInterface.Services.Locations.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;

namespace GameInterface.Services.Locations.Handlers;

/// <summary>
/// Resolves a local <see cref="PlayerEnteredLocation"/> into object-manager ids and raises a
/// <see cref="EnterLocationRequested"/> so the client networking layer asks the server for a P2P
/// instance. Kept separate from the roster sync in <see cref="LocationHandler"/> because this is a
/// client-only request path, not server-authoritative roster replication.
/// </summary>
public class LocationInstanceRequestHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<LocationInstanceRequestHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    // The location the client has already requested an instance for. PlayerEnteredLocation fires
    // several times per entry (OpenIndoorMission runs more than once), so we collapse the duplicates
    // and only request once per location. Reset on InstanceCleared so re-entering the same location
    // after leaving requests again.
    private string requestedLocationKey;

    public LocationInstanceRequestHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

        messageBroker.Subscribe<PlayerEnteredLocation>(Handle_PlayerEnteredLocation);
        messageBroker.Subscribe<InstanceCleared>(Handle_InstanceCleared);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerEnteredLocation>(Handle_PlayerEnteredLocation);
        messageBroker.Unsubscribe<InstanceCleared>(Handle_InstanceCleared);
    }

    private void Handle_PlayerEnteredLocation(MessagePayload<PlayerEnteredLocation> payload)
    {
        // Server side never requests; guard anyway in case the event is ever raised there.
        if (ModInformation.IsServer) return;

        var obj = payload.What;

        if (obj.Settlement == null)
        {
            Logger.Warning("[LocationSync] PlayerEnteredLocation with no settlement — skipping instance request");
            return;
        }

        if (objectManager.TryGetIdWithLogging(obj.Settlement, out var settlementId) == false)
        {
            Logger.Warning("[LocationSync] Could not resolve settlement id for '{Settlement}' — skipping instance request", obj.Settlement.StringId);
            return;
        }

        if (objectManager.TryGetIdWithLogging(obj.Location, out var locationId) == false)
        {
            Logger.Warning("[LocationSync] Could not resolve location id for '{Location}' — skipping instance request", obj.Location.StringId);
            return;
        }

        var locationKey = settlementId + "|" + locationId;
        if (locationKey == requestedLocationKey)
        {
            Logger.Debug("[LocationSync] Already requested an instance for {SettlementId}/{LocationId} — skipping duplicate PlayerEnteredLocation", settlementId, locationId);
            return;
        }
        requestedLocationKey = locationKey;

        Logger.Information("[LocationSync] Sending EnterLocationRequested settlement={SettlementId} location={LocationId}", settlementId, locationId);

        messageBroker.Publish(this, new EnterLocationRequested(settlementId, locationId));
    }

    private void Handle_InstanceCleared(MessagePayload<InstanceCleared> payload)
    {
        // Left the location; the next entry (even into the same location) must request a fresh instance.
        requestedLocationKey = null;
    }
}
