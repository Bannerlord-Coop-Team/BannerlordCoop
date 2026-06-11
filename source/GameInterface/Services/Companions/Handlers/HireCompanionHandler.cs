using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Companions.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.UI.Notifications.Messages;
using LiteNetLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Companions.Handlers;

internal class HireCompanionHandler : IHandler
{
    private static readonly ILogger logger = LogManager.GetLogger<HireCompanionHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public HireCompanionHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<CompanionHired>(Handle_CompanionHired);
        messageBroker.Subscribe<HireCompanion>(Handle_HireCompanion);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CompanionHired>(Handle_CompanionHired);
        messageBroker.Unsubscribe<HireCompanion>(Handle_HireCompanion);
    }

    private void Handle_CompanionHired(MessagePayload<CompanionHired> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.OneToOneConversationHero, out var oneToOneConversationHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.PlayerClan, out var playerClanId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.MainParty, out var mainPartyId)) return;

        var message = new HireCompanion(
            mainHeroId,
            oneToOneConversationHeroId,
            obj.What.HiringPrice,
            playerClanId,
            mainPartyId
        );

        network.SendAll(message);
    }

    private void Handle_HireCompanion(MessagePayload<HireCompanion> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.MainHeroId, out var mainHero)) return;
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.OneToOneConversationHeroId, out var oneToOneConversationHero)) return;
        if (!objectManager.TryGetObjectWithLogging<Clan>(obj.What.PlayerClanId, out var playerClan)) return;
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MainPartyId, out var mainParty)) return;

        GiveGoldAction.ApplyBetweenCharacters(mainHero, oneToOneConversationHero, obj.What.HiringPrice, false);
        AddCompanionAction.Apply(playerClan, oneToOneConversationHero);
        AddHeroToPartyAction.Apply(oneToOneConversationHero, mainParty, true);

        // Give client notification of changed gold
        network.Send(obj.Who as NetPeer, new NotifyGoldChange(-obj.What.HiringPrice));
    }
}