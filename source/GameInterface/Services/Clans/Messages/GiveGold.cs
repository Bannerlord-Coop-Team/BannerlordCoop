using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages;

internal readonly struct GiveGold : IEvent
{
    public readonly int Gold;
    public readonly Hero Hero;

    public GiveGold(int gold, Hero hero)
    {
        Gold = gold;
        Hero = hero;
    }
}
