using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Stances.Messages.Lifetime
{
    /// <summary>
    /// Command to create a new StanceLink on client side.
    /// </summary>
    public record StanceLinkCreated : ICommand
    {
        public StanceLinkCreated(StanceLink _StanceLink, StanceType _StanceType, IFaction _Faction1, IFaction _Faction2, bool _IsAtConstantWar)
        {
            StanceLink = _StanceLink;
            StanceType = _StanceType;
            Faction1 = _Faction1;
            Faction2 = _Faction2;
            IsAtConstantWar = _IsAtConstantWar;
        }

        public StanceLink StanceLink { get; }

        public StanceType StanceType { get; }

        public IFaction Faction1 { get; }

        public IFaction Faction2 { get; }

        public bool IsAtConstantWar { get; }
    }
}
