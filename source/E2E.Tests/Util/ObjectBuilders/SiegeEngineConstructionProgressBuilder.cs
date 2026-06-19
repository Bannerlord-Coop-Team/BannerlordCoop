using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace E2E.Tests.Util.ObjectBuilders
{
    internal class SiegeEngineConstructionProgressBuilder : IObjectBuilder
    {
        public object Build()
        {
            var siegeEngineType = new SiegeEngineType();
            return new SiegeEngineConstructionProgress(siegeEngineType, 0f, 100f);
        }
    }
}
