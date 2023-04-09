using System;
using System.Collections.Generic;
using System.Text;

namespace Common.Messaging
{
    public interface IResponse : IEvent
    {
        Guid TransactionID { get; }
    }
}
