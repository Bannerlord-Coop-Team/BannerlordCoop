using Common.Messaging;
using System;

namespace Coop.Core.Templates.ServiceTemplate.Messages;

public record ResponseTemplate : IResponse
{
    public Guid TransactionID { get; }

    public ResponseTemplate(Guid transactionID)
    {
        TransactionID = transactionID;
    }
}
