using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Actions.Messages;

public readonly struct GovernorChanged : IEvent
{
    public readonly Town Fortification;
    public readonly Hero Governor;

    public GovernorChanged(Town fortification, Hero governor)
    {
        Fortification = fortification;
        Governor = governor;
    }
}

public readonly struct GovernorRemoved : IEvent
{
    public readonly Hero Governor;

    public GovernorRemoved(Hero governor)
    {
        Governor = governor;
    }
}
