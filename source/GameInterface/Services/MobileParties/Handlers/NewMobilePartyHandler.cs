using Common.Messaging;
using GameInterface.Services.MobileParties.Interfaces;
using System.Linq;
using TaleWorlds.CampaignSystem;
using GameInterface.Services.Heroes.Messages;

namespace GameInterface.Services.MobileParties.Handlers;

internal class NewMobilePartyHandler : IHandler
{
    private readonly IMobilePartyInterface partyInterface;
    private readonly IMessageBroker messageBroker;

    public NewMobilePartyHandler(
        IMobilePartyInterface partyInterface,
        IMessageBroker messageBroker)
    {
        this.partyInterface = partyInterface;
        this.messageBroker = messageBroker;

        messageBroker.Subscribe<NewPlayerHeroRegistered>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NewPlayerHeroRegistered>(Handle);
    }

    private void Handle(MessagePayload<NewPlayerHeroRegistered> obj)
    {
        string stringId = obj.What.HeroStringId;
        var hero = Campaign.Current.CampaignObjectManager.AliveHeroes.Single(h => h.StringId == stringId);
        var party = Campaign.Current.CampaignObjectManager.MobileParties.Single(h => h.StringId == obj.What.PartyStringId);

        partyInterface.ManageNewParty(hero.PartyBelongedTo);
    }
}
