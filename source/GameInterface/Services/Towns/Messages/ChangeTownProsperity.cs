using Common.Logging.Attributes;
using Common.Messaging;

namespace GameInterface.Services.Towns.Messages
{
    /// <summary>
    /// Used when the Prosperity changes in a Town.
    /// </summary>
    [BatchLogMessage]
    public record ChangeTownProsperity : ICommand
    {
        public string TownId { get; }
        public float Prosperity { get; }

        public ChangeTownProsperity(string townId, float prosperity)
        {
            TownId = townId;
            Prosperity = prosperity;
        }
    }
}
