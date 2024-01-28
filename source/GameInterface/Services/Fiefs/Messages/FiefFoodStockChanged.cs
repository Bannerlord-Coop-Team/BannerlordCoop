using Common.Messaging;

namespace GameInterface.Services.Fiefs.Messages
{
    /// <summary>
    /// Used when the Food stock change in a Fief.
    /// </summary>
    public record FiefFoodStockChanged : ICommand
    {
        public string FiefId { get; }
        public float FoodStockQuantity { get; }

        public FiefFoodStockChanged(string fiefId, float foodStockQuantity)
        {
            FiefId = fiefId;
            FoodStockQuantity = foodStockQuantity;
        }
    }
}
