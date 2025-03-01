using Common.Network;
using GameInterface.Services.ObjectManager;

namespace GameInterface.Utils.LocalEvents
{
    public abstract record GenericEvent<TInstance, TValue> : IGenericEvent
    {
        public GenericEvent()
        { }
        public GenericEvent(TInstance instance, TValue value)
        {
            Instance = instance;
            Value = value;
        }

        public TInstance Instance { get; set; }
        public TValue Value { get; set; }

        public abstract void HandleEvent(IObjectManager objectManager, INetwork network);
    }
}
