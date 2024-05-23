#nullable enable
using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages
{
    /// <summary>
    /// Event sent when a prisoner is released
    /// </summary>
    public record PrisonerReleased : IEvent
    {
        public string HeroId { get; }
        public int EndCaptivityDetail { get; }
        public string? FacilitatorId { get; }

        public PrisonerReleased(string heroId, int endCaptivityDetail, string? facilitatorId = null)
        {
            HeroId = heroId;
            EndCaptivityDetail = endCaptivityDetail;
            FacilitatorId = facilitatorId;
        }
    }
}
