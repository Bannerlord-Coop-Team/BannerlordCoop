using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.UI.LogEntries.Messages;

public readonly struct LogPlayerRetired : IEvent
{
    public readonly Hero RetiredHero;

    public LogPlayerRetired(Hero retiredHero)
    {
        RetiredHero = retiredHero;
    }
}
