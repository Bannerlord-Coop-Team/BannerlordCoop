using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace E2E.Tests.Util.ObjectBuilders;
internal class SettlementBuilder : IObjectBuilder
{
    private static int SettlementCounter = 0;
    public object Build()
    {
        return new Settlement(
            name: new TextObject($"Settlement_{Interlocked.Increment(ref SettlementCounter)}"),
            locationComplex: null,
            pt: null
         );
    }
}
