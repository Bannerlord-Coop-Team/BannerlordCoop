using Common.Messaging;
using ProtoBuf;
#nullable enable

namespace Coop.Core.Server.Services.Heroes.Messages
{
    /// <summary>
    /// Release prisoner is approved by server
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkReleasePrisonerApproved : ICommand
    {
        [ProtoMember(1)]
        public string HeroId { get; }
        [ProtoMember(2)]
        public int EndCaptivityDetail { get; }
        [ProtoMember(3)]
        public string? FacilitatorId { get; }

        public NetworkReleasePrisonerApproved(string heroId, int endCaptivityDetail, string facilitatorId)
        {
            HeroId = heroId;
            EndCaptivityDetail = endCaptivityDetail;
            FacilitatorId = facilitatorId;
        }
    }
}
