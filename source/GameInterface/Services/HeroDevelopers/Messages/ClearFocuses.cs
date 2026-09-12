using Common.Messaging;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Services.HeroDevelopers.Messages;

public readonly struct ClearFocuses : IEvent
{
    public readonly HeroDeveloper HeroDeveloper;

    public ClearFocuses(HeroDeveloper heroDeveloper)
    {
        HeroDeveloper = heroDeveloper;
    }
}
