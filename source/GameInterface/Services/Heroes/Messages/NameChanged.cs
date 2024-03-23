using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages
{
    /// <summary>
    /// Event from GameInterface for _name
    /// </summary>
    public record NameChanged : IEvent
    {
        public string NewName { get; }
        public string HeroId { get; }

        public NameChanged(string newName, string heroId)
        {
            NewName = newName;
            HeroId = heroId;
        }
    }
}