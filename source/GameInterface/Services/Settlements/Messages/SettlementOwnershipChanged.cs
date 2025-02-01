using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Settlements.Messages
{
    /// <summary>
    /// Event for settlement ownership change
    /// </summary>
    public record SettlementOwnershipChanged : IEvent
    {
        public string SettlementId { get; }
        public string OwnerId { get; }
        public string CapturerId { get; }
        public int Detail { get; }

        public SettlementOwnershipChanged(string settlementId, string ownerId, string capturerId, int detail)
        {
            SettlementId = settlementId;
            OwnerId = ownerId;
            CapturerId = capturerId;
            Detail = detail;
        }
    }
}
