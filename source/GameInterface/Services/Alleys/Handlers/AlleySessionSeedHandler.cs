using Common;
using Common.Messaging;
using GameInterface.Registry.Messages;
using GameInterface.Services.Alleys.Interfaces;
using GameInterface.Services.ObjectManager;

namespace GameInterface.Services.Alleys.Handlers;

/// <summary>
/// On the host, seeds the authoritative CoopSession alley management data from the loaded campaign's
/// AlleyCampaignBehavior._playerOwnedCommonAreaData once the loaded objects are registered. A save that
/// was never played in co-op has empty session alley data, so without this the joining client that
/// adopts an alley's owner gets no management entry and (with the State/entry lockstep) the alley shows
/// gang-occupied with no manage menu. Seeding here lets that client restore the garrison + overseer and
/// keep the alley manageable. The owning client mirror is restored in AlleyInitializationHandler.
/// </summary>
internal class AlleySessionSeedHandler : IHandler
{
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

        foreach (var (alley, overseer, garrison) in behaviorInterface.GetPlayerOwnedAlleys())
        {
            if (!objectManager.TryGetIdWithLogging(alley, out var alleyId)) continue;

            // Only fill alleys the session doesn't know about yet, so a co-op save's transferred data
            // or a live in-session change is never clobbered by the loaded snapshot.
            if (sessionInterface.TryGetManagementData(alleyId, out _)) continue;

            string overseerId = null;
            if (overseer != null) objectManager.TryGetId(overseer, out overseerId);

            sessionInterface.SetManagementData(alleyId, overseerId, AlleyGarrisonData.ToData(garrison, objectManager));
        }
    }
}
