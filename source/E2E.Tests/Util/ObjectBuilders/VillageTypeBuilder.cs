using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Settlements;

namespace E2E.Tests.Util.ObjectBuilders;
internal class VillageTypeBuilder : IObjectBuilder
{
    public object Build()
    {
        return new VillageType("testVillageType");
    }
}
