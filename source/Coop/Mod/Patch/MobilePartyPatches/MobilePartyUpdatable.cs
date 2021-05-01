using System;
using Common;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Patch.MobilePartyPatches
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
            CampaignMapMovement.ApplyAuthoritativeState();
        }

        public int Priority { get; } = UpdatePriority.MainLoop.ApplyAuthoritativeMobilePartyState;
    }
}