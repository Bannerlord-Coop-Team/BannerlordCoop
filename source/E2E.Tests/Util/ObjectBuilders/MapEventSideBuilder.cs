using Common.Util;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace E2E.Tests.Util.ObjectBuilders;
internal class MapEventSideBuilder : IObjectBuilder
{
    public object Build()
    {
        MapEvent mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
        MobileParty party = GameObjectCreator.CreateInitializedObject<MobileParty>();

        return new MapEventSide(mapEvent, TaleWorlds.Core.BattleSideEnum.Attacker, party.Party);
    }
}