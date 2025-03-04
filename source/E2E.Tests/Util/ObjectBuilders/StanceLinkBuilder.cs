using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace E2E.Tests.Util.ObjectBuilders;
internal class StanceLinkBuilder : IObjectBuilder
{
    public object Build()
    { 
        var kingdom = GameObjectCreator.CreateInitializedObject<Kingdom>();
        var clan = GameObjectCreator.CreateInitializedObject<Clan>();
        return new StanceLink(StanceType.Neutral, kingdom, clan, false);
    }
}
