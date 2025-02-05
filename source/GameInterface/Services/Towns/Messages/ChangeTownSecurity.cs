using Common.Logging.Attributes;
using Common.Messaging;

namespace GameInterface.Services.Towns.Messages
{
    /// <summary>
    /// Used when the Security changes in a Town.
    /// </summary>
    [BatchLogMessage]
    public record ChangeTownSecurity : ICommand
    {
        public string TownId { get; }
        public float Security { get; }

        public ChangeTownSecurity(string townId, float security)
        {
            TownId = townId;
            Security = security;
        }
    }
}
