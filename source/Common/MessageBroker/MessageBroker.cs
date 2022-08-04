using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.MessageBroker
{
    public class MessageBroker : IMessageBroker
    {
        public void Publish<T>(object sender, T payload)
        {
            throw new NotImplementedException();
        }

        public void Subscribe<T>(Action<MessagePayload<T>> subscriber)
        {
            throw new NotImplementedException();
        }

        public void Unsubscribe<T>(Action<MessagePayload<T>> subscriber)
        {
            throw new NotImplementedException();
        }
    }
}
