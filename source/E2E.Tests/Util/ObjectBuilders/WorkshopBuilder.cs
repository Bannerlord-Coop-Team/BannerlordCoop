using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace E2E.Tests.Util.ObjectBuilders
{
    internal class WorkshopBuilder : IObjectBuilder
    {
        public object Build()
        {
            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            var workshop = new Workshop(settlement, "testWorkshop");
            workshop.SetCustomName(new TaleWorlds.Localization.TextObject("testWorkshop"));
            return workshop;
        }
    }
}
