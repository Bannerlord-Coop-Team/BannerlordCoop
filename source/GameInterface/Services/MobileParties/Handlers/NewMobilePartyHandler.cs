using Common.Messaging;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.MobileParties.Interfaces;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.MobileParties.Handlers
{
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

        private void Handle(MessagePayload<NewPlayerHeroRegistered> obj)
        {
            MBGUID guid = new MBGUID(obj.What.HeroGUID);
            var hero = (Hero)MBObjectManager.Instance.GetObject(guid);
            partyInterface.ManageNewParty(hero.PartyBelongedTo);
        }
    }
}
