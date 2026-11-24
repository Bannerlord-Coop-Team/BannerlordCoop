using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;

namespace GameInterface.Services.Clans.Messages;

public readonly struct EmptyClanPartyAdded : IEvent
{
    public readonly Hero Leader;
    public readonly Ship[] Ships;

    public EmptyClanPartyAdded(Hero leader, Ship[] ships)
    {
        Leader = leader;
        Ships = ships;
    }
}

public readonly struct EmptyClanPartyRemoved : IEvent
{
    public readonly Hero Leader;

    public EmptyClanPartyRemoved(Hero leader)
    {
        Leader = leader;
    }
}
