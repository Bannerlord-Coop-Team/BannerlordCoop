using Common.Logging.Attributes;
using Common.Messaging;

namespace GameInterface.Services.Fiefs.Messages
{
    /// <summary>
    /// Used when the Food stock change in a Fief.
    /// </summary>
    [BatchLogMessage]
    public record ChangeFiefFoodStock : ICommand
    {
        public string FiefId { get; }
        public float FoodStockQuantity { get; }

        public ChangeFiefFoodStock(string fiefId, float foodStockQuantity)
        {
            FiefId = fiefId;
            FoodStockQuantity = foodStockQuantity;
        }
    }
}
