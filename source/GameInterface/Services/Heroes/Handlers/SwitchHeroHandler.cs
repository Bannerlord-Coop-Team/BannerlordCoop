using Common.Messaging;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;

namespace GameInterface.Services.Heroes.Handlers;

internal class SwitchHeroHandler : IHandler
{
    private readonly IHeroInterface heroInterface;
    private readonly IMessageBroker messageBroker;

    public SwitchHeroHandler(IHeroInterface heroInterface, IMessageBroker messageBroker)
    {
        this.heroInterface = heroInterface;
        this.messageBroker = messageBroker;

        messageBroker.Subscribe<SwitchToHero>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SwitchToHero>(Handle);
    }

    private void Handle(MessagePayload<SwitchToHero> obj)
    {
        heroInterface.SwitchMainHero(obj.What.HeroId);
    }
}
