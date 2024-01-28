using Common.Messaging;

namespace GameInterface.Services.Towns.Messages
{
    /// <summary>
    /// Used when the Governor changes in a Town.
    /// </summary>
    public record TownGovernorChanged: ICommand
    {

        public string TownId { get; }
        public string GovernorId { get; }

        public TownGovernorChanged(string townId, string governorId)
        {
            TownId = townId;
            GovernorId = governorId;
        }
    }
}
