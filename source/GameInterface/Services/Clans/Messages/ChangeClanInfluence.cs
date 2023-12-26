using Common.Messaging;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Event to update game interface when clan influence is changed
    /// </summary>
    [DontLogMessage]
    public record ChangeClanInfluence : ICommand
    {
        public string ClanId { get; }
        public float Amount { get; }

        public ChangeClanInfluence(string clanId, float amount)
        {
            ClanId = clanId;
            Amount = amount;
        }
    }
}