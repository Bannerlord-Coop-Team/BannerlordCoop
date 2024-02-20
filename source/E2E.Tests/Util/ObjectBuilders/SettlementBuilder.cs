using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace E2E.Tests.Util.ObjectBuilders;
internal class SettlementBuilder : IObjectBuilder
{
    private static int SettlementCounter = 0;
    public object Build()
    {
        var settlement = new Settlement(
            new TextObject($"Settlement_{Interlocked.Increment(ref SettlementCounter)}"),
            null ,
            null);

        return settlement;
    }
}
