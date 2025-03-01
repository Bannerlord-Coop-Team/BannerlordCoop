using Common.Messaging;
using GameInterface.Services.ObjectManager;

namespace GameInterface.Utils.NetworkEvents
{
    public interface IGenericBaseNetworkEvent : IEvent
    {
        public void HandleEvent(IObjectManager objectManager);
    }
}
