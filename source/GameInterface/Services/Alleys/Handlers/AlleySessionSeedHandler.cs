using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Registry.Messages;
using GameInterface.Services.Alleys.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Data;
using Serilog;
using System;

namespace GameInterface.Services.Alleys.Handlers;

/// <summary>
/// On the host, seeds the authoritative CoopSession alley management data for the loaded save's
/// player-owned alleys once the loaded objects are registered. When the session has no entry for an
/// owned alley, the joining client that adopts its owner gets no management entry and (with the
/// State/entry lockstep) the alley shows gang-occupied with no manage menu; seeding the owner as the
/// overseer keeps it manageable. The owning client mirror is restored in AlleyInitializationHandler.
/// </summary>
internal class AlleySessionSeedHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<AlleySessionSeedHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly ISessionAlleyPlayerDataInterface sessionInterface;
    private readonly IAlleyCampaignBehaviorInterface behaviorInterface;

    public AlleySessionSeedHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        ISessionAlleyPlayerDataInterface sessionInterface,
        IAlleyCampaignBehaviorInterface behaviorInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.sessionInterface = sessionInterface;
        this.behaviorInterface = behaviorInterface;

        messageBroker.Subscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);
    }

    private void Handle_AllGameObjectsRegistered(MessagePayload<AllGameObjectsRegistered> payload)
    {
        // The host holds the authoritative management data; clients restore their mirror from it. This
        // fires once the loaded objects are registered, so the alley ids resolve (CampaignReady is too
        // early on the host: it is what triggers registration).
        if (ModInformation.IsClient) return;

        int seeded = 0;
        foreach (var alley in behaviorInterface.GetPlayerOwnedAlleys())
        {
            if (!objectManager.TryGetIdWithLogging(alley, out var alleyId)) continue;

            // Only fill alleys the session doesn't know about yet, so existing management data is never
            // clobbered by the loaded snapshot.
            if (sessionInterface.TryGetManagementData(alleyId, out _)) continue;

            if (!objectManager.TryGetIdWithLogging(alley.Owner, out var ownerId)) continue;

            // The owner runs the alley by default; the garrison starts empty (the client adds the owner
            // to the roster when it restores the entry).
            sessionInterface.SetManagementData(alleyId, ownerId, Array.Empty<TroopRosterElementData>());
            seeded++;
        }

        if (seeded > 0) Logger.Information("Seeded co-op alley management data for {Count} owned alley(s)", seeded);
    }
}
