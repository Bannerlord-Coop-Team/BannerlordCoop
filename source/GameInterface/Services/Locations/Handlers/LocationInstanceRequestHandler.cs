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

    public LocationInstanceRequestHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

        messageBroker.Subscribe<PlayerEnteredLocation>(Handle_PlayerEnteredLocation);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerEnteredLocation>(Handle_PlayerEnteredLocation);
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

        Logger.Information("[LocationSync] Sending EnterLocationRequested settlement={SettlementId} location={LocationId}", settlementId, locationId);

        messageBroker.Publish(this, new EnterLocationRequested(settlementId, locationId));
    }
}
