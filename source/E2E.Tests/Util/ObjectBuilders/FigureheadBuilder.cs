using TaleWorlds.CampaignSystem.Naval;

namespace E2E.Tests.Util.ObjectBuilders;

internal class FigureheadBuilder : IObjectBuilder
{
    public object Build()
    {
        var stringId = "testFigureheadId";
        return new Figurehead(stringId);
    }
}
