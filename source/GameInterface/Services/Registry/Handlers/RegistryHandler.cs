using Common.Messaging;
using GameInterface.Services.MobileParties;
using GameInterface.Services.Heroes.Messages;
using TaleWorlds.CampaignSystem;
using GameInterface.Services.Clans;
using GameInterface.Services.Settlements;

namespace GameInterface.Services.Registry.Handlers;

internal class RegistryHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IHeroRegistry heroRegistry;
    private readonly IMobilePartyRegistry partyRegistry;
    private readonly IClanRegistry clanRegistry;
    private readonly ISettlementRegistry settlementRegistry;

    public RegistryHandler(
        IMessageBroker messageBroker,
        IHeroRegistry heroRegistry,
        IMobilePartyRegistry partyRegistry,
        IClanRegistry clanRegistry,
        ISettlementRegistry settlementRegistry)
    {
        this.messageBroker = messageBroker;
        this.heroRegistry = heroRegistry;
        this.partyRegistry = partyRegistry;
        this.clanRegistry = clanRegistry;
        this.settlementRegistry = settlementRegistry;
        messageBroker.Subscribe<RegisterAllGameObjects>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<RegisterAllGameObjects>(Handle);
    }

    private void Handle(MessagePayload<RegisterAllGameObjects> obj)
    {
        heroRegistry.RegisterAllHeroes();
        partyRegistry.RegisterAllParties();
        clanRegistry.RegisterAllClans();
        settlementRegistry.RegisterAllSettlements();

        messageBroker.Publish(this, new AllGameObjectsRegistered());
    }
}
