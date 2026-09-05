using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.CoopSessionData.Messages;
using GameInterface.Services.Alleys.Messages;
using GameInterface.Services.Caravans.Messages;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Inventory.Messages;
using GameInterface.Services.Inventory.TradeSkills.Messages;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.Smithing.Messages;
using GameInterface.Services.Workshops.Messages;
using Serilog;

namespace GameInterface.CoopSessionData;

internal class CoopSessionUpdateHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopSessionUpdateHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly ICoopSessionProvider coopSessionProvider;

    public CoopSessionUpdateHandler(
        IMessageBroker messageBroker,
        ICoopSessionProvider coopSessionProvider)
    {
        this.messageBroker = messageBroker;
        this.coopSessionProvider = coopSessionProvider;

        messageBroker.Subscribe<NetworkUpdateCoopSession>(Handle_NetworkUpdateCoopSession);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkUpdateCoopSession>(Handle_NetworkUpdateCoopSession);
    }

    private void Handle_NetworkUpdateCoopSession(MessagePayload<NetworkUpdateCoopSession> obj)
    {
        if (ModInformation.IsServer) return;

        var session = obj.What.UpdatedSession;
        
        // Load updated session data
        messageBroker.Publish(this, new InitializeClientCraftingData(session.CraftingPlayerData));
        messageBroker.Publish(this, new InitializeClientWorkshopData(session.WorkshopPlayerData));
        messageBroker.Publish(this, new InitializeClientCaravansData(session.CaravansPlayerData));
        messageBroker.Publish(this, new InitializeClientAlleyData(session.AlleyPlayerData));
        messageBroker.Publish(this, new InitializeClientInteractionsData(session.InteractionsPlayerData));
        messageBroker.Publish(this, new InitializeClientTradeData(session.TradePlayerData));
        messageBroker.Publish(this, new InitializeClientInventoryData(session.InventoryPlayerData));
        messageBroker.Publish(this, new InitializeClientHeroMeetingData(session.HeroMeetingData));
        messageBroker.Publish(this, new InitializeClientAgingData(session.AgingPlayerData));
    }
}
