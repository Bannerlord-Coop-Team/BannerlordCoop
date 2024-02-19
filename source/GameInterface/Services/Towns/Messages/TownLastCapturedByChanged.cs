using Common.Messaging;

namespace GameInterface.Services.Towns.Messages
{
    /// <summary>
    /// Used when the LastCapturedBy changes in a Town.
    /// </summary>
    public record TownLastCapturedByChanged : ICommand
    {
        public string TownId { get; }
        public string ClanId { get; }

        public TownLastCapturedByChanged(string townId, string clanId)
        {
            TownId = townId;
            ClanId = clanId;
        }
    }
}
