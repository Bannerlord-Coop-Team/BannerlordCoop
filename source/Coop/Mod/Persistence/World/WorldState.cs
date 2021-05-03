﻿using System.ComponentModel;
using RailgunNet.Logic;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Persistence.World
{
    /// <summary>
    ///     Global world state.
    /// </summary>
    public class WorldState : RailState
    {
        public override string ToString()
        {
            return $"{CampaignTimeTicks}";
        }

        #region synced data

        [Mutable] public long CampaignTimeTicks { get; set; }
        #endregion
    }
}
