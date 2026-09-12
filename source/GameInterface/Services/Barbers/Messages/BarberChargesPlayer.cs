using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Barbers.Messages;

public readonly struct BarberChargesPlayer : IEvent 
{
    public readonly Hero MainHero;

    public BarberChargesPlayer(Hero mainHero)
    {
        MainHero = mainHero;
    }
}