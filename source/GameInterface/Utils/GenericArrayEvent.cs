using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Utils
{
    public record GenericArrayEvent<TInstance, TValue> : IEvent
    {
        public GenericArrayEvent(TInstance instance, TValue value, int index)
        {
            Instance = instance;
            Value = value;
            Index = index;
        }

        public TInstance Instance { get; }
        public TValue Value { get; }
        public int Index { get; }
    }
}
