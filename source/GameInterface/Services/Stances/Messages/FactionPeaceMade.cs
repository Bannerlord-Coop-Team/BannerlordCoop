using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Stances.Messages
{
    /// <summary>
    /// Event raised on the server when MakePeaceAction makes peace between two factions.
    /// </summary>
    public readonly struct FactionPeaceMade : IEvent
    {
        public readonly IFaction Faction1;
        public readonly IFaction Faction2;
        public readonly int DailyTribute;
        public readonly int DailyTributeDuration;
        public readonly int Detail;

        public FactionPeaceMade(IFaction faction1, IFaction faction2, int dailyTribute, int dailyTributeDuration, int detail)
        {
            Faction1 = faction1;
            Faction2 = faction2;
            DailyTribute = dailyTribute;
            DailyTributeDuration = dailyTributeDuration;
            Detail = detail;
        }
    }
}
