using Common.Messaging;
#nullable enable

namespace GameInterface.Services.Heroes.Messages
{
    /// <summary>
    /// Event to update game interface when prisoner is taken
    /// </summary>
    public record ReleasePrisoner : ICommand
    {
        public string HeroId { get; }
        public int EndCaptivityDetail { get; }
        public string? FacilitatorId { get; }

        public ReleasePrisoner(string heroId, int endCaptivityDetail, string? facilitatorId)
        {
            HeroId = heroId;
            EndCaptivityDetail = endCaptivityDetail;
            FacilitatorId = facilitatorId;
        }
    }
}
