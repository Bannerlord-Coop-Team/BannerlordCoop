using Common.Messaging;

namespace GameInterface.Services.Towns.Messages
{
    /// <summary>
    /// Used when the Governor changes in a Town.
    /// </summary>
    public record ChangeTownGovernor : ICommand
    {

        public string TownId { get; }
        public string GovernorId { get; }

        public ChangeTownGovernor(string townId, string governorId)
        {
            TownId = townId;
            GovernorId = governorId;
        }
    }
}
