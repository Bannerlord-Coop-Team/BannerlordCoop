using Common.Messaging;

namespace GameInterface.Services.Towns.Messages
{
    /// <summary>
    /// Used when the Loyalty changes in a Town.
    /// </summary>
    public record ChangeTownLoyalty : ICommand
    {
        public string TownId { get; }
        public float Loyalty { get; }

        public ChangeTownLoyalty(string townId, float loyalty)
        {
            TownId = townId;
            Loyalty = loyalty;
        }
    }
}
