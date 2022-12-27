using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.PlayerServices;

namespace Common.Messaging
{
    public interface IMessageBroker : IDisposable
    {
        void Publish<T>(T message);
        void Publish<T>(object source, T message);

        void Subscribe<T>(Action<MessagePayload<T>> subcription);

        void Unsubscribe<T>(Action<MessagePayload<T>> subscription);
    }
}
