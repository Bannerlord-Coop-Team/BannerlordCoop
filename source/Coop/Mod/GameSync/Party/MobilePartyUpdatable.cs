using System;
using Common;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.GameSync.Party
{
    /// <summary>
    ///     Updatable that applies all known serverside state of a mobile party to the local game state.
    /// </summary>
    public class MobilePartyUpdatable : IUpdateable
    {
        public void Update(TimeSpan frameTime)
        {
            if (!Coop.IsCoopGameSession() ||
                MBObjectManager.Instance == null)
            {
                return;
            }

            foreach(MobileParty party in Campaign.Current.MobileParties)
            {
                MobilePartyManaged.ApplyAuthoritativeState(party);
            }
        }

        public int Priority { get; } = UpdatePriority.MainLoop.ApplyAuthoritativeMobilePartyState;
    }
}