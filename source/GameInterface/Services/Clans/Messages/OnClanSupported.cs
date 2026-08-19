using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages;

public readonly struct OnClanSupported : IEvent
{
    public readonly Clan SupporterClan;
    public readonly Clan SupportedClan;

    public OnClanSupported(Clan supporterClan, Clan supportedClan)
    {
        SupporterClan = supporterClan;
        SupportedClan = supportedClan;
    }
}
