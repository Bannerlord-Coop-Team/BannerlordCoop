using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;

namespace GameInterface.Utils.LocalEvents
{
    public interface IGenericEvent : IEvent
    {
        public void HandleEvent(IObjectManager objectManager, INetwork network);
    }
}
