using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages;

internal readonly struct StartRebellion : IEvent
{
    public readonly Clan Clan;

    public StartRebellion(Clan clan)
    {
        Clan = clan;
    }
}
