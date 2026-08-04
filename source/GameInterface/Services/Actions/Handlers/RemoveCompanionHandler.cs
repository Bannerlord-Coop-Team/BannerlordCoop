using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Actions.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Actions.Handlers;

/// <summary>
/// Server-side counterpart to <see cref="Patches.RemoveCompanionActionPatches"/>'s client-side forward: turns
/// a locally-published <see cref="CompanionRemovalAttempted"/> (a non-host client's blocked direct call to
/// <c>RemoveCompanionAction.ApplyInternal</c>) into a <see cref="NetworkCompanionRemovalAttempted"/> broadcast,
/// then applies the real <c>RemoveCompanionAction.ApplyInternal</c> only once that message reaches the server.
/// Clients observe the resulting Hero/roster mutation through the existing AutoSync patches rather than
/// through this handler.
/// </summary>
internal class RemoveCompanionHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<RemoveCompanionHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public RemoveCompanionHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<CompanionRemovalAttempted>(Handle_CompanionRemovalAttempted);
        messageBroker.Subscribe<NetworkCompanionRemovalAttempted>(Handle_NetworkCompanionRemovalAttempted);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CompanionRemovalAttempted>(Handle_CompanionRemovalAttempted);
        messageBroker.Unsubscribe<NetworkCompanionRemovalAttempted>(Handle_NetworkCompanionRemovalAttempted);
    }

    private void Handle_CompanionRemovalAttempted(MessagePayload<CompanionRemovalAttempted> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.Clan, out var clanId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.Companion, out var companionId)) return;

        var message = new NetworkCompanionRemovalAttempted(clanId, companionId, obj.What.Detail);
        network.SendAll(message);
    }

    private void Handle_NetworkCompanionRemovalAttempted(MessagePayload<NetworkCompanionRemovalAttempted> obj)
    {
        // Only the server applies the removal authoritatively; clients receive the replicated
        // Hero/roster field changes via the existing AutoSync patches instead.
        if (ModInformation.IsClient) return;

        var data = obj.What;

        GameThread.Run(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging<Clan>(data.ClanId, out var clan)) return;
                if (!objectManager.TryGetObjectWithLogging<Hero>(data.CompanionId, out var companion)) return;

                RemoveCompanionAction.ApplyInternal(clan, companion, data.Detail);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply {Message}", nameof(NetworkCompanionRemovalAttempted));
            }
        });
    }
}
