using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.UI.Notifications.Messages;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.UI.Notifications.Handlers;

internal class OtherNotificationsHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<OtherNotificationsHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public OtherNotificationsHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<NetworkNotifyRemovedSupporter>(Handle_NetworkNotifyRemovedSupporter);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkNotifyRemovedSupporter>(Handle_NetworkNotifyRemovedSupporter);
    }

    private void Handle_NetworkNotifyRemovedSupporter(MessagePayload<NetworkNotifyRemovedSupporter> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.NotableId, out var notable)) return;
            if (!objectManager.TryGetObjectWithLogging<Clan>(obj.What.SupportedClanId, out var supportedClan)) return;

            if (supportedClan != Clan.PlayerClan) return;

            TextObject textObject = new TextObject("{=aaOIjHeP}{NOTABLE.NAME} no longer supports your clan as your relationship deteriorated too much.", null);
            textObject.SetCharacterProperties("NOTABLE", notable.CharacterObject, false);
            InformationManager.DisplayMessage(new InformationMessage(textObject.ToString(), new Color(0f, 1f, 0f, 1f)));
        });
    }
}
