using Common.Messaging;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Event to update game interface when clan renown is changed
    /// </summary>
    public record AddClanRenown : ICommand
    {
        public string ClanId { get; }
        public float Amount { get; }
        public bool ShouldNotify { get; }

        public AddClanRenown(string clanId, float amount, bool shouldNotify)
        {
            ClanId = clanId;
            Amount = amount;
            ShouldNotify = shouldNotify;
        }
    }
}