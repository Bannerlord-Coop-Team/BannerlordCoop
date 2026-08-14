using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages
{
    /// <summary>
    /// Event for settlement ownership change
    /// </summary>
    public record SettlementOwnershipChanged : IEvent
    {
        public string SettlementId { get; }
        public string OwnerId { get; }
        public string PreviousOwnerId { get; }
        public string CapturerId { get; }
        public int Detail { get; }

        public SettlementOwnershipChanged(string settlementId, string ownerId, string previousOwnerId, string capturerId, int detail)
        {
            SettlementId = settlementId;
            OwnerId = ownerId;
            PreviousOwnerId = previousOwnerId;
            CapturerId = capturerId;
            Detail = detail;
        }
    }
}
