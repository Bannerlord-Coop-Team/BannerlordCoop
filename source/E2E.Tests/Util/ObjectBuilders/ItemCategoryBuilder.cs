using TaleWorlds.Core;

namespace E2E.Tests.Util.ObjectBuilders
{
    internal class ItemCategoryBuilder : IObjectBuilder
    {
        public object Build()
        {
            return new ItemCategory("smithing_materials").InitializeObject(
                isTradeGood: true,
                baseDemand: 500,
                luxuryDemand: 200,
                properties: ItemCategory.Property.BonusToProduction,
                canSubstitute: null,
                substitutionFactor: 0,
                isAnimal: false,
                isValid: true
            );
        }
    }
}
