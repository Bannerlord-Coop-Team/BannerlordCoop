using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Handlers;

/// <summary>
/// Bridges a client's request to change its controlled hero's <see cref="Hero.HitPoints"/> to the
/// authoritative server. The client forwards the change; the server applies it, and the existing HitPoints
/// property sync replicates the new value back to every client.
/// </summary>
public class HeroHitPointsHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeroHitPointsHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    public HeroHitPointsHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

        // ModInformation is evaluated per call, so each side guards itself.
        messageBroker.Subscribe<HeroHitPointsChangeRequested>(Handle_HeroHitPointsChangeRequested);
        messageBroker.Subscribe<NetworkHeroHitPointsChangeRequest>(Handle_NetworkHeroHitPointsChangeRequest);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<HeroHitPointsChangeRequested>(Handle_HeroHitPointsChangeRequested);
        messageBroker.Unsubscribe<NetworkHeroHitPointsChangeRequest>(Handle_NetworkHeroHitPointsChangeRequest);
    }

    // Client: forward the request to the authoritative server.
    private void Handle_HeroHitPointsChangeRequested(MessagePayload<HeroHitPointsChangeRequested> payload)
    {
        if (ModInformation.IsServer) return;

        if (!objectManager.TryGetIdWithLogging(payload.What.Hero, out var heroId)) return;

        network.SendAll(new NetworkHeroHitPointsChangeRequest(heroId, payload.What.HitPoints));
    }

    // Server: apply authoritatively. Setting Hero.HitPoints runs the server-side property-sync intercept,
    // which broadcasts the new value to every client (including the requester).
    private void Handle_NetworkHeroHitPointsChangeRequest(MessagePayload<NetworkHeroHitPointsChangeRequest> payload)
    {
        if (ModInformation.IsClient) return;

        if (!objectManager.TryGetObjectWithLogging<Hero>(payload.What.HeroId, out var hero)) return;

        hero.HitPoints = payload.What.HitPoints;
    }
}
