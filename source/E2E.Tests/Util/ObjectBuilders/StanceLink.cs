using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Siege;

namespace E2E.Tests.Util.ObjectBuilders
{
    public class StanceLinkBuilder : IObjectBuilder
    {
        public object Build()
        {
            Kingdom kingdom1 = new();
            Kingdom kingdom2 = new();
            return new StanceLink(StanceType.Neutral, kingdom1, kingdom2, false);
        }
    }
}
