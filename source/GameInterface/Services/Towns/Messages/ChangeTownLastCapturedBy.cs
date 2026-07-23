using Common.Messaging;

namespace GameInterface.Services.Towns.Messages
{
    /// <summary>
    /// Used when the LastCapturedBy changes in a Town.
    /// </summary>
    public record ChangeTownLastCapturedBy : ICommand
    {
        public string TownId { get; }
        public string ClanId { get; }

        public ChangeTownLastCapturedBy(string townId, string clanId)
        {
            TownId = townId;
            ClanId = clanId;
        }
    }
}
