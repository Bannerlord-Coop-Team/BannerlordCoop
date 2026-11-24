using Common;
using Common.Messaging;
using GameInterface.Configuration;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Kingdoms.Handlers;

/// <summary>
/// Tells the player why the server refused their join when the kingdom has maxClansPerKingdom clans.
/// Only clients receive NetworkKingdomFull, the server sends it to the player who asked.
/// </summary>
internal class KingdomClanLimitHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public KingdomClanLimitHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

        messageBroker.Subscribe<NetworkKingdomFull>(Handle_NetworkKingdomFull);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkKingdomFull>(Handle_NetworkKingdomFull);
    }

    private void Handle_NetworkKingdomFull(MessagePayload<NetworkKingdomFull> payload)
    {
        GameThread.RunSafe(() =>
        {
            // A failed lookup still tells the player, without the kingdom name.
            objectManager.TryGetObjectWithLogging<Kingdom>(payload.What.KingdomId, out var kingdom);
            MBInformationManager.AddQuickInformation(KingdomClanLimit.GetFullText(kingdom, ModConfigProvider.ModOptions));
        }, context: nameof(KingdomClanLimitHandler));
    }
}
