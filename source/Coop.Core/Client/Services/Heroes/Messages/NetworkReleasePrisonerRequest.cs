using Common.Messaging;
using ProtoBuf;
#nullable enable

namespace Coop.Core.Client.Services.Heroes.Messages
{
    /// <summary>
    /// Request from client to server to release prisoner
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkReleasePrisonerRequest : ICommand
    {
        [ProtoMember(1)]
        public string HeroId { get; }
        [ProtoMember(2)]
        public int EndCaptivityDetail { get; }
        [ProtoMember(3)]
        public string? FacilitatorId { get; }

        public NetworkReleasePrisonerRequest(string heroId, int endCaptivityDetail, string? facilitatorId)
        {
            HeroId = heroId;
            EndCaptivityDetail = endCaptivityDetail;
            FacilitatorId = facilitatorId;
        }
    }
}
