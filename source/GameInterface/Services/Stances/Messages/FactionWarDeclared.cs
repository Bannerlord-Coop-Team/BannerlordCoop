using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Stances.Messages
{
    /// <summary>
    /// Event raised on the server when DeclareWarAction declares war between two factions.
    /// </summary>
    public readonly struct FactionWarDeclared : IEvent
    {
        public readonly IFaction Faction1;
        public readonly IFaction Faction2;
        public readonly int Detail;

        public FactionWarDeclared(IFaction faction1, IFaction faction2, int detail)
        {
            Faction1 = faction1;
            Faction2 = faction2;
            Detail = detail;
        }
    }
}
