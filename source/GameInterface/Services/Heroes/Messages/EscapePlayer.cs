using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages
{
    /// <summary>
    /// Event to update game interface when player escapes
    /// </summary>
    public record EscapePlayer : ICommand
    {
        public string HeroId { get; }

        public EscapePlayer(string heroId)
        {
            HeroId = heroId;
        }
    }
}
