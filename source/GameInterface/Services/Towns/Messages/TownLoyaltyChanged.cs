using Common.Messaging;

namespace GameInterface.Services.Towns.Messages
{
    /// <summary>
    /// Used when the Loyalty changes in a Town.
    /// </summary>
    public record TownLoyaltyChanged : ICommand
    {
        public string TownId { get; }
        public float Loyalty { get; }

        public TownLoyaltyChanged(string townId, float loyalty)
        {
            TownId = townId;
            Loyalty = loyalty;
        }
    }
}
