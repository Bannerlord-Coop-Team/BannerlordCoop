using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages
{
    /// <summary>
    /// Event from GameInterface for HairTags
    /// </summary>
    public record HairTagsChanged : IEvent
    {
        public string HairTags { get; }
        public string HeroId { get; }

        public HairTagsChanged(string hairTags, string heroId)
        {
            HairTags = hairTags;
            HeroId = heroId;
        }
    }
}