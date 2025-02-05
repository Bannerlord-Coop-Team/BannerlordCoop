using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages
{
    /// <summary>
    /// Event from GameInterface for TattooTags
    /// </summary>
    public record TattooTagsChanged : IEvent
    {
        public string TattooTags { get; }
        public string HeroId { get; }

        public TattooTagsChanged(string tattooTags, string heroId)
        {
            TattooTags = tattooTags;
            HeroId = heroId;
        }
    }
}