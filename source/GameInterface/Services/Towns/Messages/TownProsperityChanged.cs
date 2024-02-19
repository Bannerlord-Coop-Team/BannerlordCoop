using Common.Messaging;

namespace GameInterface.Services.Towns.Messages
{
    /// <summary>
    /// Used when the Prosperity changes in a Town.
    /// </summary>
    public record TownProsperityChanged : ICommand
    {
        public string TownId { get; }
        public float Prosperity { get; }

        public TownProsperityChanged(string townId, float prosperity)
        {
            TownId = townId;
            Prosperity = prosperity;
        }
    }
}
