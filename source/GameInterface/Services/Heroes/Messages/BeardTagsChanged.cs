using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages
{
    /// <summary>
    /// Event from GameInterface for BeardTags
    /// </summary>
    public record BeardTagsChanged : IEvent
    {
        public string BeardTags { get; }
        public string HeroId { get; }

        public BeardTagsChanged(string beardTags, string heroId)
        {
            BeardTags = beardTags;
            HeroId = heroId;
        }
    }
}