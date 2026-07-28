using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages;

internal readonly struct VassalServiceLeft : IEvent
{
    public readonly Clan Clan;

    public VassalServiceLeft(Clan clan)
    {
        Clan = clan;
    }
}
