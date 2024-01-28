using Common.Messaging;

namespace GameInterface.Services.Towns.Messages
{
    /// <summary>
    /// Used when the Food stock change in a Town.
    /// </summary>
    public record ChangeTownFoodStock : ICommand
    {
        public string TownId { get; }
        public double FoodStockQuantity { get; }

        public ChangeTownFoodStock(string townId, double foodStockQuantity)
        {
            TownId = townId;
            FoodStockQuantity = foodStockQuantity;
        }
    }
}
