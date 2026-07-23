using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace E2E.Tests.Util.ObjectBuilders
{
    internal class WorkshopTypeBuilder : IObjectBuilder
    {
        public object Build()
        {
            var workshopType = new WorkshopType
            {
                Name = new TextObject("Smithy Workshop"),
                JobName = new TextObject("Smith"),
                Description = new TextObject("A workshop for crafting metal goods."),
                EquipmentCost = 15000,
                Frequency = 4,
                IsHidden = false,
                SignMeshName = "smithy_sign",
                PropMeshName1 = "smithy_prop_1",
                PropMeshName2 = "smithy_prop_2",
                PropMeshName3List = new List<string> { "smithy_prop_3_1", "smithy_prop_3_2" },
                PropMeshName4 = "smithy_prop_4",
                PropMeshName5 = "smithy_prop_5",
                PropMeshName6 = "smithy_prop_6"
            };

            // Create ItemCategory
            var itemCategory = GameObjectCreator.CreateInitializedObject<ItemCategory>();

            // Adding production inputs and outputs
            var production = new WorkshopType.Production(conversionSpeed: 0.9f);
            production.AddInput(itemCategory, 2);
            production.AddInput(itemCategory, 1);
            production.AddOutput(itemCategory, 5);
            workshopType._productions = new TaleWorlds.Library.MBList<WorkshopType.Production> { production };

            return workshopType;
        }
    }
}
