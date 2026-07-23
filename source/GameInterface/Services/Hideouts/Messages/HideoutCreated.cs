using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Hideouts.Messages;
internal class HideoutCreated : IEvent
{
    public Hideout Hideout { get; }

    public HideoutCreated(Hideout hideout)
    {
        Hideout = hideout;
    }
}
