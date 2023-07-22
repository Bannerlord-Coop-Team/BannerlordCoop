using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Handlers;

internal class NewMobilePartyHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<NewMobilePartyHandler>();

    private readonly IMobilePartyInterface partyInterface;
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public NewMobilePartyHandler(
        IMobilePartyInterface partyInterface,
        IMessageBroker messageBroker,
        IObjectManager objectManager)
    {
        this.partyInterface = partyInterface;
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        messageBroker.Subscribe<NewPlayerHeroRegistered>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NewPlayerHeroRegistered>(Handle);
    }

    private void Handle(MessagePayload<NewPlayerHeroRegistered> obj)
    {
        if (objectManager.TryGetObject(obj.What.PartyStringId, out MobileParty party) == false)
        {
            Logger.Error("Could not find {objType} with string id {stringId}", typeof(MobileParty), obj.What.PartyStringId);
            return;
        }

        partyInterface.ManageNewParty(party);
    }
}
